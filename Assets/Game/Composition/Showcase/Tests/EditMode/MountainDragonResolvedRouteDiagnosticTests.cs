using System;
using System.Text;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonResolvedRouteDiagnosticTests
    {
        private const uint Seed = 0x5EED1234;
        private const string RouteLogPrefix = "MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=";
        private const string TerminalLogPrefix = "MOUNTAIN_DRAGON_TERMINAL_CORRIDOR=";
        private const string TerminalWinnerLogPrefix = "MOUNTAIN_DRAGON_TERMINAL_WINNER=";
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

        [Test]
        public void CurrentProductionTerminalWinnerSerializesForCollisionIsolation()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);
            Assert.That(route.Road.Points.Count, Is.GreaterThan(91));

            FeatureCatalogue catalogue = WorldBuilderRoadVoxelCatalogue.Build(
                ascent,
                RoadSurfaceMaterial,
                Allocator.Temp);
            var corridors = new NativeArray<Primitive>(
                catalogue.Definitions.Length,
                Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var names = new string[catalogue.Definitions.Length];
            try
            {
                for (int definitionIndex = 0; definitionIndex < catalogue.Definitions.Length; definitionIndex++)
                {
                    corridors[definitionIndex] = DecodeCorridor(catalogue, definitionIndex);
                    names[definitionIndex] = catalogue.Definitions[definitionIndex].Name.ToString();
                }

                ResolvedWorldRoadPoint from = route.Road.Points[90];
                ResolvedWorldRoadPoint to = route.Road.Points[91];
                var diagnostic = new StringBuilder(4096);
                diagnostic.Append("p89=");
                AppendPoint(diagnostic, route.Road.Points[89]);
                diagnostic.Append(" p90=");
                AppendPoint(diagnostic, from);
                diagnostic.Append(" p91=");
                AppendPoint(diagnostic, to);

                // The built-player hard-stop is centred on p90. Sample the outgoing centreline plus
                // approximately 4.5 dm to either side, matching the player capsule-scale footprint,
                // using the production order-independent corridor winner instead of reimplementing it.
                diagnostic.Append(" outgoing=");
                for (int sample = 0; sample <= 8; sample++)
                {
                    int xdm = LerpRounded(from.Xdm, to.Xdm, sample, 8);
                    int zdm = LerpRounded(from.Zdm, to.Zdm, sample, 8);
                    if (sample > 0) diagnostic.Append(';');
                    diagnostic.Append(sample).Append('{');
                    AppendWinner(diagnostic, corridors, names, xdm, zdm, "C");
                    diagnostic.Append(',');
                    AppendWinner(diagnostic, corridors, names, xdm + 2, zdm - 4, "L");
                    diagnostic.Append(',');
                    AppendWinner(diagnostic, corridors, names, xdm - 2, zdm + 4, "R");
                    diagnostic.Append('}');
                }

                Debug.Log(TerminalWinnerLogPrefix + diagnostic);
            }
            finally
            {
                corridors.Dispose();
                catalogue.Dispose();
            }
        }

        private static Primitive DecodeCorridor(FeatureCatalogue catalogue, int definitionIndex)
        {
            FeatureDefinition definition = catalogue.Definitions[definitionIndex];
            int pc = definition.ProgramOffset;
            Assert.That((ShapeOp)catalogue.Program[pc], Is.EqualTo(ShapeOp.EmitTerrainCorridor),
                definition.Name.ToString());
            Assert.That(catalogue.Program[pc + 1], Is.Zero,
                "Road corridor diagnostics require immediate operands only.");
            int operand = pc + 2;
            ExplicitPlacement placement = catalogue.ExplicitPlacements[definitionIndex];

            Primitive primitive = default;
            primitive.Shape = PrimitiveShape.TerrainCorridor;
            primitive.Mode = PrimitiveMode.TerrainCorridor;
            primitive.A = placement.Position;
            primitive.A.x += catalogue.Program[operand + 0];
            primitive.A.y += catalogue.Program[operand + 1];
            primitive.A.z += catalogue.Program[operand + 2];
            primitive.B = placement.Position;
            primitive.B.x += catalogue.Program[operand + 3];
            primitive.B.y += catalogue.Program[operand + 4];
            primitive.B.z += catalogue.Program[operand + 5];
            primitive.InnerRadius = catalogue.Program[operand + 6];
            primitive.Radius = catalogue.Program[operand + 7];
            primitive.C.x = catalogue.Program[operand + 8];
            primitive.C.y = catalogue.Program[operand + 9];
            primitive.C.z = catalogue.Program[operand + 10];
            primitive.D.x = catalogue.Program[operand + 11];
            primitive.Material = (byte)catalogue.Program[operand + 12];
            primitive.D.y = catalogue.Program[operand + 13];
            primitive.D.z = catalogue.Program[operand + 14];
            return primitive;
        }

        private static void AppendWinner(
            StringBuilder diagnostic,
            NativeArray<Primitive> corridors,
            string[] names,
            int xdm,
            int zdm,
            string label)
        {
            diagnostic.Append(label).Append('@').Append(xdm).Append(',').Append(zdm).Append('=');
            bool found = ContinuousTerrainCorridorRasteriser.TryChoose(
                corridors,
                xdm,
                zdm,
                out Primitive winner);
            Assert.That(found, Is.True, "No corridor winner at terminal sample.");
            bool sampled = TerrainCorridorRasteriser.TrySample(
                in winner,
                xdm,
                zdm,
                out TerrainCorridorSample corridorSample);
            Assert.That(sampled, Is.True);

            int winnerIndex = FindPrimitive(corridors, in winner);
            Assert.That(winnerIndex, Is.GreaterThanOrEqualTo(0));
            diagnostic.Append(names[winnerIndex])
                .Append(" targetY=").Append(corridorSample.TargetHeightVoxels)
                .Append(" dist=").Append(corridorSample.DistanceDm)
                .Append(" surf=").Append(corridorSample.SurfaceCoverage31)
                .Append(" grade=").Append(corridorSample.Coverage31);
        }

        private static int FindPrimitive(NativeArray<Primitive> corridors, in Primitive winner)
        {
            for (int i = 0; i < corridors.Length; i++)
            {
                Primitive candidate = corridors[i];
                if (candidate.Shape == winner.Shape
                    && candidate.Mode == winner.Mode
                    && candidate.Material == winner.Material
                    && candidate.A.Equals(winner.A)
                    && candidate.B.Equals(winner.B)
                    && candidate.Radius == winner.Radius
                    && candidate.InnerRadius == winner.InnerRadius
                    && candidate.C.Equals(winner.C)
                    && candidate.D.Equals(winner.D))
                    return i;
            }
            return -1;
        }

        private static void AppendPoint(StringBuilder diagnostic, ResolvedWorldRoadPoint point)
        {
            diagnostic.Append(point.Xdm).Append(',').Append(point.Ydm).Append(',').Append(point.Zdm);
        }

        private static int LerpRounded(int from, int to, int numerator, int denominator)
        {
            long scaled = (long)from * (denominator - numerator) + (long)to * numerator;
            if (scaled >= 0) return (int)((scaled + denominator / 2) / denominator);
            return (int)((scaled - denominator / 2) / denominator);
        }
    }
}
