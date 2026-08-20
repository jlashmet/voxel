using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveTraversalFacingTests
    {
        [Test]
        public void GeneratedTerminalsPreserveFinalAuthoredFacing()
        {
            CaveConfig config = CaveConfig.Default;
            config.SegmentLength = 6;
            config.MainSegmentCount = 6;
            config.TurnChancePercent = 0;
            config.VerticalChancePercent = 0;
            config.ChamberChancePercent = 0;
            config.BranchChancePercent = 100;
            config.MaxBranches = 1;
            config.MaxBranchDepth = 1;
            config.BranchSegmentCount = 2;
            config.MinBranchSeparation = 0;
            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0xABCDEFul, int3.zero, Facing.East, 7, 9, 4);
            CaveMaterialPalette palette = new CaveMaterialPalette { Opening = 0, Rock = 2 };

            CaveAuthoringResult result = CaveAuthoring.Author(
                new NoopSession(), in request, in config, in palette);

            CaveTraversalCandidate main = Find(result.TraversalCandidates, CaveTraversalFlags.MainPath);
            CaveTraversalCandidate branch = Find(result.TraversalCandidates, CaveTraversalFlags.Branch);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(main.IsWellFormed);
                Assert.IsTrue(branch.IsWellFormed);
                Assert.AreEqual(Facing.East, main.ExitFacing,
                    "A no-turn main path must retain the entrance-facing direction at its terminal.");
                Assert.IsTrue(branch.ExitFacing == Facing.North || branch.ExitFacing == Facing.South,
                    "A branch spawned from an eastbound path must retain its authored quarter-turn direction.");
            });
        }

        [Test]
        public void EqualCandidatesUseExitFacingAsDeterministicTieBreak()
        {
            CaveTraversalFlags flags = CaveTraversalFlags.ReachableFromEntrance |
                                       CaveTraversalFlags.Branch |
                                       CaveTraversalFlags.Terminal;
            var east = new CaveTraversalCandidate
            {
                Position = new int3(4, -2, 12),
                TraversalDistance = 48,
                BranchDepth = 1,
                Flags = flags,
                ExitFacing = Facing.East,
            };
            var west = east;
            west.ExitFacing = Facing.West;

            var forward = new CaveTraversalCandidateSet();
            forward.Items.Add(west);
            forward.Items.Add(east);
            var reverse = new CaveTraversalCandidateSet();
            reverse.Items.Add(east);
            reverse.Items.Add(west);
            CavePlacementRequirements requirements = CavePlacementRequirements.AnyReachableTerminal();
            Facing expectedFacing = (int)Facing.East < (int)Facing.West ? Facing.East : Facing.West;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(CavePlacementResolver.TrySelectDeepest(
                    in forward, in requirements, out CaveTraversalCandidate selectedForward));
                Assert.IsTrue(CavePlacementResolver.TrySelectDeepest(
                    in reverse, in requirements, out CaveTraversalCandidate selectedReverse));
                Assert.AreEqual(expectedFacing, selectedForward.ExitFacing);
                Assert.AreEqual(expectedFacing, selectedReverse.ExitFacing);
            });
        }

        private static CaveTraversalCandidate Find(
            in CaveTraversalCandidateSet candidates,
            CaveTraversalFlags requiredFlag)
        {
            for (int i = 0; i < candidates.Items.Length; i++)
            {
                CaveTraversalCandidate candidate = candidates.Items[i];
                if ((candidate.Flags & requiredFlag) != 0)
                    return candidate;
            }
            Assert.Fail($"No cave traversal candidate carried {requiredFlag}.");
            return default;
        }

        private sealed class NoopSession : IStructureAuthoringSession
        {
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(
                int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None,
                VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
