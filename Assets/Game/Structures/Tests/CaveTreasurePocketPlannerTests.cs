using System.Collections.Generic;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.Structures.Tests
{
    public sealed class CaveTreasurePocketPlannerTests
    {
        [Test]
        public void PhysicallyBlockedBestTerminalFallsBackWithoutPartialCarve()
        {
            CaveTraversalCandidate preferred = Branch(new int3(0, -4, 0), 90, Facing.North);
            CaveTraversalCandidate fallback = Branch(new int3(30, -4, 0), 60, Facing.North);
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(fallback);
            candidates.Items.Add(preferred);

            var world = new SolidWorldSession();
            CaveSecretPocketConfig config = PocketConfig();

            // Barrier thickness 2 means the north-facing connector starts at z=3. This simulates a
            // terminal chamber or intersecting cave volume occupying the preferred pocket envelope.
            world.MakeEmpty(new int3(0, -4, 3));

            Assert.IsTrue(CaveTreasurePocketPlanner.TryAuthorBest(
                world,
                in candidates,
                24,
                in config,
                out CaveGameplayPlacement placement,
                out CaveSecretPocket pocket));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(fallback.Position, placement.Terminal.Position);
                Assert.AreEqual(fallback.TraversalDistance, placement.Terminal.TraversalDistance);
                Assert.IsTrue(pocket.IsWellFormed);
                Assert.AreEqual(2, world.CarveCalls,
                    "Rejected preferred terminal must perform no carve before the fallback succeeds.");
            });
        }

        [Test]
        public void MutationFailureDoesNotRetryAnotherTerminal()
        {
            CaveTraversalCandidate preferred = Branch(new int3(0, -4, 0), 90, Facing.North);
            CaveTraversalCandidate fallback = Branch(new int3(30, -4, 0), 60, Facing.North);
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(fallback);
            candidates.Items.Add(preferred);

            var world = new SolidWorldSession { DropCarveCall = 2 };
            CaveSecretPocketConfig config = PocketConfig();

            Assert.IsFalse(CaveTreasurePocketPlanner.TryAuthorBest(
                world,
                in candidates,
                24,
                in config,
                out _,
                out CaveSecretPocket pocket));

            Assert.Multiple(() =>
            {
                Assert.IsFalse(pocket.IsWellFormed,
                    "A pocket must not become semantic proof when read-back shows an incomplete carve.");
                Assert.AreEqual(2, world.CarveCalls,
                    "A storage/mutation failure may have changed geometry, so fallback must abort instead of carving another terminal.");
            });
        }

        [Test]
        public void AuthoredCaveTerminalLeavesVerifiedRockForTreasurePocket()
        {
            var world = new SolidWorldSession();
            CaveConfig cave = CaveConfig.Default;
            cave.TunnelWidth = 5;
            cave.TunnelHeight = 7;
            cave.SegmentLength = 8;
            cave.MainSegmentCount = 3;
            cave.TurnChancePercent = 0;
            cave.VerticalChancePercent = 0;
            cave.SurfaceDescentSegments = 0;
            cave.SurfaceDescentPerSegment = 0;
            cave.MinimumSurfaceCover = 0;
            cave.ChamberChancePercent = 0;
            cave.BranchChancePercent = 100;
            cave.MaxBranches = 1;
            cave.MaxBranchDepth = 1;
            cave.BranchSegmentCount = 2;
            cave.MinBranchSeparation = 0;
            cave.WallRoughness = 0;
            cave.FloorRoughness = 0;
            cave.CeilingRoughness = 0;

            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0x12345678ul, int3.zero, Facing.North, 5, 7, 2);
            var palette = new CaveMaterialPalette { Opening = 0, Rock = 2 };
            CaveAuthoringResult authored = CaveAuthoring.Author(
                world, in request, in cave, in palette);
            CaveSecretPocketConfig pocketConfig = PocketConfig();

            Assert.GreaterOrEqual(authored.TraversalCandidates.Count, 2,
                "Configured authored cave should expose a main and branch terminal.");
            Assert.IsTrue(CaveTreasurePocketPlanner.TryAuthorBest(
                world,
                in authored.TraversalCandidates,
                8,
                in pocketConfig,
                out CaveGameplayPlacement placement,
                out CaveSecretPocket pocket));

            Assert.Multiple(() =>
            {
                Assert.That(placement.Terminal.Flags & CaveTraversalFlags.Branch,
                    Is.EqualTo(CaveTraversalFlags.Branch));
                Assert.IsTrue(pocket.IsWellFormed);
                Assert.IsTrue(AllSolid(world, in pocket.Barrier),
                    "The actual cave authorer must leave the retained false-wall slab as rock.");
                Assert.IsTrue(AllEmpty(world, in pocket.Connector));
                Assert.IsTrue(AllEmpty(world, in pocket.Pocket));
                Assert.AreEqual(2, world.CarveCalls,
                    "Cave authoring itself uses cross-section fills; only the secret connector and pocket use Carve.");
            });
        }

        private static CaveTraversalCandidate Branch(int3 position, int distance, Facing facing) =>
            new CaveTraversalCandidate
            {
                Position = position,
                TraversalDistance = distance,
                BranchDepth = 1,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.Terminal |
                        CaveTraversalFlags.Branch,
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

        private static bool AllSolid(SolidWorldSession world, in Game.Structures.Api.DecorationBounds bounds)
        {
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            for (int z = bounds.Min.z; z < bounds.MaxExclusive.z; z++)
            for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                if (!world.IsSolid(x, y, z)) return false;
            return true;
        }

        private static bool AllEmpty(SolidWorldSession world, in Game.Structures.Api.DecorationBounds bounds)
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
            public int DropCarveCall { get; set; }
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
            public void FillBulk(int3 min, int3 size, byte material)
            {
                for (int y = min.y; y < min.y + size.y; y++)
                for (int z = min.z; z < min.z + size.z; z++)
                for (int x = min.x; x < min.x + size.x; x++)
                    Set(x, y, z, material);
            }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
            {
                for (int y = minY; y < maxYExclusive; y++)
                    Set(x, y, z, material);
            }
            public void Box(int3 min, int3 size, byte material) => FillBulk(min, size, material);
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
                if (DropCarveCall == CarveCalls) return;
                FillBulk(min, size, 0);
            }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
