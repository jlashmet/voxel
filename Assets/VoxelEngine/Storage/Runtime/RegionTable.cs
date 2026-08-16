using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Storage.Runtime
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

        // Meshing probes are extremely spatially coherent: a Transvoxel density sample performs
        // many neighbouring voxel reads that overwhelmingly hit the same 51.2 m region. Avoid
        // paying NativeHashMap lookup cost for every one of those probes. The cached slot is
        // validated against the live Region before use, so slot reuse after eviction cannot return
        // stale data even if a copied RegionTable carries an old scalar cache.
        private int3 _lastCoord;
        private int _lastSlot;
        private bool _hasLast;

        public int ResidentCount => _coordToSlot.Count;
        public bool IsCreated => _coordToSlot.IsCreated;

        public RegionTable(int expectedResident, Allocator allocator)
        {
            _allocator = allocator;
            _coordToSlot = new NativeHashMap<int3, int>(expectedResident, allocator);
            _regions = new NativeList<Region>(expectedResident, allocator);
            _freeSlots = new NativeList<int>(expectedResident >> 2, allocator);
            _lastCoord = default;
            _lastSlot = -1;
            _hasLast = false;
        }

        public bool TryGetRegion(int3 coord, out Region region)
        {
            if (_hasLast && coord.Equals(_lastCoord)
                && (uint)_lastSlot < (uint)_regions.Length)
            {
                Region cached = _regions[_lastSlot];
                if (cached.IsCreated && cached.Coord.Equals(coord))
                {
                    region = cached;
                    return true;
                }

                _hasLast = false;
                _lastSlot = -1;
            }

            if (_coordToSlot.TryGetValue(coord, out var slot))
            {
                region = _regions[slot];
                _lastCoord = coord;
                _lastSlot = slot;
                _hasLast = true;
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
            if (_hasLast && coord.Equals(_lastCoord)
                && (uint)_lastSlot < (uint)_regions.Length)
            {
                Region cached = _regions[_lastSlot];
                if (cached.IsCreated && cached.Coord.Equals(coord))
                    return cached;
            }

            if (_coordToSlot.TryGetValue(coord, out var existing))
            {
                _lastCoord = coord;
                _lastSlot = existing;
                _hasLast = true;
                return _regions[existing];
            }

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
            _lastCoord = coord;
            _lastSlot = slot;
            _hasLast = true;
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
            {
                _regions[slot] = region;
                _lastCoord = region.Coord;
                _lastSlot = slot;
                _hasLast = true;
            }
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

            if (_hasLast && (_lastSlot == slot || _lastCoord.Equals(coord)))
            {
                _hasLast = false;
                _lastSlot = -1;
            }
        }

        public NativeArray<int3> GetResidentCoords(Allocator allocator) =>
            _coordToSlot.GetKeyArray(allocator);

        public bool CopyResidentCoords(ref int cursor, NativeArray<int3> destination,
                                       out int count)
        {
            if (!destination.IsCreated || destination.Length == 0)
                throw new ArgumentException("Destination must contain at least one slot.",
                                            nameof(destination));
            cursor = math.clamp(cursor, 0, _regions.Length);
            count = 0;
            int slotsExamined = 0;
            while (cursor < _regions.Length && slotsExamined < destination.Length)
            {
                Region region = _regions[cursor++];
                slotsExamined++;
                if (!region.IsCreated) continue;
                destination[count++] = region.Coord;
            }
            return cursor >= _regions.Length;
        }

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
            _hasLast = false;
            _lastSlot = -1;
        }
    }
}
