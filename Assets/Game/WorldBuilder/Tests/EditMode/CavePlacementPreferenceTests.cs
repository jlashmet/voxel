using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CavePlacementPreferenceTests
    {
        [Test]
        public void BranchPreferenceDoesNotOverrideHardTraversalDistance()
        {
            CaveTraversalFlags terminal = CaveTraversalFlags.ReachableFromEntrance |
                                          CaveTraversalFlags.Terminal;
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(2, -2, 4),
                TraversalDistance = 12,
                BranchDepth = 1,
                Flags = terminal | CaveTraversalFlags.Branch,
            });
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(0, -2, 60),
                TraversalDistance = 60,
                BranchDepth = 0,
                Flags = terminal | CaveTraversalFlags.MainPath,
            });
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(3, -3, 18),
                TraversalDistance = 36,
                BranchDepth = 1,
                Flags = terminal | CaveTraversalFlags.Branch,
            });

            CavePlacementRequirements requirements =
                CavePlacementRequirements.AnyReachableTerminal(24);
            CavePlacementPreferences preferences =
                CavePlacementPreferences.PreferBranchTerminal;

            Assert.IsTrue(CavePlacementResolver.TrySelectBest(
                in candidates, in requirements, in preferences, out CaveTraversalCandidate selected));
            Assert.Multiple(() =>
            {
                Assert.AreEqual(new int3(3, -3, 18), selected.Position,
                    "The shallow preferred branch must remain excluded by the hard minimum distance.");
                Assert.AreEqual(36, selected.TraversalDistance);
                Assert.That(selected.Flags & CaveTraversalFlags.Branch,
                    Is.EqualTo(CaveTraversalFlags.Branch));
            });
        }

        [Test]
        public void MissingPreferredBranchFallsBackToDeepestHardValidCandidate()
        {
            CaveTraversalFlags mainTerminal = CaveTraversalFlags.ReachableFromEntrance |
                                              CaveTraversalFlags.MainPath |
                                              CaveTraversalFlags.Terminal;
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(0, -2, 24),
                TraversalDistance = 24,
                BranchDepth = 0,
                Flags = mainTerminal,
            });
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(0, -4, 48),
                TraversalDistance = 48,
                BranchDepth = 0,
                Flags = mainTerminal,
            });

            CavePlacementRequirements requirements =
                CavePlacementRequirements.AnyReachableTerminal(12);
            CavePlacementPreferences preferences =
                CavePlacementPreferences.PreferBranchTerminal;

            Assert.IsTrue(CavePlacementResolver.TrySelectBest(
                in candidates, in requirements, in preferences, out CaveTraversalCandidate selected));
            Assert.AreEqual(new int3(0, -4, 48), selected.Position);
        }

        [Test]
        public void HookPlannerAppliesBranchPreferenceAfterHardFiltering()
        {
            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0xFACEB00Cul, int3.zero, Facing.North, 7, 9, 4);
            CaveTraversalFlags terminal = CaveTraversalFlags.ReachableFromEntrance |
                                          CaveTraversalFlags.Terminal;
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(0, -2, 50),
                TraversalDistance = 50,
                BranchDepth = 0,
                Flags = terminal | CaveTraversalFlags.MainPath,
            });
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(4, -3, 20),
                TraversalDistance = 32,
                BranchDepth = 1,
                Flags = terminal | CaveTraversalFlags.Branch,
            });

            CavePlacementRequirements requirements =
                CavePlacementRequirements.AnyReachableTerminal(24);
            CavePlacementPreferences preferences =
                CavePlacementPreferences.PreferBranchTerminal;

            Assert.IsTrue(CaveHookPlanner.TryAtBestCandidate(
                in request, in candidates, in requirements, in preferences, out CaveHookSet hooks));
            Assert.AreEqual(3, hooks.Count);
            for (int i = 0; i < hooks.Items.Length; i++)
                Assert.AreEqual(new int3(4, -3, 20), hooks.Items[i].Position);
        }
    }
}
