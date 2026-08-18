using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Why a slot request could not be served.</summary>
    public enum GpuBrickAdmission
    {
        /// <summary>A slot is held for this coordinate at the requested generation.</summary>
        Resident = 0,

        /// <summary>A slot was taken from the free list or from a colder resident brick.</summary>
        Admitted = 1,

        /// <summary>Every slot is pinned by something at least as warm; the caller must wait.</summary>
        Full = 2,

        /// <summary>The delta describes no payload, so it never needed a slot.</summary>
        NoPayload = 3,

        /// <summary>An older generation arrived after a newer one; publishing it would go backwards.</summary>
        Stale = 4,
    }

    /// <summary>
    /// Which GPU brick slot holds which logical brick.
    ///
    /// The GPU mirror is a fixed set of slots indexed exactly like the CPU authoritative brick pool,
    /// so publishing a brick is a write into four parallel buffers at one index. What this type owns
    /// is the mapping in between: logical brick coordinate to slot, the generation resident in each
    /// slot, and which slot to reuse when the table is full.
    ///
    /// It is deliberately free of Unity types and GPU resources. Slot bookkeeping is where a mirror
    /// goes wrong — a stale generation overwriting a newer one, a slot recycled while the hierarchy
    /// still references it — and none of those failures need a graphics device to reproduce. Keeping
    /// the policy here means it can be tested as ordinary integer logic.
    ///
    /// Eviction is least-recently-touched. Fixed slots and no defragmentation are per specs §8.3:
    /// moving one logical brick must never force repacking the world.
    /// </summary>
    public sealed class GpuBrickSlotTable
    {
        private struct Slot
        {
            public int3 Coordinate;
            public ulong Generation;
            public long LastTouched;
            public bool Occupied;
            public bool Pinned;
        }

        private readonly Slot[] _slots;
        private readonly Dictionary<int3, int> _slotByCoordinate;
        private readonly Stack<int> _free;
        private long _clock;

        public int Capacity { get; }
        public int ResidentCount => Capacity - _free.Count;
        public int PinnedCount { get; private set; }

        /// <summary>Slots taken from a resident brick rather than the free list.</summary>
        public ulong EvictionCount { get; private set; }

        /// <summary>Requests refused because every slot was pinned.</summary>
        public ulong RefusedCount { get; private set; }

        /// <summary>Deltas dropped for describing an older generation than the slot already holds.</summary>
        public ulong StaleCount { get; private set; }

        public GpuBrickSlotTable(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            Capacity = capacity;
            _slots = new Slot[capacity];
            _slotByCoordinate = new Dictionary<int3, int>(capacity);
            _free = new Stack<int>(capacity);
            for (int i = capacity - 1; i >= 0; i--) _free.Push(i);
        }

        public bool TryGetSlot(int3 coordinate, out int slot) =>
            _slotByCoordinate.TryGetValue(coordinate, out slot);

        public bool TryGetGeneration(int3 coordinate, out ulong generation)
        {
            if (!_slotByCoordinate.TryGetValue(coordinate, out int slot))
            {
                generation = 0;
                return false;
            }
            generation = _slots[slot].Generation;
            return true;
        }

        /// <summary>
        /// Finds or reserves the slot a delta should publish into.
        ///
        /// A delta with no payload never consumes a slot, and releases one it previously held: a
        /// brick that was blown open into air must stop occupying mirror memory, not linger with
        /// geometry nobody can reach.
        /// </summary>
        public GpuBrickAdmission TryAdmit(in VoxelBrickDelta delta, out int slot)
        {
            slot = -1;

            if (!delta.NeedsSlot)
            {
                Release(delta.Coordinate);
                return GpuBrickAdmission.NoPayload;
            }

            if (_slotByCoordinate.TryGetValue(delta.Coordinate, out int existing))
            {
                ulong resident = _slots[existing].Generation;
                if (delta.SourceGeneration < resident)
                {
                    StaleCount++;
                    return GpuBrickAdmission.Stale;
                }

                slot = existing;
                Touch(existing);
                if (delta.SourceGeneration == resident) return GpuBrickAdmission.Resident;

                _slots[existing].Generation = delta.SourceGeneration;
                return GpuBrickAdmission.Admitted;
            }

            if (!TryTakeSlot(out slot))
            {
                RefusedCount++;
                return GpuBrickAdmission.Full;
            }

            _slots[slot] = new Slot
            {
                Coordinate = delta.Coordinate,
                Generation = delta.SourceGeneration,
                LastTouched = ++_clock,
                Occupied = true,
            };
            _slotByCoordinate[delta.Coordinate] = slot;
            return GpuBrickAdmission.Admitted;
        }

        /// <summary>
        /// Marks a brick as in use by active coverage so eviction cannot take it.
        ///
        /// Recycling a slot the render hierarchy still points at is how a mirror produces geometry
        /// from one brick wearing another's coordinate. Pinning is the caller's statement that the
        /// slot is referenced; it is not a cache hint.
        /// </summary>
        public bool Pin(int3 coordinate)
        {
            if (!_slotByCoordinate.TryGetValue(coordinate, out int slot)) return false;
            if (_slots[slot].Pinned) return true;
            _slots[slot].Pinned = true;
            PinnedCount++;
            return true;
        }

        public bool Unpin(int3 coordinate)
        {
            if (!_slotByCoordinate.TryGetValue(coordinate, out int slot)) return false;
            if (!_slots[slot].Pinned) return false;
            _slots[slot].Pinned = false;
            PinnedCount--;
            return true;
        }

        public bool IsPinned(int3 coordinate) =>
            _slotByCoordinate.TryGetValue(coordinate, out int slot) && _slots[slot].Pinned;

        /// <summary>Records use, so eviction prefers colder bricks.</summary>
        public void Touch(int3 coordinate)
        {
            if (_slotByCoordinate.TryGetValue(coordinate, out int slot)) Touch(slot);
        }

        public bool Release(int3 coordinate)
        {
            if (!_slotByCoordinate.Remove(coordinate, out int slot)) return false;
            if (_slots[slot].Pinned) PinnedCount--;
            _slots[slot] = default;
            _free.Push(slot);
            return true;
        }

        public void Clear()
        {
            _slotByCoordinate.Clear();
            _free.Clear();
            for (int i = Capacity - 1; i >= 0; i--)
            {
                _slots[i] = default;
                _free.Push(i);
            }
            PinnedCount = 0;
            _clock = 0;
        }

        private void Touch(int slot) => _slots[slot].LastTouched = ++_clock;

        private bool TryTakeSlot(out int slot)
        {
            if (_free.Count > 0)
            {
                slot = _free.Pop();
                return true;
            }

            slot = -1;
            long coldest = long.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Occupied || _slots[i].Pinned) continue;
                if (_slots[i].LastTouched >= coldest) continue;
                coldest = _slots[i].LastTouched;
                slot = i;
            }

            if (slot < 0) return false;

            _slotByCoordinate.Remove(_slots[slot].Coordinate);
            _slots[slot] = default;
            EvictionCount++;
            return true;
        }
    }
}
