using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldbuildingGallerySecretDiscoveryCompatibilityTests
    {
        [Test]
        public void ReplayCompatibilityAcceptsVerticalAuthoringDriftForSameRoute()
        {
            int3 baked = new int3(280, 168, 2016);
            CaveAuthoringResult replay = Replay(new int3(280, 174, 2016));

            Assert.That(
                ShowcaseWorld.IsWorldbuildingGalleryCaveReplayCompatible(baked, in replay),
                Is.True,
                "A restored bake may carry an older vertical endpoint while the replay preserves the same planar route and main terminal semantics.");
        }

        [TestCase(281, 2016)]
        [TestCase(280, 2017)]
        public void ReplayCompatibilityRejectsPlanarRouteDrift(int x, int z)
        {
            int3 baked = new int3(280, 168, 2016);
            CaveAuthoringResult replay = Replay(new int3(x, 174, z));

            Assert.That(
                ShowcaseWorld.IsWorldbuildingGalleryCaveReplayCompatible(baked, in replay),
                Is.False,
                "Horizontal endpoint drift changes route identity and must not be hidden as bake compatibility.");
        }

        [Test]
        public void ReplayCompatibilityRejectsMissingMainTraversalTerminal()
        {
            int3 baked = new int3(280, 168, 2016);
            CaveAuthoringResult replay = Replay(new int3(280, 174, 2016));
            CaveTraversalCandidate branch = replay.TraversalCandidates.Items[0];
            branch.BranchDepth = 1;
            branch.Flags = CaveTraversalFlags.ReachableFromEntrance |
                           CaveTraversalFlags.Branch |
                           CaveTraversalFlags.Terminal;
            replay.TraversalCandidates.Items[0] = branch;

            Assert.That(
                ShowcaseWorld.IsWorldbuildingGalleryCaveReplayCompatible(baked, in replay),
                Is.False,
                "Matching coordinates alone are insufficient; the replay must still prove a reachable main-path terminal.");
        }

        private static CaveAuthoringResult Replay(int3 endpoint)
        {
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = endpoint,
                TraversalDistance = 112,
                BranchDepth = 0,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.MainPath |
                        CaveTraversalFlags.Terminal,
                ExitFacing = Facing.North,
            });

            return new CaveAuthoringResult
            {
                MainPathEnd = endpoint,
                MainPathTraversalDistance = 112,
                TraversalCandidates = candidates,
            };
        }
    }
}
