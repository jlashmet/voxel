using System;
using System.Text;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonResolvedRouteDiagnosticTests
    {
        private const uint Seed = 0x5EED1234;
        private const string RouteLogPrefix = "MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=";
        private const string TerminalLogPrefix = "MOUNTAIN_DRAGON_TERMINAL_CORRIDOR=";
        private const byte RoadSurfaceMaterial = 13;

        [Test]
        public void CurrentProductionRouteSerializesForSummitRootCauseIsolation()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);

            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);
            Assert.That(route.Road.Points.Count, Is.GreaterThan(20));

            var serialized = new StringBuilder(route.Road.Points.Count * 20);
            for (int i = 0; i < route.Road.Points.Count; i++)
            {
                if (i > 0) serialized.Append(';');
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                serialized.Append(point.Xdm)
                    .Append(',')
                    .Append(point.Ydm)
                    .Append(',')
                    .Append(point.Zdm);
            }

            Debug.Log(RouteLogPrefix + serialized);
        }

        [Test]
        public void CurrentProductionTerminalCorridorSerializesForCollisionIsolation()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);

            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);
            Assert.That(route.Road.Points.Count, Is.GreaterThan(91));

            var diagnostic = new StringBuilder(2048);
            diagnostic.Append("points=");
            for (int i = 88; i <= 91; i++)
            {
                if (i > 88) diagnostic.Append(';');
                ResolvedWorldRoadPoint point = route.Road.Points[i];
                diagnostic.Append(i)
                    .Append(':')
                    .Append(point.Xdm).Append(',')
                    .Append(point.Ydm).Append(',')
                    .Append(point.Zdm).Append(" terrainY=")
                    .Append(surface.HeightAtDm(point.Xdm, point.Zdm));
            }

            ResolvedWorldRoadPoint from = route.Road.Points[90];
            ResolvedWorldRoadPoint to = route.Road.Points[91];
            diagnostic.Append(" samples90to91=");
            for (int sample = 0; sample <= 8; sample++)
            {
                int xdm = LerpRounded(from.Xdm, to.Xdm, sample, 8);
                int zdm = LerpRounded(from.Zdm, to.Zdm, sample, 8);
                int roadYdm = LerpRounded(from.Ydm, to.Ydm, sample, 8);
                int terrainYdm = surface.HeightAtDm(xdm, zdm);
                if (sample > 0) diagnostic.Append(';');
                diagnostic.Append(sample)
                    .Append(':').Append(xdm).Append(',').Append(zdm)
                    .Append(" roadY=").Append(roadYdm)
                    .Append(" terrainY=").Append(terrainYdm)
                    .Append(" delta=").Append(terrainYdm - roadYdm);
            }

            FeatureCatalogue catalogue = WorldBuilderRoadVoxelCatalogue.Build(
                ascent,
                RoadSurfaceMaterial,
                Allocator.Temp);
            try
            {
                int corridorCount = 0;
                diagnostic.Append(" corridorPrograms=");
                for (int definitionIndex = 0; definitionIndex < catalogue.Definitions.Length; definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    for (int pc = definition.ProgramOffset; pc < end;)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        int length = ShapeOps.InstructionLength(op);
                        Assert.That(length, Is.GreaterThan(0));
                        if (op == ShapeOp.EmitTerrainCorridor)
                        {
                            if (corridorCount > 0) diagnostic.Append('|');
                            diagnostic.Append("def").Append(definitionIndex)
                                .Append("@pc").Append(pc).Append('[');
                            for (int word = 0; word < length; word++)
                            {
                                if (word > 0) diagnostic.Append(',');
                                diagnostic.Append(catalogue.Program[pc + word]);
                            }
                            diagnostic.Append(']');
                            corridorCount++;
                        }
                        pc += length;
                        if (op == ShapeOp.End) break;
                    }
                }
                Assert.That(corridorCount, Is.GreaterThan(0));
            }
            finally
            {
                catalogue.Dispose();
            }

            Debug.Log(TerminalLogPrefix + diagnostic);
        }

        private static int LerpRounded(int from, int to, int numerator, int denominator)
        {
            long scaled = (long)from * (denominator - numerator) + (long)to * numerator;
            if (scaled >= 0) return (int)((scaled + denominator / 2) / denominator);
            return (int)((scaled - denominator / 2) / denominator);
        }
    }
}
