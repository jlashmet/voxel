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
                Assert.AreEqual(plaza.SizeDm.X + 1, definition.Footprint.x,
                    "Inclusive authored plaza bounds require one voxel for both endpoints.");
                Assert.AreEqual(plaza.SizeDm.Y + 1, definition.Footprint.z,
                    "Inclusive authored plaza bounds require one voxel for both endpoints.");
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
        public void HardAndGradedPiazzaOwnTheSameInclusiveAuthoredBoundary()
        {
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            PlannedPlaza plaza = settlement.Plaza;
            int expectedMinX = plaza.CentreDm.X - plaza.SizeDm.X / 2;
            int expectedMinZ = plaza.CentreDm.Y - plaza.SizeDm.Y / 2;
            int expectedMaxX = plaza.CentreDm.X + plaza.SizeDm.X / 2;
            int expectedMaxZ = plaza.CentreDm.Y + plaza.SizeDm.Y / 2;

            FeatureCatalogue hard = KentridgeMarketPiazzaCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue graded = KentridgeVerticalTownSurfaceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int gradedIndex = -1;
                for (int i = 0; i < graded.Definitions.Length; i++)
                {
                    if (graded.Definitions[i].Name.ToString() == "kentridge-vertical-market-square")
                    {
                        gradedIndex = i;
                        break;
                    }
                }

                Assert.GreaterOrEqual(gradedIndex, 0,
                    "Expected the vertical town surface catalogue to contain Market Square.");

                FeatureDefinition hardDefinition = hard.Definitions[0];
                ExplicitPlacement hardPlacement = hard.ExplicitPlacements[0];
                FeatureDefinition gradedDefinition = graded.Definitions[gradedIndex];
                ExplicitPlacement gradedPlacement = graded.ExplicitPlacements[gradedIndex];

                Assert.AreEqual(expectedMinX, hardPlacement.Position.x);
                Assert.AreEqual(expectedMinZ, hardPlacement.Position.z);
                Assert.AreEqual(expectedMinX, gradedPlacement.Position.x);
                Assert.AreEqual(expectedMinZ, gradedPlacement.Position.z);

                Assert.AreEqual(expectedMaxX,
                    hardPlacement.Position.x + hardDefinition.Footprint.x - 1,
                    "Hard piazza must own its authored +X endpoint.");
                Assert.AreEqual(expectedMaxZ,
                    hardPlacement.Position.z + hardDefinition.Footprint.z - 1,
                    "Hard piazza must own its authored +Z endpoint; the saved scene seam is on this row.");
                Assert.AreEqual(expectedMaxX,
                    gradedPlacement.Position.x + gradedDefinition.Footprint.x - 1,
                    "Graded piazza must own the same authored +X endpoint.");
                Assert.AreEqual(expectedMaxZ,
                    gradedPlacement.Position.z + gradedDefinition.Footprint.z - 1,
                    "Graded piazza must own the same authored +Z endpoint as the hard surface.");
            }
            finally
            {
                hard.Dispose();
                graded.Dispose();
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