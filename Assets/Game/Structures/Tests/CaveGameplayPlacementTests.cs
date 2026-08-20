using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CaveGameplayPlacementTests
    {
        [Test]
        public void BossRejectsEntranceAdjacentTerminalAndUsesTraversalDistance()
        {
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(Main(new int3(0, 0, 80), 8));
            candidates.Items.Add(Main(new int3(1, -3, 2), 60));
            candidates.Items.Add(Branch(new int3(3, -4, 4), 90));

            Assert.IsTrue(CaveGameplayPlacementPlanner.TrySelectBoss(
                in candidates, 24, out CaveGameplayPlacement placement));
            Assert.Multiple(() =>
            {
                Assert.AreEqual(CaveGameplayPlacementKind.Boss, placement.Kind);
                Assert.AreEqual(new int3(1, -3, 2), placement.Terminal.Position,
                    "Boss placement must use authored traversal distance rather than world-space distance.");
                Assert.AreEqual(60, placement.Terminal.TraversalDistance);
                Assert.AreEqual(0, placement.Terminal.BranchDepth);
            });
        }

        [Test]
        public void TreasurePrefersBranchButFallsBackToDeepestHardValidTerminal()
        {
            var withBranch = new CaveTraversalCandidateSet();
            withBranch.Items.Add(Main(new int3(0, 0, 120), 120));
            withBranch.Items.Add(Branch(new int3(4, -2, 12), 48));

            Assert.IsTrue(CaveGameplayPlacementPlanner.TrySelectTreasure(
                in withBranch, 24, out CaveGameplayPlacement preferred));
            Assert.Multiple(() =>
            {
                Assert.AreEqual(CaveGameplayPlacementKind.Treasure, preferred.Kind);
                Assert.AreEqual(new int3(4, -2, 12), preferred.Terminal.Position);
                Assert.That(preferred.Terminal.Flags & CaveTraversalFlags.Branch,
                    Is.EqualTo(CaveTraversalFlags.Branch));
            });

            var noBranch = new CaveTraversalCandidateSet();
            noBranch.Items.Add(Main(new int3(10, -2, 30), 40));
            noBranch.Items.Add(Main(new int3(-5, -2, 60), 80));
            Assert.IsTrue(CaveGameplayPlacementPlanner.TrySelectTreasure(
                in noBranch, 24, out CaveGameplayPlacement fallback));
            Assert.AreEqual(80, fallback.Terminal.TraversalDistance,
                "A soft branch preference must not make treasure placement fail when no branch exists.");
        }

        [Test]
        public void TreasureSelectionIsIndependentOfCandidateEnumerationOrder()
        {
            CaveTraversalCandidate left = Branch(new int3(-4, -2, 20), 64);
            CaveTraversalCandidate right = Branch(new int3(5, -2, 20), 64);
            var forward = new CaveTraversalCandidateSet();
            forward.Items.Add(right);
            forward.Items.Add(left);
            var reverse = new CaveTraversalCandidateSet();
            reverse.Items.Add(left);
            reverse.Items.Add(right);

            Assert.IsTrue(CaveGameplayPlacementPlanner.TrySelectTreasure(
                in forward, 24, out CaveGameplayPlacement a));
            Assert.IsTrue(CaveGameplayPlacementPlanner.TrySelectTreasure(
                in reverse, 24, out CaveGameplayPlacement b));
            Assert.AreEqual(left.Position, a.Terminal.Position);
            Assert.AreEqual(left.Position, b.Terminal.Position);
        }

        [Test]
        public void DestructibleTreasurePocketRetainsBarrierAndCarvesOnlyHiddenVolumes()
        {
            var world = new SolidWorldSession();
            CaveTraversalCandidate terminal = Branch(new int3(10, -8, 20), 72, Facing.East);
            CaveSecretPocketConfig config = PocketConfig();

            Assert.IsTrue(CaveSecretPocketAuthoring.TryAuthor(
                world, in terminal, in config, out CaveSecretPocket secret));

            Assert.Multiple(() =>
            {
                Assert.IsTrue(secret.IsWellFormed);
                Assert.IsTrue(secret.SeparatesHiddenSpaceBeforeOpen);
                Assert.IsTrue(secret.GrantsNormalTraversalAfterOpen);
                Assert.IsTrue(secret.SupportsDestruction);
                Assert.IsTrue(secret.CanMatchHostSurface);
                Assert.IsFalse(secret.IsStructurallyCritical);
                Assert.AreEqual(2, world.CarveCalls);
                Assert.IsTrue(AllSolid(world, in secret.Barrier),
                    "The false wall must remain original solid cave rock.");
                Assert.IsTrue(AllEmpty(world, in secret.Connector));
                Assert.IsTrue(AllEmpty(world, in secret.Pocket));
            });
        }

        [Test]
        public void SecretPocketPreflightFailureIsAtomic()
        {
            var world = new SolidWorldSession();
            CaveTraversalCandidate terminal = Branch(new int3(0, -4, 0), 72, Facing.North);
            CaveSecretPocketConfig config = PocketConfig();

            // For a north-facing terminal with this config the connector begins at z=3. A pre-existing
            // opening there invalidates the topology proof before any authoring mutation is allowed.
            world.MakeEmpty(new int3(0, -4, 3));

            Assert.IsFalse(CaveSecretPocketAuthoring.TryAuthor(
                world, in terminal, in config, out _));
            Assert.AreEqual(0, world.CarveCalls,
                "Failed solid-rock topology validation must not partially carve a secret pocket.");
        }

        private static CaveTraversalCandidate Main(int3 position, int distance, Facing facing = Facing.North) =>
            Candidate(position, distance, 0, CaveTraversalFlags.MainPath, facing);

        private static CaveTraversalCandidate Branch(int3 position, int distance, Facing facing = Facing.West) =>
            Candidate(position, distance, 1, CaveTraversalFlags.Branch, facing);

        private static CaveTraversalCandidate Candidate(
            int3 position,
            int distance,
            byte depth,
            CaveTraversalFlags pathFlag,
            Facing facing) => new CaveTraversalCandidate
        {
            Position = position,
            TraversalDistance = distance,
            BranchDepth = depth,
            Flags = CaveTraversalFlags.ReachableFromEntrance |
                    CaveTraversalFlags.Terminal |
                    pathFlag,
            ExitFacing = facing,
        };

        private static CaveSecretPocketConfig PocketConfig() => new CaveSecretPocketConfig
        {
            BarrierThickness = 2,
            EntranceWidth = 5,
            EntranceHeight = 7,
            ConnectorLength = 3,
            PocketWidth = 9,
            PocketHeight = 9,
            PocketDepth = 9,
        };

        private static bool AllSolid(SolidWorldSession world, in DecorationBounds bounds)
        {
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            for (int z = bounds.Min.z; z < bounds.MaxExclusive.z; z++)
            for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                if (!world.IsSolid(x, y, z)) return false;
            return true;
        }

        private static bool AllEmpty(SolidWorldSession world, in DecorationBounds bounds)
        {
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            for (int z = bounds.Min.z; z < bounds.MaxExclusive.z; z++)
            for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                if (world.IsSolid(x, y, z)) return false;
            return true;
        }

        private sealed class SolidWorldSession : IStructureAuthoringSession
        {
            private readonly HashSet<int3> _empty = new HashSet<int3>();

            public int CarveCalls { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => _empty.Count;

            public void MakeEmpty(int3 position) => _empty.Add(position);
            public byte Get(int x, int y, int z) => IsSolid(x, y, z) ? (byte)2 : (byte)0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => !_empty.Contains(new int3(x, y, z));
            public void Set(int x, int y, int z, byte material)
            {
                int3 p = new int3(x, y, z);
                if (material == 0) _empty.Add(p); else _empty.Remove(p);
            }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
                Set(x, y, z, material);
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
            public void Carve(int3 min, int3 size)
            {
                CarveCalls++;
                for (int y = min.y; y < min.y + size.y; y++)
                for (int z = min.z; z < min.z + size.z; z++)
                for (int x = min.x; x < min.x + size.x; x++)
                    _empty.Add(new int3(x, y, z));
            }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
