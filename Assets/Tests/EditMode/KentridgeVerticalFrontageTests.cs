using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeVerticalFrontageTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void DenseUrbanBlocksPromoteCourtEdgesIntoOccupiedVerticalFrontages()
        {
            KentridgeVerticalFrontagePlan plan = KentridgeVerticalFrontagePlanner.Build(Seed);
            Assert.AreEqual(6, plan.Zones.Count,
                "Every dense block above the lower ward should own one inhabited downhill face.");

            int civic = 0;
            for (int i = 0; i < plan.Zones.Count; i++)
            {
                KentridgeVerticalFrontageZone zone = plan.Zones[i];
                Assert.AreNotEqual(KentridgeUrbanBand.LowerWard, zone.Band);
                Assert.Greater(zone.LengthDm, zone.GapWidthDm);
                Assert.Greater(zone.HeightDm, 40);
                Assert.Greater(zone.DepthDm, 0);
                Assert.Greater(zone.BayPitchDm, 0);
                if (zone.Band == KentridgeUrbanBand.CivicCrown) civic++;
            }

            Assert.AreEqual(2, civic,
                "Both sides of the civic climb should be built into the summit terrace.");
        }

        [Test]
        public void VerticalFrontagesRemainInfrastructureBelowUpperBuildingsAndAccess()
        {
            FeatureCatalogue catalogue = KentridgeVerticalFrontageCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(6, catalogue.Definitions.Length);
                Assert.AreEqual(6, catalogue.ExplicitPlacements.Length);
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    Assert.AreEqual(FeatureKind.Infrastructure, catalogue.Definitions[i].Kind);
                    Assert.AreEqual(85, catalogue.Definitions[i].Precedence);
                    Assert.Greater(catalogue.Definitions[i].Footprint.x, 0);
                    Assert.Greater(catalogue.Definitions[i].Footprint.y, 0);
                    Assert.Greater(catalogue.Definitions[i].Footprint.z, 0);
                }
            }
            finally
            {
                catalogue.Dispose();
            }
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
