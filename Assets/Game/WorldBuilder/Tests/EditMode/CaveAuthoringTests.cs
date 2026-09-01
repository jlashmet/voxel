using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveAuthoringTests
    {
        [Test]
        public void SameSeedAndRequestProduceIdenticalWrites()
        {
            CaveConfig config = TestConfig();
            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0x1122334455667788ul,
                new int3(120, 40, -80),
                Facing.South,
                7,
                9,
                5);
            CaveMaterialPalette palette = TestPalette();
            var a = new RecordingSession();
            var b = new RecordingSession();

            CaveAuthoringResult resultA = CaveAuthoring.Author(a, in request, in config, in palette);
            CaveAuthoringResult resultB = CaveAuthoring.Author(b, in request, in config, in palette);

            Assert.Multiple(() =>
            {
                CollectionAssert.AreEqual(a.Operations, b.Operations);
                Assert.AreEqual(resultA.SegmentsAuthored, resultB.SegmentsAuthored);
                Assert.AreEqual(resultA.BranchesAuthored, resultB.BranchesAuthored);
                Assert.AreEqual(resultA.ChambersAuthored, resultB.ChambersAuthored);
                Assert.AreEqual(resultA.MainPathEnd, resultB.MainPathEnd);
                Assert.That(resultA.SegmentsAuthored, Is.GreaterThan(0));
            });
        }

        [Test]
        public void DifferentSeedsChangeDeterministicNetworkChoices()
        {
            CaveConfig config = TestConfig();
            config.TurnChancePercent = 100;
            config.VerticalChancePercent = 100;
            config.BranchChancePercent = 100;
            config.ChamberChancePercent = 100;
            CaveMaterialPalette palette = TestPalette();
            CaveGenerationRequest aRequest = CaveGenerationRequest.Attached(
                0x101ul, int3.zero, Facing.East, 7, 9, 4);
            CaveGenerationRequest bRequest = CaveGenerationRequest.Attached(
                0x202ul, int3.zero, Facing.East, 7, 9, 4);
            var a = new RecordingSession();
            var b = new RecordingSession();

            CaveAuthoring.Author(a, in aRequest, in config, in palette);
            CaveAuthoring.Author(b, in bRequest, in config, in palette);

            CollectionAssert.AreNotEqual(a.Operations, b.Operations);
        }

        [Test]
        public void GuaranteedTunnelCoreRemainsTraversableAcrossTurnsAndVerticalChanges()
        {
            CaveConfig config = TestConfig();
            config.TunnelWidth = 3;
            config.TunnelHeight = 4;
            config.SegmentLength = 6;
            config.MainSegmentCount = 10;
            config.TurnChancePercent = 100;
            config.VerticalChancePercent = 100;
            config.MaxVerticalStepPerSegment = 2;
            config.MaxBranches = 0;
            config.MaxBranchDepth = 0;
            config.BranchChancePercent = 0;
            config.ChamberChancePercent = 0;
            config.FloorRoughness = 0;
            config.CeilingRoughness = 0;
            config.WallRoughness = 0;
            CaveMaterialPalette palette = TestPalette();
            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0xAABBCCDDEEFFul, int3.zero, Facing.North, 3, 4, 3);
            var authoring = new RecordingSession(recordCells: true);

            CaveAuthoringResult result = CaveAuthoring.Author(
                authoring, in request, in config, in palette);

            int3 start = request.EntranceWorldPosition + new int3(0, 1, 0);
            int3 goal = result.MainPathEnd + new int3(0, 1, 0);
            Assert.IsTrue(IsReachable(authoring.Cells, start, goal),
                $"Guaranteed cave core did not connect {start} to {goal}.");
        }

        [Test]
        public void AllAuthoredCellsStayInsideRequestBounds()
        {
            CaveConfig config = TestConfig();
            config.ChamberChancePercent = 0;
            CaveMaterialPalette palette = TestPalette();
            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0x777ul,
                new int3(1_000, 200, -2_000),
                Facing.West,
                7,
                9,
                4);
            var authoring = new RecordingSession(recordCells: true);
            Assert.IsTrue(request.TryGetWorldBounds(in config, out StructureGenerationBounds bounds));

            CaveAuthoring.Author(authoring, in request, in config, in palette);

            foreach (int3 cell in authoring.Cells)
                Assert.IsTrue(bounds.Contains(cell), $"Out-of-bounds cave write at {cell}.");
        }

        [Test]
        public void SurfaceTunnelHonorsMinimumCoverAfterEntranceClearance()
        {
            const uint terrainSeed = 0x44556677u;
            int surfaceY = TerrainQuery.HeightAt(0, 0, terrainSeed);
            CaveConfig config = TestConfig();
            config.SurfaceDescentSegments = 0;
            config.MinimumSurfaceCover = 10;
            config.MaxBranches = 0;
            config.BranchChancePercent = 0;
            config.ChamberChancePercent = 0;
            CaveMaterialPalette palette = TestPalette();
            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x1234ul,
                terrainSeed,
                new int3(0, surfaceY, 0),
                Facing.East,
                5,
                7,
                3);
            var authoring = new RecordingSession();

            CaveAuthoring.Author(authoring, in request, in config, in palette);

            foreach (ColumnWrite write in authoring.Columns)
            {
                // The explicit entrance throat is allowed to reach the surface. Every generated
                // network cross-section after that throat must remain under the requested cover.
                if (write.X <= request.Origin.x + request.Entrance.ClearanceLength)
                    continue;

                int surface = TerrainQuery.HeightAt(write.X, write.Z, terrainSeed);
                Assert.That(
                    write.MaxYExclusive,
                    Is.LessThanOrEqualTo(surface - config.MinimumSurfaceCover),
                    $"Tunnel surfaced at ({write.X},{write.Z}).");
            }
        }

        [Test]
        public void RegionClippingOrderDoesNotChangeAttachedCaveCells()
        {
            CaveConfig config = TestConfig();
            config.MaxBranches = 0;
            config.BranchChancePercent = 0;
            config.ChamberChancePercent = 0;
            CaveMaterialPalette palette = TestPalette();
            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0xCAFEBABEul,
                new int3(10, 30, 10),
                Facing.East,
                5,
                7,
                3);

            var full = new RecordingSession(recordCells: true);
            CaveAuthoring.Author(full, in request, in config, in palette);

            int splitX = request.Origin.x + 18;
            var left = new RecordingSession(true, p => p.x < splitX);
            var right = new RecordingSession(true, p => p.x >= splitX);
            CaveAuthoring.Author(left, in request, in config, in palette);
            CaveAuthoring.Author(right, in request, in config, in palette);

            var union = new HashSet<int3>(right.Cells);
            union.UnionWith(left.Cells);
            CollectionAssert.AreEquivalent(full.Cells, union);
        }

        [Test]
        public void ValidationRejectsLoopsInvalidProbabilitiesAndWorldOverflow()
        {
            CaveConfig valid = TestConfig();
            CaveConfig looped = valid;
            looped.EnableLoops = true;
            CaveConfig invalidChance = valid;
            invalidChance.BranchChancePercent = 101;

            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0x99ul,
                new int3(int.MaxValue - 2, 0, 0),
                Facing.North,
                5,
                7,
                3);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(valid.IsWellFormed);
                Assert.IsFalse(looped.IsWellFormed);
                Assert.IsFalse(invalidChance.IsWellFormed);
                Assert.IsFalse(request.TryGetWorldBounds(in valid, out _));
            });
        }

        private static CaveConfig TestConfig()
        {
            CaveConfig config = CaveConfig.Default;
            config.TunnelWidth = 5;
            config.TunnelHeight = 7;
            config.SegmentLength = 6;
            config.MainSegmentCount = 8;
            config.MaxVerticalStepPerSegment = 2;
            config.SurfaceDescentSegments = 2;
            config.SurfaceDescentPerSegment = 2;
            config.MaxBranches = 3;
            config.MaxBranchDepth = 1;
            config.BranchSegmentCount = 3;
            config.MinBranchSeparation = 4;
            config.MinChamberRadius = 4;
            config.MaxChamberRadius = 7;
            config.MinChamberHeight = 5;
            config.MaxChamberHeight = 9;
            config.FloorRoughness = 1;
            config.CeilingRoughness = 1;
            config.WallRoughness = 1;
            config.BoundsHalfExtents = new int3(96, 48, 96);
            config.MinVerticalOffset = -36;
            config.MaxVerticalOffset = 24;
            return config;
        }

        private static CaveMaterialPalette TestPalette() => new CaveMaterialPalette
        {
            Opening = 0,
            Rock = 2,
            Accent = 3,
            Decoration = 4,
            Water = 5,
        };

        private static bool IsReachable(HashSet<int3> cells, int3 start, int3 goal)
        {
            if (!cells.Contains(start) || !cells.Contains(goal)) return false;

            var queue = new Queue<int3>();
            var visited = new HashSet<int3> { start };
            queue.Enqueue(start);
            int3[] steps =
            {
                new int3(1, 0, 0), new int3(-1, 0, 0),
                new int3(0, 1, 0), new int3(0, -1, 0),
                new int3(0, 0, 1), new int3(0, 0, -1),
            };

            while (queue.Count > 0)
            {
                int3 current = queue.Dequeue();
                if (current.Equals(goal)) return true;

                foreach (int3 step in steps)
                {
                    int3 next = current + step;
                    if (cells.Contains(next) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }
            return false;
        }

        private readonly struct ColumnWrite
        {
            public readonly int X;
            public readonly int MinY;
            public readonly int MaxYExclusive;
            public readonly int Z;

            public ColumnWrite(int x, int minY, int maxYExclusive, int z)
            {
                X = x;
                MinY = minY;
                MaxYExclusive = maxYExclusive;
                Z = z;
            }
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            private readonly bool _recordCells;
            private readonly Func<int3, bool> _cellFilter;

            public RecordingSession(bool recordCells = false, Func<int3, bool> cellFilter = null)
            {
                _recordCells = recordCells;
                _cellFilter = cellFilter;
            }

            public readonly List<string> Operations = new List<string>();
            public readonly List<ColumnWrite> Columns = new List<ColumnWrite>();
            public readonly HashSet<int3> Cells = new HashSet<int3>();
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => Cells.Count;

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

            public void FillColumnBulk(
                int x, int minY, int maxYExclusive, int z, byte material)
            {
                Operations.Add($"column:{x}:{minY}:{maxYExclusive}:{z}:{material}");
                Columns.Add(new ColumnWrite(x, minY, maxYExclusive, z));
                if (!_recordCells) return;

                for (int y = minY; y < maxYExclusive; y++)
                {
                    var cell = new int3(x, y, z);
                    if (_cellFilter == null || _cellFilter(cell))
                        Cells.Add(cell);
                }
            }

            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(
                int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling) { }
            public void Cylinder(
                int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0)
            {
                Operations.Add($"cylinder:{cx}:{baseY}:{cz}:{radius}:{height}:{material}:{innerRadius}");
            }
            public void Disc(int cx, int y, int cz, int radius, byte material)
            {
                Operations.Add($"disc:{cx}:{y}:{cz}:{radius}:{material}");
            }
            public void Cone(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(
                int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(
                int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(
                int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(
                int3 min, int width, int height, int depth,
                int depthAxis, byte material) { }
            public void Stairs(
                int3 min, int width, int steps, int rise, int run,
                int axis, byte material) { }
            public void SpiralStair(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(
                int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
