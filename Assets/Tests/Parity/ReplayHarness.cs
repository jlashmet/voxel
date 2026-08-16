using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Storage.Runtime.Occupancy;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Terrain.Runtime;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// Deterministic replay harness over seeded worlds and recorded event logs.
    ///
    /// Runs the same sequence of AlterationEvents through two independent BrickPool
    /// + RegionTable instances and asserts byte-identical state after every tick.
    /// This is the core of SC-003: if this harness can distinguish two runs, the
    /// engine has failed Constitution Principle I (Determinism).
    ///
    /// Usage pattern (test code):
    /// <code>
    /// using var harness = new ReplayHarness();
    /// harness.SeedWorld(seed);
    /// harness.ReplayEvents(eventsA, eventsB);
    /// Assert.IsTrue(harness.StateMatches);
    /// </code>
    /// </summary>
    public struct ReplayHarness : IDisposable
    {
        private BrickPool _poolA;
        private RegionTable _tableA;
        private BrickPool _poolB;
        private RegionTable _tableB;

        public bool StateMatches { get; private set; }
        public int FirstMismatchIndex { get; private set; }

        public void Dispose()
        {
            _tableA.Dispose();
            _poolA.Dispose();
            _tableB.Dispose();
            _poolB.Dispose();
        }

        /// <summary>
        /// Initialise both worlds with the same terrain seed.
        /// Must be called before any replay.
        /// </summary>
        public void SeedWorld(uint terrainSeed)
        {
            // Allocate capacity per device-matrix tier budgets for a test world.
            const int poolCapacity = 4096;

            _poolA = new BrickPool(poolCapacity, Allocator.Persistent);
            _tableA = new RegionTable(128, Allocator.Persistent);

            _poolB = new BrickPool(poolCapacity, Allocator.Persistent);
            _tableB = new RegionTable(128, Allocator.Persistent);

            // Generate identical terrain in both.
            var regionA = _tableA.LoadRegion(int3.zero);
            var regionB = _tableB.LoadRegion(int3.zero);
            var generationA = new RegionGenerationStore(in _tableA);
            var generationB = new RegionGenerationStore(in _tableB);

            VoxelEngine.Terrain.Runtime.TerrainGenerator.Generate(
                generationA, regionA.Coord, terrainSeed, ParityTerrain.Materials);
            VoxelEngine.Terrain.Runtime.TerrainGenerator.Generate(
                generationB, regionB.Coord, terrainSeed, ParityTerrain.Materials);

            // Materialise both.
            _tableA.CommitRegion(regionA);
            _tableB.CommitRegion(regionB);

            StateMatches = true;
            FirstMismatchIndex = -1;
        }

        /// <summary>
        /// Replay a sequence of AlterationEvents through both worlds and verify they
        /// remain byte-identical after each event.
        /// </summary>
        public void ReplayEvents(AlterationEvent[] eventsA, AlterationEvent[] eventsB)
        {
            for (int i = 0; i < eventsA.Length && StateMatches; i++)
            {
                var evtA = eventsA[i];
                var evtB = eventsB[i];

                ApplyEvent(ref _poolA, ref _tableA, in evtA);
                ApplyEvent(ref _poolB, ref _tableB, in evtB);

                // Compare the whole world, not just the bricks this event touched.
                // SC-003 claims byte-identical *state*; checking only the affected list
                // would miss divergence an earlier event introduced elsewhere.
                if (!WorldsMatch())
                {
                    StateMatches = false;
                    FirstMismatchIndex = i;
                    return;
                }
            }
        }

        /// <summary>
        /// True when both worlds hold byte-identical state: the same resident regions, the
        /// same brick references, and the same voxel and occupancy bytes for every mixed
        /// brick.
        ///
        /// Pool *indices* are deliberately not compared. Two runs may legitimately assign
        /// different slots for the same brick depending on free-list order; what must match
        /// is the content those slots hold.
        /// </summary>
        private bool WorldsMatch()
        {
            var coordsA = _tableA.GetResidentCoords(Allocator.Temp);
            var coordsB = _tableB.GetResidentCoords(Allocator.Temp);

            try
            {
                if (coordsA.Length != coordsB.Length) return false;

                for (int c = 0; c < coordsA.Length; c++)
                {
                    var coord = coordsA[c];

                    if (!_tableA.TryGetRegion(coord, out var regionA)) return false;
                    if (!_tableB.TryGetRegion(coord, out var regionB)) return false;

                    for (int b = 0; b < VoxelDimensions.BricksPerRegion; b++)
                    {
                        var refA = regionA.BrickRefs[b];
                        var refB = regionB.BrickRefs[b];

                        if (refA.IsMixed != refB.IsMixed) return false;

                        if (!refA.IsMixed)
                        {
                            // Empty or uniform: the reference itself carries the material.
                            if (refA.Value != refB.Value) return false;
                            continue;
                        }

                        if (!BrickBytesMatch(refA.PoolIndex, refB.PoolIndex)) return false;
                    }
                }

                return true;
            }
            finally
            {
                coordsA.Dispose();
                coordsB.Dispose();
            }
        }

        /// <summary>Compares the voxel and occupancy bytes of one brick in each pool.</summary>
        private bool BrickBytesMatch(int poolIndexA, int poolIndexB)
        {
            int voxA = _poolA.VoxelOffset(poolIndexA);
            int voxB = _poolB.VoxelOffset(poolIndexB);

            for (int v = 0; v < VoxelDimensions.VoxelsPerBrick; v++)
            {
                if (_poolA.Voxels[voxA + v] != _poolB.Voxels[voxB + v]) return false;
            }

            int occA = _poolA.OccupancyOffset(poolIndexA);
            int occB = _poolB.OccupancyOffset(poolIndexB);

            for (int o = 0; o < VoxelDimensions.OccupancyWordsPerBrick; o++)
            {
                if (_poolA.Occupancy[occA + o] != _poolB.Occupancy[occB + o]) return false;
            }

            return true;
        }

        /// <summary>
        /// Expands one event and writes its result into the world.
        ///
        /// Dispatches to the real Edits.Runtime expansions — the harness must exercise the
        /// shipping code paths, since a harness with its own copy of expansion would prove
        /// nothing about the engine.
        /// </summary>
        private static void ApplyEvent(ref BrickPool pool, ref RegionTable table, in AlterationEvent evt)
        {
            NativeList<int3> affected;

            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                    affected = ExplosionExpansion.Expand(new RegionReadSource(in table, in pool), in evt);
                    break;

                case AlterationEvent.KindBrush:
                    affected = BrushExpansion.Expand(evt);
                    break;

                default:
                    // Raw batch carries its payload out of band; nothing to expand here.
                    return;
            }

            WriteBricks(ref pool, ref table, affected, evt.material);
            affected.Dispose();
        }

        /// <summary>Writes the expanded brick set into the grid as uniform bricks.</summary>
        private static void WriteBricks(
            ref BrickPool pool, ref RegionTable table, in NativeList<int3> bricks, byte material)
        {
            for (int i = 0; i < bricks.Length; i++)
            {
                int3 brick = bricks[i];

                var regionCoord = new int3(
                    brick.x >> VoxelDimensions.RegionEdgeLog2,
                    brick.y >> VoxelDimensions.RegionEdgeLog2,
                    brick.z >> VoxelDimensions.RegionEdgeLog2);

                if (!table.TryGetRegion(regionCoord, out var region))
                    continue; // Non-resident regions are outside this replay's scope.

                int bx = brick.x & VoxelDimensions.RegionEdgeMask;
                int by = brick.y & VoxelDimensions.RegionEdgeMask;
                int bz = brick.z & VoxelDimensions.RegionEdgeMask;
                int idx = Region.BrickIndex(bx, by, bz);

                // Returning a mixed brick's slot before overwriting keeps the pool from
                // leaking across a long replay (T022's uniform-collapse invariant).
                var existing = region.BrickRefs[idx];
                if (existing.IsMixed) pool.Free(existing.PoolIndex);

                region.BrickRefs[idx] = BrickRef.Uniform(material);
                table.CommitRegion(region);
            }
        }
    }
}
