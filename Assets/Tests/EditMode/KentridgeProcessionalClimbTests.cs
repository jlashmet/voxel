using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeProcessionalClimbTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UpperTownAlternatesRisesAndFlatPublicRooms()
        {
            KentridgeProcessionalClimbPlan plan = KentridgeProcessionalClimb.Build(Seed);
            Assert.AreEqual(6, plan.Segments.Count);

            Assert.AreEqual(KentridgeProcessionalSegmentKind.Rise, plan.Segments[0].Kind);
            Assert.AreEqual(KentridgeProcessionalSegmentKind.Landing, plan.Segments[1].Kind);
            Assert.AreEqual(KentridgeProcessionalSegmentKind.Rise, plan.Segments[2].Kind);
            Assert.AreEqual(KentridgeProcessionalSegmentKind.Landing, plan.Segments[3].Kind);
            Assert.AreEqual(KentridgeProcessionalSegmentKind.Rise, plan.Segments[4].Kind);
            Assert.AreEqual(KentridgeProcessionalSegmentKind.Rise, plan.Segments[5].Kind);

            Assert.AreEqual(96, plan.Segments[1].WidthDm,
                "Upper Landing width should come from its skeleton open-space reservation.");
            Assert.AreEqual(100, plan.Segments[3].WidthDm,
                "Civic Gate width should come from its skeleton open-space reservation.");
            Assert.AreEqual(0, plan.Segments[1].RiseDm);
            Assert.AreEqual(0, plan.Segments[3].RiseDm);
        }

        [Test]
        public void SharedVerticalProfileIsActuallyFlatAtSemanticLandings()
        {
            int x = KentridgeTownPlanner.MainSpineXDm;

            Assert.AreEqual(
                KentridgeVerticalProfile.UpperLandingOffsetDm,
                KentridgeVerticalProfile.SurfaceOffsetDm(x, 340));
            Assert.AreEqual(
                KentridgeVerticalProfile.UpperLandingOffsetDm,
                KentridgeVerticalProfile.SurfaceOffsetDm(x, 325));
            Assert.AreEqual(
                KentridgeVerticalProfile.UpperLandingOffsetDm,
                KentridgeVerticalProfile.SurfaceOffsetDm(x, 365));

            Assert.AreEqual(
                KentridgeVerticalProfile.CivicGateOffsetDm,
                KentridgeVerticalProfile.SurfaceOffsetDm(x, 260));
            Assert.AreEqual(
                KentridgeVerticalProfile.CivicGateOffsetDm,
                KentridgeVerticalProfile.SurfaceOffsetDm(x, 245));
            Assert.AreEqual(
                KentridgeVerticalProfile.CivicGateOffsetDm,
                KentridgeVerticalProfile.SurfaceOffsetDm(x, 275));
        }

        [Test]
        public void ProcessionalSurfaceOverridesGenericRoadWithoutCreatingGameplayStructures()
        {
            FeatureCatalogue catalogue = KentridgeProcessionalClimbCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(6, catalogue.Definitions.Length);
                Assert.AreEqual(6, catalogue.ExplicitPlacements.Length);

                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FeatureDefinition definition = catalogue.Definitions[i];
                    Assert.AreEqual(FeatureKind.Landform, definition.Kind);
                    Assert.AreEqual(28, definition.Precedence);
                    StringAssert.StartsWith("kentridge-procession-", definition.Name.ToString());
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
