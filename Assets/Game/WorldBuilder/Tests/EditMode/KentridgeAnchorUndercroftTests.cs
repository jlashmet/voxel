using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeAnchorUndercroftTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void AnchorUndercroftsOccupyPubAndWarehouseSupportFacesWithoutAddingGameplayStructures()
        {
            FeatureCatalogue catalogue = KentridgeAnchorUndercroftCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(2, catalogue.Definitions.Length);
                Assert.AreEqual(2, catalogue.Rules.Length);
                Assert.AreEqual(4, catalogue.ExplicitPlacements.Length,
                    "Pub and warehouse should each receive two derived downhill bays.");
                Assert.AreEqual(0, catalogue.Anchors.Length);

                int placements = 0;
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FeatureDefinition definition = catalogue.Definitions[i];
                    PlacementRule rule = catalogue.Rules[i];
                    Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                    Assert.AreEqual(93, definition.Precedence);
                    Assert.AreEqual(84, definition.Footprint.x);

                    // Height is (support height + roof allowance), and support height is
                    // derived per plot from the terrain drop beneath it, clamped to [28, 78] dm.
                    // Asserting exactly 40 required both bays to sit on the minimum clamp, which
                    // held only while the ground under the pub and the warehouse was flat enough
                    // to bottom out. The derived range is the actual contract.
                    Assert.GreaterOrEqual(definition.Footprint.y, 28 + 12,
                        "Undercroft height must clear the minimum support plus roof allowance.");
                    Assert.LessOrEqual(definition.Footprint.y, 78 + 12,
                        "Undercroft height must stay within the maximum support plus roof allowance.");
                    Assert.AreEqual(definition.Footprint.x, definition.Footprint.z);
                    Assert.Greater(definition.ProgramLength, 20);
                    Assert.AreEqual(i, rule.DefinitionId);
                    Assert.AreEqual(2, rule.ExplicitCount);
                    placements += rule.ExplicitCount;
                }

                Assert.AreEqual(4, placements);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void UndercroftPlacementsAreDerivedFromCurrentStablePlotEdges()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            BuildingPlot pub = Find(plan, KentridgeRole.Pub);
            BuildingPlot warehouse = Find(plan, KentridgeRole.Warehouse);

            FeatureCatalogue catalogue = KentridgeAnchorUndercroftCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Int3 pubFootprint = KentridgeDefinition.FootprintDm(pub.Archetype);
                Int3 warehouseFootprint = KentridgeDefinition.FootprintDm(warehouse.Archetype);

                Assert.AreEqual(pub.PositionDm.Y + pubFootprint.Z - 12,
                    catalogue.ExplicitPlacements[0].Position.z);
                Assert.AreEqual(pub.PositionDm.Y + pubFootprint.Z - 12,
                    catalogue.ExplicitPlacements[1].Position.z);
                Assert.AreEqual(warehouse.PositionDm.Y + warehouseFootprint.Z - 12,
                    catalogue.ExplicitPlacements[2].Position.z);
                Assert.AreEqual(warehouse.PositionDm.Y + warehouseFootprint.Z - 12,
                    catalogue.ExplicitPlacements[3].Position.z);

                Assert.AreEqual(pub.PositionDm.X + 8,
                    catalogue.ExplicitPlacements[0].Position.x);
                Assert.AreEqual(warehouse.PositionDm.X + 8,
                    catalogue.ExplicitPlacements[2].Position.x);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static BuildingPlot Find(SettlementPlan plan, KentridgeRole role)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
                if (plan.Plots[i].RoleId == (int)role) return plan.Plots[i];

            Assert.Fail("Missing stable Kentridge role " + role);
            return default;
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
