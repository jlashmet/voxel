using NUnit.Framework;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tiering.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WorldbuildingGalleryStructuralCompositionPlayModeTests
    {
        private const uint Seed = 0x5EED1234u;
        private const int FinalRefinementBrickPoolCapacity = 262144;

        [Test]
        public void TypedStructuralProofCataloguesPlanDeterministicallyAndRejectInvalidAttachments()
        {
            using var first = new ShowcaseWorld(Seed, 4096, 1, 2);
            using var second = new ShowcaseWorld(Seed, 4096, 1, 2);

            Assert.AreEqual(4, ShowcaseWorld.WorldbuildingGalleryStructuralProofCaseCount);
            for (int proof = 0; proof < ShowcaseWorld.WorldbuildingGalleryStructuralProofCaseCount; proof++)
            {
                ShowcaseWorld.GalleryStructuralProofMetrics firstMetrics =
                    first.WorldbuildingGalleryStructuralProofMetrics(proof);
                ShowcaseWorld.GalleryStructuralProofMetrics secondMetrics =
                    second.WorldbuildingGalleryStructuralProofMetrics(proof);

                Assert.AreEqual(StructuralCompositionResult.Ok, firstMetrics.Result,
                    $"proof {proof} ({firstMetrics.Name}) should compose through the production planner");
                Assert.Greater(firstMetrics.ChildCount, 0, $"proof {proof} should attach child pieces");
                Assert.Greater(firstMetrics.PrimitiveCost, 0, $"proof {proof} should carry primitive cost");
                Assert.Greater(firstMetrics.VoxelCost, 0, $"proof {proof} should carry voxel cost");
                Assert.AreNotEqual(0UL, firstMetrics.GraphHash, $"proof {proof} should expose graph identity");

                Assert.AreEqual(firstMetrics.Result, secondMetrics.Result, $"proof {proof} result");
                Assert.AreEqual(firstMetrics.ChildCount, secondMetrics.ChildCount, $"proof {proof} children");
                Assert.AreEqual(firstMetrics.PrimitiveCost, secondMetrics.PrimitiveCost, $"proof {proof} primitives");
                Assert.AreEqual(firstMetrics.VoxelCost, secondMetrics.VoxelCost, $"proof {proof} voxel cost");
                Assert.AreEqual(firstMetrics.BoundsMin, secondMetrics.BoundsMin, $"proof {proof} min bounds");
                Assert.AreEqual(firstMetrics.BoundsMax, secondMetrics.BoundsMax, $"proof {proof} max bounds");
                Assert.AreEqual(firstMetrics.GraphHash, secondMetrics.GraphHash, $"proof {proof} graph hash");
            }

            Assert.Greater(first.WorldbuildingGalleryStructuralBridgeTerrainRelief, 0,
                "bridge proving site must span real terrain relief");
            Assert.AreEqual(StructuralAttachmentRejectReason.OrientationMismatch,
                first.AuditWorldbuildingGalleryStructuralBridgeOrientationReject());
            Assert.AreEqual(StructuralAttachmentRejectReason.IncompatibleRoleOrTags,
                first.AuditWorldbuildingGalleryStructuralCastleSemanticReject());
            Assert.AreEqual(StructuralAttachmentRejectReason.IncompatibleRoleOrTags,
                first.WorldbuildingGalleryStructuralBridgeNegativeReject);
            Assert.AreEqual(StructuralAttachmentRejectReason.MissingTerrainSupport,
                first.WorldbuildingGalleryStructuralCliffNegativeReject);
        }

        [Test]
        public void RefinedCastleGatePreservesCanonicalCharacterMotorTraversal()
        {
            // This regression authors the entire final presentation/refinement just like the gallery.
            // Mirror the gallery's device-tier budget path as well as its requested capacity. Passing
            // only the capacity hits VoxelEngineBootstrap's conservative 256 MB unknown-device
            // fallback and clamps this production-sized fixture to 127100 bricks on the CI runner.
            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                FinalRefinementBrickPoolCapacity,
                tierBytes);
            using var world = new ShowcaseWorld(Seed, capacity, 1, 2, tierBytes);
            world.EnsureWorldbuildingGalleryStructuralRefinementBlocking();

            ShowcaseWorld.GalleryStructuralTraversalReport traversal =
                world.AuditWorldbuildingGalleryStructuralTraversal(1);

            Assert.IsTrue(traversal.Reached,
                $"refined gate must preserve its canonical traversal; steps={traversal.Steps} " +
                $"start={traversal.StartDistanceMetres:0.###}m end={traversal.EndDistanceMetres:0.###}m " +
                $"feet={traversal.FinalFeetPosition}");
            Assert.LessOrEqual(traversal.EndDistanceMetres, 1.35f,
                "refined gate traversal must retain the production motor acceptance distance");
        }
    }
}
