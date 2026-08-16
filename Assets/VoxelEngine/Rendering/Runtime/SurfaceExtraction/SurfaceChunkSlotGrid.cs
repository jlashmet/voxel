using System;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Fixed-capacity toroidal slot ownership for one LOD ring. World coordinates map directly
    /// to one cell by modulo the current clipmap edge, so moving the camera never grows a slot
    /// dictionary. Coordinates simultaneously inside the (2r+1)^3 window cannot collide.
    /// Reusing a toroidal cell always advances its generation, invalidating stale worker builds.
    /// </summary>
    internal sealed class SurfaceChunkSlotGrid
    {
        private SurfaceChunkSlot[] _slots = Array.Empty<SurfaceChunkSlot>();
        // Dense active-index bookkeeping lets visibility walk only slots that actually own a
        // discovered surface chunk. The reverse map keeps acquire/retire O(1), including
        // toroidal replacement when a newly exposed coordinate reuses an outgoing cell.
        private int[] _activeSlotIndices = Array.Empty<int>();
        private int[] _activeDenseIndexBySlot = Array.Empty<int>();
        private int3 _centre;
        private int _radius = -1;
        private int _edge;
        private uint _generationCounter;

        public int Capacity => _slots.Length;
        public int ActiveCount { get; private set; }

        public void UpdateWindow(int3 centre, int radius)
        {
            int nextRadius = math.max(0, radius);
            if (_radius != nextRadius)
            {
                _radius = nextRadius;
                _edge = _radius * 2 + 1;
                int capacity = _edge * _edge * _edge;
                _slots = new SurfaceChunkSlot[capacity];
                _activeSlotIndices = new int[capacity];
                _activeDenseIndexBySlot = new int[capacity];
                for (int i = 0; i < capacity; i++)
                    _activeDenseIndexBySlot[i] = -1;
                ActiveCount = 0;
            }
            _centre = centre;
        }

        public bool TryAcquire(int3 coordinate, out SurfaceChunkSlot slot)
        {
            if (!Contains(coordinate))
            {
                slot = default;
                return false;
            }

            int index = SlotIndex(coordinate);
            ref SurfaceChunkSlot current = ref _slots[index];
            if (current.Generation == 0 || !current.Coordinate.Equals(coordinate))
            {
                bool replacing = current.Generation != 0;
                if (!replacing)
                {
                    _activeDenseIndexBySlot[index] = ActiveCount;
                    _activeSlotIndices[ActiveCount++] = index;
                }
                current.Reinitialize(coordinate, NextGeneration());
            }

            slot = current;
            return true;
        }

        public bool TryGet(int3 coordinate, out SurfaceChunkSlot slot)
        {
            if (!Contains(coordinate) || _edge <= 0)
            {
                slot = default;
                return false;
            }

            slot = _slots[SlotIndex(coordinate)];
            return slot.Generation != 0 && slot.Coordinate.Equals(coordinate);
        }

        public void Retire(int3 coordinate)
        {
            if (_edge <= 0) return;
            int index = SlotIndex(coordinate);
            ref SurfaceChunkSlot slot = ref _slots[index];
            if (slot.Generation == 0 || !slot.Coordinate.Equals(coordinate)) return;

            int denseIndex = _activeDenseIndexBySlot[index];
            if (denseIndex >= 0)
            {
                int lastDenseIndex = ActiveCount - 1;
                int lastSlotIndex = _activeSlotIndices[lastDenseIndex];
                _activeSlotIndices[denseIndex] = lastSlotIndex;
                _activeDenseIndexBySlot[lastSlotIndex] = denseIndex;
                _activeDenseIndexBySlot[index] = -1;
                ActiveCount = lastDenseIndex;
            }
            slot.Retire();
        }

        public int3 ActiveCoordinateAt(int activeIndex)
        {
            if ((uint)activeIndex >= (uint)ActiveCount)
                throw new ArgumentOutOfRangeException(nameof(activeIndex));
            return _slots[_activeSlotIndices[activeIndex]].Coordinate;
        }

        private bool Contains(int3 coordinate)
        {
            if (_radius < 0) return false;
            int3 delta = math.abs(coordinate - _centre);
            return math.cmax(delta) <= _radius;
        }

        private int SlotIndex(int3 coordinate)
        {
            int x = FloorMod(coordinate.x, _edge);
            int y = FloorMod(coordinate.y, _edge);
            int z = FloorMod(coordinate.z, _edge);
            return x + _edge * (y + _edge * z);
        }

        private uint NextGeneration()
        {
            uint generation = ++_generationCounter;
            if (generation == 0) generation = ++_generationCounter;
            return generation;
        }

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
