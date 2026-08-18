using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CavePlacementTests
    {
        [Test]
        public void BossLikeRequirementsRejectEntranceAdjacentCandidateAndSelectDeepMainTerminal()
        {
            var candidates = new CaveTraversalCandidateSet();
            CaveTraversalFlags mainTerminal = CaveTraversalFlags.ReachableFromEntrance |
                                              CaveTraversalFlags.MainPath |
                                              CaveTraversalFlags.Terminal;
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(0, 0, 6),
                TraversalDistance = 6,
                BranchDepth = 0,
                Flags = mainTerminal,
            });
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(4, -2, 12),
                TraversalDistance = 48,
                BranchDepth = 0,
                Flags = mainTerminal,
            });
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = new int3(2, -2, 10),
                TraversalDistance = 72,
                BranchDepth = 1,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.Branch |
                        CaveTraversalFlags.Terminal,
            });

            var requirements = new CavePlacementRequirements
            {
                MinTraversalDistance = 24,
                MaxTraversalDistance = -1,
                MinBranchDepth = 0,
                MaxBranchDepth = 0,
                RequiredFlags = mainTerminal,
                ForbiddenFlags = CaveTraversalFlags.Branch,
            };
            CaveTraversalCandidate near = candidates.Items[0];
            CaveTraversalCandidate deep = candidates.Items[1];
            CaveTraversalCandidate branch = candidates.Items[2];

            Assert.Multiple(() =>
            {
                Assert.IsFalse(requirements.Matches(in near));
                Assert.IsTrue(requirements.Matches(in deep));
                Assert.IsFalse(requirements.Matches(in branch));
                Assert.IsTrue(CavePlacementResolver.TrySelectDeepest(
                    in candidates, in requirements, out CaveTraversalCandidate selected));
                Assert.AreEqual(48, selected.TraversalDistance);
                Assert.AreEqual(new int3(4, -2, 12), selected.Position);
            });
        }

        [Test]
        public void GeneratedBranchTerminalInheritsCumulativeTraversalDistance()
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

            Assert.AreEqual(2, result.TraversalCandidates.Count);
            CaveTraversalCandidate main = Find(result.TraversalCandidates, CaveTraversalFlags.MainPath);
            CaveTraversalCandidate branch = Find(result.TraversalCandidates, CaveTraversalFlags.Branch);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(config.MainSegmentCount * config.SegmentLength,
                    result.MainPathTraversalDistance);
                Assert.AreEqual(result.MainPathTraversalDistance, main.TraversalDistance);
                Assert.AreEqual(0, main.BranchDepth);
                Assert.AreEqual(1, branch.BranchDepth);
                Assert.AreEqual(config.SegmentLength * (1 + config.BranchSegmentCount),
                    branch.TraversalDistance,
                    "Branch distance must inherit the fork's distance from the entrance.");
                Assert.That(branch.Flags & CaveTraversalFlags.ReachableFromEntrance,
                    Is.EqualTo(CaveTraversalFlags.ReachableFromEntrance));
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
