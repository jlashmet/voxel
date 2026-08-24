using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Hightown;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// The two-town slice: Kentridge is recovered content and must keep every building the original
    /// game had, while Hightown is generated and must read as a different town rather than as
    /// Kentridge rearranged.
    /// </summary>
    public sealed class TwoTownWorldTests
    {
        private const uint Seed = 0x4B454E54u;

        /// <summary>
        /// Every role recovered from the original MountingForce Kentridge maps. This is the list the
        /// slice is judged against, so it is spelled out here rather than derived from the enum —
        /// deriving it would make the test agree with any future deletion.
        /// </summary>
        private static readonly KentridgeRole[] RecoveredRoles =
        {
            KentridgeRole.Inn,
            KentridgeRole.Pub,
            KentridgeRole.Church,
            KentridgeRole.MayorHouse,
            KentridgeRole.WeaponShop,
            KentridgeRole.ArmorShop,
            KentridgeRole.MagicShop,
            KentridgeRole.LoganHouse,
            KentridgeRole.RebeccaHouse,
            KentridgeRole.SarahHouse,
            KentridgeRole.KatieHouse,
            KentridgeRole.AwonHouse,
            KentridgeRole.AbandonedHouse,
            KentridgeRole.MedrareHouse,
            KentridgeRole.Warehouse,
            KentridgeRole.Well,
        };

        [Test]
        public void KentridgeStillPlacesEveryRecoveredBuilding()
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);

            var placed = new HashSet<int>();
            for (int i = 0; i < kentridge.Plots.Count; i++)
                placed.Add(kentridge.Plots[i].RoleId);

            var missing = new List<string>();
            for (int i = 0; i < RecoveredRoles.Length; i++)
                if (!placed.Contains((int)RecoveredRoles[i]))
                    missing.Add(RecoveredRoles[i].ToString());

            Assert.IsEmpty(
                missing,
                "Kentridge is recovered content: these buildings existed in the original game and " +
                "are not placed by the town planner: " + string.Join(", ", missing));
        }

        [Test]
        public void HightownIsAWholeTownRatherThanAHandfulOfPlots()
        {
            SettlementPlan hightown = HightownDefinition.Build(Seed);

            Assert.That(hightown.Id, Is.EqualTo(HightownDefinition.Id));
            Assert.That(hightown.Streets.Count, Is.GreaterThanOrEqualTo(4),
                "Hightown's grid needs its avenues and cross streets to read as a planned town.");
            Assert.That(hightown.Plots.Count, Is.GreaterThanOrEqualTo(8),
                "A town with a handful of buildings reads as a hamlet.");
        }

        [Test]
        public void HightownLooksDistinctFromKentridge()
        {
            ArchitectureTheme kentridge = KentridgeDefinition.Theme;
            ArchitectureTheme hightown = HightownDefinition.Theme;

            Assert.That(hightown.Id, Is.Not.EqualTo(kentridge.Id));

            // Materials carry the difference close up, storey height carries it from a distance.
            // Requiring both stops the towns diverging on paint alone.
            Assert.That(hightown.Roof, Is.Not.EqualTo(kentridge.Roof),
                "Roofs are the largest visible surface of a town seen from outside it.");
            Assert.That(hightown.Frame, Is.Not.EqualTo(kentridge.Frame));
            Assert.That(hightown.Window, Is.Not.EqualTo(kentridge.Window),
                "Lit windows against plain glass is a night-time difference between the towns.");
            Assert.That(hightown.Foundation, Is.Not.EqualTo(kentridge.Foundation),
                "The footings are visible wherever a building meets sloping ground.");

            // Vertical proportions deliberately match: the towns share plot envelopes and the
            // architecture pass rejects anything taller than the envelope it was given.
            Assert.That(hightown.FloorHeightDm, Is.EqualTo(kentridge.FloorHeightDm));
        }

        [Test]
        public void TheTwoTownsAreSeparatePlacesWithCountryBetweenThem()
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);
            SettlementPlan hightown = HightownDefinition.Build(Seed);

            int dx = hightown.CentreDm.X - kentridge.CentreDm.X;
            int dz = hightown.CentreDm.Y - kentridge.CentreDm.Y;
            double metres = System.Math.Sqrt((double)dx * dx + (double)dz * dz) * 0.1;

            // Far enough for open country, a river and a forest to sit between them; close enough
            // that the road is a journey rather than an expedition.
            Assert.That(metres, Is.GreaterThan(250.0),
                "The towns must not share a skyline or their terrain would have nowhere to go.");
            Assert.That(metres, Is.LessThan(900.0));
        }

        [Test]
        public void TheTwoTownsDoNotOverlap()
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);
            SettlementPlan hightown = HightownDefinition.Build(Seed);

            for (int i = 0; i < kentridge.Plots.Count; i++)
            for (int j = 0; j < hightown.Plots.Count; j++)
            {
                BuildingPlot a = kentridge.Plots[i];
                BuildingPlot b = hightown.Plots[j];
                bool overlaps =
                    System.Math.Abs(a.PositionDm.X - b.PositionDm.X) < 200 &&
                    System.Math.Abs(a.PositionDm.Y - b.PositionDm.Y) < 200;

                Assert.IsFalse(
                    overlaps,
                    "Kentridge plot " + a.RoleId + " and Hightown plot " + b.RoleId +
                    " are on top of each other; the towns share one coordinate space.");
            }
        }

        [Test]
        public void HightownVoxelCatalogueDoesNotEmitSouthOfTheCountryMidpoint()
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);
            SettlementPlan hightown = HightownDefinition.Build(Seed);
            int midpointZ = (kentridge.CentreDm.Y + hightown.CentreDm.Y) / 2;
            var settings = new VoxelWorldGenSettings(1, DefaultMaterials());
            FeatureCatalogue catalogue = HightownVoxelCatalogue.Build(
                hightown,
                settings,
                Allocator.Temp);

            try
            {
                var leaked = new List<string>();
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    for (int i = 0; i < rule.ExplicitCount; i++)
                    {
                        ExplicitPlacement placement = catalogue.ExplicitPlacements[
                            rule.ExplicitOffset + i];
                        if (placement.Position.z >= midpointZ) continue;
                        leaked.Add(definition.Name + "@" + placement.Position);
                    }
                }

                Assert.That(leaked, Is.Empty,
                    "A Hightown-only catalogue emitted placements on Kentridge's side of the "
                    + "country midpoint: " + string.Join("; ", leaked));
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void TheBridgeCarriesTheRoadClearOverTheRiver()
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);
            SettlementPlan hightown = HightownDefinition.Build(Seed);
            var settings = new VoxelWorldGenSettings(1, DefaultMaterials());

            RegionCorridorPlan corridor = RegionCorridorCatalogue.Plan(
                Seed, settings, kentridge.CentreDm, hightown.CentreDm);

            Assert.That(corridor.RiverBedDm, Is.LessThan(corridor.LowestBankDm),
                "The channel must be cut below the lowest ground it passes through, or the river " +
                "runs along the surface instead of through a valley.");
            Assert.That(corridor.WaterTopDm, Is.GreaterThan(corridor.RiverBedDm),
                "A river with no depth is a wet stripe.");
            Assert.That(corridor.WaterTopDm, Is.LessThan(corridor.LowestBankDm),
                "The water surface must sit below the banks or the river floods the country.");
            Assert.That(corridor.DeckUnderDm, Is.GreaterThan(corridor.HighestBankDm),
                "The deck has to clear the higher bank, not just the one it was measured from.");
            Assert.That(corridor.DeckUnderDm - corridor.WaterTopDm, Is.GreaterThan(20),
                "There must be visible headroom between the water and the underside of the bridge.");
        }

        /// <summary>
        /// The plan above is arithmetic about four integers, and arithmetic is not geometry.
        ///
        /// Every corridor feature was once discarded whole while that plan stayed green: the
        /// programs named absolute altitudes but their placements were anchored at y=0, so
        /// rasterisation's footprint check threw the river and the bridge away and left the road
        /// painted at the floor of the world. Nothing that measured the plan could see it. This
        /// measures what would actually be handed to the rasteriser.
        /// </summary>
        [Test]
        public void TheCorridorEmitsGeometryTheRasteriserWillAccept()
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);
            SettlementPlan hightown = HightownDefinition.Build(Seed);
            var settings = new VoxelWorldGenSettings(1, DefaultMaterials());

            RegionCorridorPlan corridor = RegionCorridorCatalogue.Plan(
                Seed, settings, kentridge.CentreDm, hightown.CentreDm);
            FeatureCatalogue catalogue = RegionCorridorCatalogue.Build(
                Seed, settings, kentridge.CentreDm, hightown.CentreDm, Allocator.Temp);

            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);
            try
            {
                var emitted = new Dictionary<string, int>();
                var lowestByFeature = new Dictionary<string, int>();
                var highestByFeature = new Dictionary<string, int>();

                for (int ruleId = 0; ruleId < catalogue.Rules.Length; ruleId++)
                {
                    PlacementRule rule = catalogue.Rules[ruleId];
                    int definitionId = rule.DefinitionId;
                    FeatureDefinition definition = catalogue.Definitions[definitionId];
                    string name = definition.Name.ToString();

                    Assert.That(rule.ExplicitCount, Is.GreaterThan(0),
                        name + " placed nothing, so none of it can reach the world.");

                    for (int i = 0; i < rule.ExplicitCount; i++)
                    {
                        ExplicitPlacement placement =
                            catalogue.ExplicitPlacements[rule.ExplicitOffset + i];

                        primitives.Clear();
                        anchors.Clear();
                        ParameterSet parameters = default;
                        EvaluationResult evaluation = ShapeProgram.Evaluate(
                            in catalogue, definitionId, in parameters,
                            placement.Position, placement.Orientation, Seed,
                            FeatureGeneration.InstanceSeed(
                                Seed, definitionId, placement.Position),
                            primitives, anchors);

                        Assert.That(evaluation, Is.EqualTo(EvaluationResult.Ok),
                            name + " placement " + i + " failed to evaluate.");
                        Assert.That(primitives.Length, Is.GreaterThan(0),
                            name + " placement " + i + " emitted no primitives.");

                        // The exact test FeatureGeneration fails closed on. A primitive outside
                        // its declared footprint does not clip — the whole instance is dropped.
                        int3 maxExclusive = placement.Position + definition.Footprint;
                        for (int p = 0; p < primitives.Length; p++)
                        {
                            primitives[p].Bounds(out int3 min, out int3 max);
                            Assert.That(
                                math.all(min >= placement.Position) && math.all(max < maxExclusive),
                                Is.True,
                                name + " primitive " + p + " escaped its declared footprint: min=" +
                                min + ", max=" + max + ", footprint=[" + placement.Position + ", " +
                                maxExclusive + "). The rasteriser discards the whole instance.");

                            Track(lowestByFeature, name, min.y, lowest: true);
                            Track(highestByFeature, name, max.y, lowest: false);
                        }

                        emitted.TryGetValue(name, out int running);
                        emitted[name] = running + primitives.Length;
                    }
                }

                Assert.That(emitted.Keys, Is.EquivalentTo(
                    new[] { "corridor-road", "corridor-river", "corridor-bridge" }),
                    "The corridor is road, river and bridge together; a missing one means the " +
                    "crossing no longer agrees with itself.");

                // Altitude is the part the plan could never check. Terrain along this route sits
                // near the world's base height, so geometry down at the floor of the world is the
                // signature of a program authored in absolute altitudes against a y=0 placement.
                Assert.That(lowestByFeature["corridor-road"],
                    Is.GreaterThan(corridor.RiverBedDm - 200),
                    "The road is painted at the bottom of the world rather than on the ground.");
                Assert.That(lowestByFeature["corridor-river"],
                    Is.EqualTo(corridor.RiverBedDm),
                    "The channel must start at the bed altitude the plan resolved.");
                Assert.That(lowestByFeature["corridor-bridge"],
                    Is.EqualTo(corridor.RiverBedDm),
                    "The piers must stand on the river bed, not float above it.");

                // The whole point of a bridge: its deck is above the water it crosses.
                Assert.That(highestByFeature["corridor-bridge"],
                    Is.GreaterThan(corridor.WaterTopDm),
                    "The bridge does not reach above the water surface.");
                Assert.That(lowestByFeature["corridor-river"],
                    Is.LessThan(corridor.DeckUnderDm),
                    "The river must pass under the deck, not over it.");
            }
            finally
            {
                primitives.Dispose();
                anchors.Dispose();
                catalogue.Dispose();
            }
        }

        private static void Track(
            Dictionary<string, int> into, string name, int value, bool lowest)
        {
            if (!into.TryGetValue(name, out int current))
            {
                into[name] = value;
                return;
            }

            into[name] = lowest
                ? System.Math.Min(current, value)
                : System.Math.Max(current, value);
        }

        [Test]
        public void TheCrossingSitsBetweenTheTownsRatherThanInsideOne()
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);
            SettlementPlan hightown = HightownDefinition.Build(Seed);
            var settings = new VoxelWorldGenSettings(1, DefaultMaterials());

            RegionCorridorPlan corridor = RegionCorridorCatalogue.Plan(
                Seed, settings, kentridge.CentreDm, hightown.CentreDm);

            int south = System.Math.Min(kentridge.CentreDm.Y, hightown.CentreDm.Y);
            int north = System.Math.Max(kentridge.CentreDm.Y, hightown.CentreDm.Y);

            Assert.That(corridor.CrossingZDm, Is.GreaterThan(south + 700),
                "A river through Kentridge's streets is not a river between the towns.");
            Assert.That(corridor.CrossingZDm, Is.LessThan(north - 700));
            Assert.That(corridor.RoadXDm, Is.EqualTo(kentridge.CentreDm.X),
                "The road should run on the shared axis both towns are planned around.");
        }

        /// <summary>
        /// Material ids are a property of the game's palette, not of worldgen. Any distinct set
        /// works here: these tests are about arrangement, not about which byte means stone.
        /// </summary>
        private static VoxelMaterialMap DefaultMaterials() =>
            new VoxelMaterialMap(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
    }
}
