using System.Collections.Generic;
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
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            FeatureCatalogue mountainCatalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                MountainMaterial,
                PathMaterial,
                DragonMaterial,
                Allocator.Temp);
            try
            {
                int mountainId = FindDefinition(
                    mountainCatalogue,
                    WorldBuilderMountainLandmarkCatalogue.LandformDefinitionName);
                int dragonId = FindDefinition(
                    mountainCatalogue,
                    WorldBuilderMountainLandmarkCatalogue.PlaceholderDefinitionName);
                Assert.That(mountainId, Is.GreaterThanOrEqualTo(0));
                Assert.That(dragonId, Is.GreaterThanOrEqualTo(0));

                FeatureDefinition mountain = mountainCatalogue.Definitions[mountainId];
                Assert.That(mountain.Kind, Is.EqualTo(FeatureKind.Landform));
                Assert.That(mountain.Footprint.x, Is.GreaterThanOrEqualTo(1000),
                    "The authored landmark must remain substantial, not collapse into a hill-sized prop.");
                Assert.That(mountain.Footprint.x, Is.LessThanOrEqualTo(FeatureBudget.MaxFootprintVoxels));

                List<Primitive> mountainPrimitives = Evaluate(mountainCatalogue, mountainId, Seed);
                Primitive frustum = mountainPrimitives.Find(p => p.Shape == PrimitiveShape.Frustum);
                Assert.That(frustum.Shape, Is.EqualTo(PrimitiveShape.Frustum));
                Assert.That(frustum.B.y, Is.EqualTo(spec.Origin.y + spec.MountainHeight));

                List<Primitive> ramps = mountainPrimitives.FindAll(p => p.Shape == PrimitiveShape.Ramp);
                ramps.Sort((a, b) => a.A.y.CompareTo(b.A.y));
                Assert.That(ramps.Count, Is.GreaterThanOrEqualTo(spec.SwitchbackCount + 1));
                Assert.That(ramps[0].A.y, Is.EqualTo(spec.Origin.y));
                Assert.That(ramps[ramps.Count - 1].B.y, Is.EqualTo(spec.Origin.y + spec.MountainHeight));

                for (int i = 0; i < ramps.Count; i++)
                {
                    Primitive ramp = ramps[i];
                    int run = ramp.Axis == 0 ? ramp.B.x - ramp.A.x + 1
                            : ramp.Axis == 2 ? ramp.B.z - ramp.A.z + 1
                            : ramp.B.y - ramp.A.y + 1;
                    int rise = ramp.B.y - ramp.A.y + 1;
                    Assert.That(rise * 4, Is.LessThanOrEqualTo(run),
                        "Every ascent segment must remain shallow enough for normal movement.");

                    if (i == 0) continue;
                    Primitive previous = ramps[i - 1];
                    Assert.That(ramp.A.y, Is.LessThanOrEqualTo(previous.B.y + 1),
                        "Switchbacks may not introduce a vertical jump between ascent segments.");
                    Assert.That(HasPathLanding(mountainPrimitives, previous, ramp), Is.True,
                        "Each change of direction must be joined by a path-surface landing.");
                }

                Assert.That(HasSummitConnection(mountainPrimitives, in spec), Is.True,
                    "The final ascent must join the usable summit rather than stop below it.");

                List<Primitive> dragonPrimitives = Evaluate(mountainCatalogue, dragonId, Seed);
                Assert.That(dragonPrimitives.Count, Is.EqualTo(1));
                Primitive cube = dragonPrimitives[0];
                Assert.That(cube.Shape, Is.EqualTo(PrimitiveShape.Box));
                Assert.That(cube.Material, Is.EqualTo(DragonMaterial),
                    "The placeholder dragon must use the authored red showcase material.");
                int3 cubeSize = cube.B - cube.A + 1;
                Assert.That(math.all(cubeSize == new int3(
                    spec.PlaceholderSize, spec.PlaceholderSize, spec.PlaceholderSize)), Is.True);
                Assert.That(cube.A.y, Is.EqualTo(spec.Origin.y + spec.MountainHeight + 1),
                    "The placeholder must sit directly on top of the authored summit.");
            }
            finally
            {
                mountainCatalogue.Dispose();
            }

            FeatureCatalogue productionCatalogue = ShowcaseCatalogue.Build(Seed, Allocator.Temp);
            try
            {
                Assert.That(
                    FindDefinition(productionCatalogue, WorldBuilderMountainLandmarkCatalogue.LandformDefinitionName),
                    Is.GreaterThanOrEqualTo(0));
                int productionDragonId = FindDefinition(
                    productionCatalogue,
                    WorldBuilderMountainLandmarkCatalogue.PlaceholderDefinitionName);
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
            MountainLandmarkSpec landmark = encounter.Landmark;
            Assert.That(encounter.Update(landmark.Origin.x - 200, landmark.Origin.z - 200, 16), Is.EqualTo(0));
            Assert.That(encounter.ActiveDialogue, Is.Null);
            Assert.That(
                encounter.Update(landmark.SummitApproachWorldX, landmark.SummitApproachWorldZ, 16),
                Is.EqualTo(1));
            Assert.That(encounter.HasTriggered, Is.True);
            Assert.That(encounter.ActiveDialogue, Is.EqualTo("Hello, I'm Mr. Dragon."));

            encounter.Update(landmark.Origin.x - 200, landmark.Origin.z - 200, 6000);
            Assert.That(encounter.ActiveDialogue, Is.Null);
            Assert.That(
                encounter.Update(landmark.SummitApproachWorldX, landmark.SummitApproachWorldZ, 16),
                Is.EqualTo(0),
                "The one-shot proximity source must not restart the completed cutscene.");
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

        private static bool HasPathLanding(List<Primitive> primitives, Primitive from, Primitive to)
        {
            int x = from.Direction < 0 ? from.A.x : from.B.x;
            int y = from.B.y;
            for (int i = 0; i < primitives.Count; i++)
            {
                Primitive p = primitives[i];
                if (p.Shape != PrimitiveShape.Box || p.Material != PathMaterial || p.A.y != y) continue;
                if (x < p.A.x || x > p.B.x) continue;
                bool touchesFrom = p.A.z <= from.B.z + 1 && p.B.z + 1 >= from.A.z;
                bool touchesTo = p.A.z <= to.B.z + 1 && p.B.z + 1 >= to.A.z;
                if (touchesFrom && touchesTo) return true;
            }
            return false;
        }

        private static bool HasSummitConnection(List<Primitive> primitives, in MountainLandmarkSpec spec)
        {
            int summitSouth = spec.Origin.z + spec.CentreLocal - spec.SummitRadius;
            int summitY = spec.Origin.y + spec.MountainHeight;
            for (int i = 0; i < primitives.Count; i++)
            {
                Primitive p = primitives[i];
                if (p.Shape != PrimitiveShape.Box || p.Material != PathMaterial || p.A.y != summitY) continue;
                if (p.B.z + 1 >= summitSouth) return true;
            }
            return false;
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
