using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CaveTraversalDecorationBridgeTests
    {
        [Test]
        public void SelectedBranchTerminalFeedsDecorationPatchWithoutReconstructingFacing()
        {
            CaveTraversalFlags terminalFlags = CaveTraversalFlags.ReachableFromEntrance |
                                               CaveTraversalFlags.Terminal;
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(0, -2, 200),
                TraversalDistance = 24,
                BranchDepth = 0,
                Flags = terminalFlags | CaveTraversalFlags.MainPath,
                ExitFacing = Facing.North,
            });
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(3, -8, 5),
                TraversalDistance = 72,
                BranchDepth = 1,
                Flags = terminalFlags | CaveTraversalFlags.Branch,
                ExitFacing = Facing.West,
            });

            CavePlacementRequirements requirements =
                CavePlacementRequirements.AnyReachableTerminal(24);
            CavePlacementPreferences preferences =
                CavePlacementPreferences.PreferBranchTerminal;
            Assert.IsTrue(CavePlacementResolver.TrySelectBest(
                in candidates, in requirements, in preferences, out CaveTraversalCandidate selected));

            CaveConfig config = SpaciousConfig();
            Assert.IsTrue(CaveTraversalDecorationBridge.TryCreatePatch(
                0x123456789ABCDEF0ul, in selected, in config, out CaveWalkablePatch patch));
            Assert.IsTrue(CaveDecorationSpaceAdapter.TryCreate(
                in patch,
                out DecorationSpace space,
                out DecorationContext context,
                out CaveDecorationCandidate[] decorationCandidates,
                out DecorationExclusion[] exclusions));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(new int3(3, -8, 5), selected.Position,
                    "Traversal selection must beat misleading world-space distance.");
                Assert.AreEqual(Facing.West, selected.ExitFacing);
                Assert.AreEqual(selected.Position, patch.End);
                Assert.AreEqual(selected.ExitFacing, patch.Facing);
                Assert.IsTrue(patch.IsWellFormed);
                Assert.IsTrue(space.IsWellFormed);
                Assert.IsTrue(context.IsWellFormed);
                Assert.AreEqual(9, decorationCandidates.Length);
                Assert.AreEqual(2, exclusions.Length);
            });

            CaveTraversalCandidate differentlyOriented = selected;
            differentlyOriented.ExitFacing = Facing.East;
            Assert.IsTrue(CaveTraversalDecorationBridge.TryCreatePatch(
                0x123456789ABCDEF0ul, in differentlyOriented, in config,
                out CaveWalkablePatch differentlyOrientedPatch));
            Assert.AreNotEqual(patch.PatchId, differentlyOrientedPatch.PatchId,
                "Distinct authored terminal orientations must not alias one decoration space identity.");
        }

        private static CaveConfig SpaciousConfig()
        {
            CaveConfig config = CaveConfig.Default;
            config.TunnelWidth = 48;
            config.TunnelHeight = 34;
            config.SegmentLength = 40;
            config.WallRoughness = 1;
            config.FloorRoughness = 1;
            config.CeilingRoughness = 2;
            config.MinChamberRadius = 12;
            config.MaxChamberRadius = 30;
            config.MinChamberHeight = 14;
            config.MaxChamberHeight = 30;
            config.BoundsHalfExtents = new int3(320, 120, 320);
            return config;
        }
    }
}
