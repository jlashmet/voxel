using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Remembers extraction ownership already retired by a completed graphics fence so the later
    /// worker-side Release call cannot decrement the shared active-reader state a second time.
    /// Counted keys preserve correctness if the same footprint is retired more than once before
    /// its owning workers consume their completion notifications.
    /// </summary>
    internal sealed class GpuFenceRetiredExtractionLedger
    {
        private readonly Dictionary<Key, int> _counts = new();

        internal int Count { get; private set; }

        internal void Record(int3 brickCacheOrigin, int brickCacheEdge)
        {
            if (brickCacheEdge <= 0)
                throw new ArgumentOutOfRangeException(nameof(brickCacheEdge));

            var key = new Key(brickCacheOrigin, brickCacheEdge);
            _counts.TryGetValue(key, out int count);
            _counts[key] = count + 1;
            Count++;
        }

        internal bool TryConsume(int3 brickCacheOrigin, int brickCacheEdge)
        {
            var key = new Key(brickCacheOrigin, brickCacheEdge);
            if (!_counts.TryGetValue(key, out int count) || count <= 0) return false;

            if (count == 1) _counts.Remove(key);
            else _counts[key] = count - 1;
            Count--;
            return true;
        }

        internal void Clear()
        {
            _counts.Clear();
            Count = 0;
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly int3 _origin;
            private readonly int _edge;

            internal Key(int3 origin, int edge)
            {
                _origin = origin;
                _edge = edge;
            }

            public bool Equals(Key other) =>
                _edge == other._edge && math.all(_origin == other._origin);

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(_origin.GetHashCode(), _edge);
        }
    }
}
