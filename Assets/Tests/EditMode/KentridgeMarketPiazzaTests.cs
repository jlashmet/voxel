using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeMarketPiazzaTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void HardPiazzaUsesExistingSemanticMarketSquareWithoutChangingStreetTopology()
        {
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            PlannedPlaza plaza = settlement.Plaza;
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.X, plaza.CentreDm.X);
            Assert.AreEqual(KentridgeDefinition.TownCentreDm.Y, plaza.CentreDm.Y);
            Assert.AreEqual(220, plaza.SizeDm.X);
            Assert.AreEqual(140, plaza.SizeDm.Y);
            Assert.AreEqual(4, settlement.Streets.Count);

            FeatureCatalogue catalogue = KentridgeMarketPiazzaCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(1, catalogue.Definitions.Length);
                Assert.AreEqual(1, catalogue.ExplicitPlacements.Length);
                FeatureDefinition definition = catalogue.Definitions[0];
                ExplicitPlacement placement = catalogue.ExplicitPlacements[0];

                Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                Assert.AreEqual(KentridgeMarketPiazzaCatalogue.PiazzaPrecedence,
                    definition.Precedence);
                Assert.AreEqual(plaza.SizeDm.X, definition.Footprint.x,
                    "Test settings use one voxel per decimetre.");
                Assert.AreEqual(plaza.SizeDm.Y, definition.Footprint.z);
                Assert.AreEqual(KentridgeMarketPiazzaCatalogue.SurfaceThicknessDm,
                    definition.Footprint.y);
                Assert.AreEqual(plaza.CentreDm.X - plaza.SizeDm.X / 2,
                    placement.Position.x);
                Assert.AreEqual(plaza.CentreDm.Y - plaza.SizeDm.Y / 2,
                    placement.Position.z);

                int surfaceY = KentridgeVerticalProfile.SurfaceYAtDm(
                    plaza.CentreDm.X,
                    plaza.CentreDm.Y,
                    Seed,
                    1);
                Assert.AreEqual(surfaceY,
                    placement.Position.y + definition.Footprint.y,
                    "The hard piazza must stay flush with the existing graded Market Square.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void PiazzaRemainsAThinSharedSpaceBelowStreetDressingAndGameplayArchitecture()
        {
            Assert.AreEqual(5, KentridgeMarketPiazzaCatalogue.BorderWidthDm);
            Assert.AreEqual(2, KentridgeMarketPiazzaCatalogue.SurfaceThicknessDm);
            Assert.Greater(KentridgeMarketPiazzaCatalogue.PiazzaPrecedence, 60,
                "Piazza should visually unify the Market Square above ordinary frontage paint.");
            Assert.Less(KentridgeMarketPiazzaCatalogue.PiazzaPrecedence, 80,
                "Street dressing and later architecture must remain free to occupy the shared space.");
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
