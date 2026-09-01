using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonProductionAcceptanceTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte MountainMaterial = 1;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;

        [Test]
        public void MountainPathDragonAndProximityFlowUseProductionWorldBuilder()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            MountainLandformSpec spec = surface.Spec;
            MountainLandformMass summit = surface.GetMass(0);

            FeatureCatalogue mountainCatalogue = WorldBuilderMountainLandformCatalogue.Build(
                surface,
                MountainMaterial,
                Allocator.Temp);
            try
            {
                int mountainId = FindDefinition(
                    mountainCatalogue,
                    WorldBuilderMountainLandformCatalogue.LandformDefinitionName);
                Assert.That(mountainId, Is.GreaterThanOrEqualTo(0));

                FeatureDefinition mountain = mountainCatalogue.Definitions[mountainId];
                Assert.That(mountain.Kind, Is.EqualTo(FeatureKind.Landform));
                int authoredDiameter = 2 * Math.Max(spec.RadiusXdm, spec.RadiusZdm);
                int realizedDiameter = Math.Max(mountain.Footprint.x, mountain.Footprint.z);
                Assert.That(authoredDiameter, Is.GreaterThanOrEqualTo(1000),
                    "The authored landmark must remain substantial, not collapse into a hill-sized prop.");
                Assert.That(realizedDiameter * 5, Is.GreaterThanOrEqualTo(authoredDiameter * 4),
                    "The realized mountain must occupy at least 80% of its authored major-axis diameter.");
                Assert.That(mountain.Footprint.x, Is.LessThanOrEqualTo(FeatureBudget.MaxFootprintVoxels));
                Assert.That(mountain.Footprint.z, Is.LessThanOrEqualTo(FeatureBudget.MaxFootprintVoxels));

                List<Primitive> mountainPrimitives = Evaluate(mountainCatalogue, mountainId, Seed);
                int frustumCount = mountainPrimitives.FindAll(p => p.Shape == PrimitiveShape.Frustum).Count;
                Assert.That(frustumCount, Is.EqualTo(surface.MassCount),
                    "The production landform catalogue must realize the authoritative mountain masses.");
                Assert.That(surface.HeightAtDm(summit.CentreXdm, summit.CentreZdm), Is.EqualTo(summit.TopYdm));
            }
            finally
            {
                mountainCatalogue.Dispose();
            }

            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);
            Assert.That(route.Road.Points.Count, Is.GreaterThan(20));
            Assert.That(route.Road.Points[0].Xdm, Is.EqualTo(ShowcaseMountainDragonLayout.EntryXdm).Within(40));
            Assert.That(route.Road.Points[0].Zdm, Is.EqualTo(ShowcaseMountainDragonLayout.EntryZdm).Within(40));

            for (int i = 1; i < route.Road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint a = route.Road.Points[i - 1];
                ResolvedWorldRoadPoint b = route.Road.Points[i];
                int horizontal = ResolverPlanarDistance(a, b);
                int rise = Math.Abs(b.Ydm - a.Ydm);
                Assert.That((long)rise * 1000L,
                    Is.LessThanOrEqualTo((long)horizontal * route.Road.Intent.Profile.MaximumGradePermille),
                    $"Resolved production ascent segment {i - 1} exceeds the configured grade contract.");
            }

            ResolvedWorldRoadPoint summitApproach = ShowcaseMountainDragonLayout.SummitApproach(ascent);
            Assert.That(summitApproach.Xdm, Is.EqualTo(summit.CentreXdm).Within(40));
            Assert.That(summitApproach.Zdm, Is.EqualTo(summit.CentreZdm).Within(40));
            Assert.That(summitApproach.Ydm, Is.GreaterThan(spec.OriginYdm));

            FeatureCatalogue roadCatalogue = WorldBuilderRoadVoxelCatalogue.Build(
                ascent,
                PathMaterial,
                Allocator.Temp);
            try
            {
                int terrainCorridorCount = 0;
                for (int definitionIndex = 0; definitionIndex < roadCatalogue.Definitions.Length; definitionIndex++)
                {
                    FeatureDefinition definition = roadCatalogue.Definitions[definitionIndex];
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    for (int pc = definition.ProgramOffset; pc < end;)
                    {
                        ShapeOp op = (ShapeOp)roadCatalogue.Program[pc];
                        if (op == ShapeOp.EmitTerrainCorridor) terrainCorridorCount++;
                        Assert.That(op, Is.Not.EqualTo(ShapeOp.EmitRamp),
                            "Production ascent must lower through the shared terrain-corridor path, not legacy ramps.");
                        int length = ShapeOps.InstructionLength(op);
                        Assert.That(length, Is.GreaterThan(0));
                        pc += length;
                        if (op == ShapeOp.End) break;
                    }
                }
                Assert.That(terrainCorridorCount, Is.GreaterThan(0));
            }
            finally
            {
                roadCatalogue.Dispose();
            }

            FeatureCatalogue placeholderCatalogue = WorldBuilderMountainSummitPlaceholderCatalogue.Build(
                surface,
                ShowcaseMountainDragonLayout.PlaceholderSize,
                DragonMaterial,
                Allocator.Temp);
            try
            {
                int dragonId = FindDefinition(
                    placeholderCatalogue,
                    WorldBuilderMountainSummitPlaceholderCatalogue.DefinitionName);
                Assert.That(dragonId, Is.GreaterThanOrEqualTo(0));
                List<Primitive> dragonPrimitives = Evaluate(placeholderCatalogue, dragonId, Seed);
                Assert.That(dragonPrimitives.Count, Is.EqualTo(1));
                Primitive cube = dragonPrimitives[0];
                Assert.That(cube.Shape, Is.EqualTo(PrimitiveShape.Box));
                Assert.That(cube.Material, Is.EqualTo(DragonMaterial));
                int3 cubeSize = cube.B - cube.A + 1;
                Assert.That(math.all(cubeSize == new int3(
                    ShowcaseMountainDragonLayout.PlaceholderSize,
                    ShowcaseMountainDragonLayout.PlaceholderSize,
                    ShowcaseMountainDragonLayout.PlaceholderSize)), Is.True);
                Assert.That(cube.A.y, Is.EqualTo(summit.TopYdm + 1),
                    "The placeholder must sit directly on the authoritative summit crest.");
            }
            finally
            {
                placeholderCatalogue.Dispose();
            }

            FeatureCatalogue productionCatalogue = ShowcaseCatalogue.Build(Seed, Allocator.Temp);
            try
            {
                Assert.That(
                    FindDefinition(productionCatalogue, WorldBuilderMountainLandformCatalogue.LandformDefinitionName),
                    Is.GreaterThanOrEqualTo(0));
                int productionDragonId = FindDefinition(
                    productionCatalogue,
                    WorldBuilderMountainSummitPlaceholderCatalogue.DefinitionName);
                Assert.That(productionDragonId, Is.GreaterThanOrEqualTo(0));
                List<Primitive> productionDragon = Evaluate(productionCatalogue, productionDragonId, Seed);
                Assert.That(productionDragon.Count, Is.EqualTo(1));
                Assert.That(productionDragon[0].Material, Is.EqualTo(DragonMaterial),
                    "Production Showcase composition must preserve the red placeholder binding.");
            }
            finally
            {
                productionCatalogue.Dispose();
            }

            var encounter = new MountainDragonEncounterRuntime(Seed);
            MountainLandformSpec landmark = encounter.Landmark;
            ResolvedWorldRoadPoint encounterSummit = ShowcaseMountainDragonLayout.SummitApproach(encounter.Ascent);
            Assert.That(encounter.Update(landmark.OriginXdm - 200, landmark.OriginZdm - 200, 16), Is.EqualTo(0));
            Assert.That(encounter.ActiveDialogue, Is.Null);
            Assert.That(
                encounter.Update(encounterSummit.Xdm, encounterSummit.Zdm, 16),
                Is.EqualTo(1));
            Assert.That(encounter.HasTriggered, Is.True);
            Assert.That(encounter.ActiveDialogue, Is.EqualTo("Hello, I'm Mr. Dragon."));

            encounter.Update(landmark.OriginXdm - 200, landmark.OriginZdm - 200, 6000);
            Assert.That(encounter.ActiveDialogue, Is.Null);
            Assert.That(
                encounter.Update(encounterSummit.Xdm, encounterSummit.Zdm, 16),
                Is.EqualTo(0),
                "The one-shot proximity source must not restart the completed cutscene.");
        }

        private static int ResolverPlanarDistance(ResolvedWorldRoadPoint a, ResolvedWorldRoadPoint b)
        {
            long dx = (long)b.Xdm - a.Xdm;
            long dz = (long)b.Zdm - a.Zdm;
            return Math.Max(1, IntegerSqrtRounded(dx * dx + dz * dz));
        }

        private static int IntegerSqrtRounded(long value)
        {
            if (value <= 0) return 0;
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                if (middle <= value / middle) low = middle + 1;
                else high = middle - 1;
            }

            long root = high;
            long lowerError = value - root * root;
            long next = root + 1;
            if (next <= 3037000499L)
            {
                long upperError = next * next - value;
                if (upperError <= lowerError) root = next;
            }
            return root > int.MaxValue ? int.MaxValue : (int)root;
        }

        private static List<Primitive> Evaluate(FeatureCatalogue catalogue, int definitionId, uint seed)
        {
            FeatureDefinition definition = catalogue.Definitions[definitionId];
            ExplicitPlacement placement = PlacementFor(catalogue, definitionId);
            ParameterSet parameters = FeatureGeneration.ResolveParameters(
                in catalogue, in definition, in placement,
                definitionId, placement.Position, seed);

            using var primitives = new NativeList<Primitive>(64, Allocator.Temp);
            using var anchors = new NativeList<ResolvedAnchor>(8, Allocator.Temp);
            EvaluationResult result = ShapeProgram.Evaluate(
                in catalogue,
                definitionId,
                in parameters,
                placement.Position,
                placement.Orientation,
                seed,
                FeatureGeneration.InstanceSeed(seed, definitionId, placement.Position),
                primitives,
                anchors);
            Assert.That(result, Is.EqualTo(EvaluationResult.Ok));

            var copy = new List<Primitive>(primitives.Length);
            for (int i = 0; i < primitives.Length; i++) copy.Add(primitives[i]);
            return copy;
        }

        private static int FindDefinition(FeatureCatalogue catalogue, string name)
        {
            for (int i = 0; i < catalogue.DefinitionCount; i++)
                if (catalogue.Definitions[i].Name.ToString() == name) return i;
            return -1;
        }

        private static ExplicitPlacement PlacementFor(FeatureCatalogue catalogue, int definitionId)
        {
            for (int i = 0; i < catalogue.Rules.Length; i++)
            {
                PlacementRule rule = catalogue.Rules[i];
                if (rule.DefinitionId != definitionId || rule.ExplicitCount <= 0) continue;
                return catalogue.ExplicitPlacements[rule.ExplicitOffset];
            }
            Assert.Fail("No explicit placement found for definition " + definitionId + ".");
            return default;
        }
    }
}