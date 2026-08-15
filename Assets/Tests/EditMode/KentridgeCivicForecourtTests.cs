using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeCivicForecourtTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void CivicForecourtIsFramedByStableChurchAndMayorAnchors()
        {
            KentridgeCivicForecourtPlan plan = KentridgeCivicForecourtPlanner.Build(Seed);

            Assert.AreEqual(1118, plan.MinXDm);
            Assert.AreEqual(1222, plan.MaxXDm);
            Assert.AreEqual(104, plan.WidthDm);
            Assert.AreEqual(106, plan.MinZDm);
            Assert.AreEqual(194, plan.MaxZDm);
            Assert.AreEqual(88, plan.DepthDm);
            Assert.AreEqual(KentridgeTownPlanner.MainSpineXDm, plan.CentreDm.X);
            Assert.AreEqual(150, plan.CentreDm.Y);
            Assert.Greater(plan.WidthDm, KentridgeTownPlanner.MainRoadWidthDm,
                "Civic room should preserve pedestrian apron beyond the procession carriageway.");
        }

        [Test]
        public void CivicForecourtCatalogueStaysFlushAndBelowLaterCivicArchitecture()
        {
            KentridgeCivicForecourtPlan plan = KentridgeCivicForecourtPlanner.Build(Seed);
            FeatureCatalogue catalogue = KentridgeCivicForecourtCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(1, catalogue.Definitions.Length);
                Assert.AreEqual(1, catalogue.ExplicitPlacements.Length);
                FeatureDefinition definition = catalogue.Definitions[0];
                ExplicitPlacement placement = catalogue.ExplicitPlacements[0];

                Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                Assert.AreEqual(KentridgeCivicForecourtCatalogue.ForecourtPrecedence,
                    definition.Precedence);
                Assert.AreEqual(plan.WidthDm, definition.Footprint.x,
                    "Test settings use one voxel per decimetre.");
                Assert.AreEqual(plan.DepthDm, definition.Footprint.z);
                Assert.AreEqual(KentridgeCivicForecourtCatalogue.SurfaceThicknessDm,
                    definition.Footprint.y);
                Assert.AreEqual(plan.MinXDm, placement.Position.x);
                Assert.AreEqual(plan.MinZDm, placement.Position.z);

                int surfaceY = KentridgeVerticalProfile.SurfaceYAtDm(
                    plan.CentreDm.X,
                    plan.CentreDm.Y,
                    Seed,
                    1);
                Assert.AreEqual(surfaceY,
                    placement.Position.y + definition.Footprint.y,
                    "Formal summit court must remain flush with the authored Civic Crown shelf.");

                Assert.Greater(KentridgeCivicForecourtCatalogue.ForecourtPrecedence, 61,
                    "Civic summit should outrank ordinary piazza/public-surface paint.");
                Assert.Less(KentridgeCivicForecourtCatalogue.ForecourtPrecedence, 80,
                    "Street/civic dressing must remain free to occupy the formal court.");
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
