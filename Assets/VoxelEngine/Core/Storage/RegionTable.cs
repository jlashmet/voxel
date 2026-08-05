using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Sparse map from region coordinate to resident region.
    ///
    /// This tier is what makes a kilometre-scale world possible at all. A flat
    /// top-level grid is not viable: at 10 cm voxels each brick spans 0.8 m, so a
    /// 10 km world is 12,500 bricks per axis and a flat pointer grid would need
    /// roughly 2e12 entries. Sparsity here is the enabling structure, not an
    /// optimisation.
    ///
    /// Only resident regions exist. World extent costs nothing.
    /// </summary>
    public struct RegionTable : IDisposable
    {
        private NativeHashMap<int3, int> _coordToSlot;
        private NativeList<Region> _regions;
        private NativeList<int> _freeSlots;
        private readonly Allocator _allocator;

        public int ResidentCount => _coordToSlot.Count;
        public bool IsCreated => _coordToSlot.IsCreated;

        public RegionTable(int expectedResident, Allocator allocator)
        {
            _allocator = allocator;
            _coordToSlot = new NativeHashMap<int3, int>(expectedResident, allocator);
            _regions = new NativeList<Region>(expectedResident, allocator);
            _freeSlots = new NativeList<int>(expectedResident >> 2, allocator);
        }

        public bool TryGetRegion(int3 coord, out Region region)
        {
            if (_coordToSlot.TryGetValue(coord, out var slot))
            {
                region = _regions[slot];
                return true;
            }

            region = default;
            return false;
        }

        public bool IsResident(int3 coord) => _coordToSlot.ContainsKey(coord);

        /// <summary>
        /// Makes a region resident. Idempotent — loading an already-resident region
        /// returns the existing one rather than leaking the old brick references.
        /// </summary>
        public Region LoadRegion(int3 coord)
        {
            if (_coordToSlot.TryGetValue(coord, out var existing))
                return _regions[existing];

            var region = new Region(coord, _allocator);

            int slot;
            if (_freeSlots.Length > 0)
            {
                slot = _freeSlots[_freeSlots.Length - 1];
                _freeSlots.RemoveAt(_freeSlots.Length - 1);
                _regions[slot] = region;
            }
            else
            {
                slot = _regions.Length;
                _regions.Add(region);
            }

            _coordToSlot.Add(coord, slot);
            return region;
        }

        /// <summary>
        /// Writes a mutated region back to its slot. Region is a struct, so callers
        /// operate on a copy and must commit. Deliberate: it keeps region access
        /// allocation-free in the hot path at the cost of an explicit write-back.
        /// </summary>
        public void CommitRegion(in Region region)
        {
            if (_coordToSlot.TryGetValue(region.Coord, out var slot))
                _regions[slot] = region;
        }

        /// <summary>
        /// Evicts a region, returning its mixed bricks to the pool.
        ///
        /// On the client this requires no write-back: the client owns no truth, so it
        /// discards and regenerates terrain from the seed plus re-fetches the edit
        /// overlay on return. That asymmetry is what makes unload effectively
        /// instantaneous and fast traversal smooth.
        /// </summary>
        public void EvictRegion(int3 coord, ref BrickPool pool)
        {
            if (!_coordToSlot.TryGetValue(coord, out var slot)) return;

            var region = _regions[slot];
            region.ReleaseBricks(ref pool);
            region.Dispose();

            _regions[slot] = default;
            _freeSlots.Add(slot);
            _coordToSlot.Remove(coord);
        }

        public NativeArray<int3> GetResidentCoords(Allocator allocator) =>
            _coordToSlot.GetKeyArray(allocator);

        public void Dispose()
        {
            if (_regions.IsCreated)
            {
                for (var i = 0; i < _regions.Length; i++)
                {
                    var r = _regions[i];
                    if (r.IsCreated) r.Dispose();
                }

                _regions.Dispose();
            }

            if (_coordToSlot.IsCreated) _coordToSlot.Dispose();
            if (_freeSlots.IsCreated) _freeSlots.Dispose();
        }
    }
}
