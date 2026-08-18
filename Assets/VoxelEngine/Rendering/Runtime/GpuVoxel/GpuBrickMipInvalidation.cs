using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Which occupancy summaries an edit invalidated, and how much of that to rebuild this frame.
    ///
    /// The mip pyramid is over occupancy, not colour, so a ray that misses terminates in a few
    /// steps and a coarse node can be skipped in one instruction. Rebuilding a level is a bitwise OR
    /// of a node's eight children, which is cheap — the expensive mistake is doing it per edit
    /// rather than per frame. Destroying a wall touches hundreds of bricks that share the same
    /// ancestors, and rebuilding those ancestors once per brick would multiply the work by the
    /// number of bricks that happened to fall.
    ///
    /// So this deduplicates. A brick marks its ancestor chain, each level keeps a set rather than a
    /// list, and a frame drains a bounded slice per level. Draining coarse-to-fine is deliberate:
    /// a coarse node is what covers the view while its children are still pending, so it is the one
    /// worth having correct first.
    ///
    /// Sparse by construction. A kilometre-scale world has no dense pyramid to allocate, and the
    /// occupied fraction is tiny.
    /// </summary>
    public sealed class GpuBrickMipInvalidation
    {
        /// <summary>
        /// Summary levels above the bricks themselves. Each halves resolution per axis, so eight
        /// levels reach from one brick to a 256-brick span — past the far edge of any render ring.
        /// </summary>
        public const int DefaultLevelCount = 8;

        private readonly HashSet<int3>[] _dirtyByLevel;
        private readonly List<int3> _drainScratch = new();

        public int LevelCount { get; }

        /// <summary>Distinct nodes waiting to be rebuilt, across every level.</summary>
        public int PendingCount
        {
            get
            {
                int total = 0;
                for (int level = 0; level < LevelCount; level++) total += _dirtyByLevel[level].Count;
                return total;
            }
        }

        /// <summary>Marks that were folded into an existing one rather than adding work.</summary>
        public ulong CoalescedCount { get; private set; }

        public GpuBrickMipInvalidation(int levelCount = DefaultLevelCount)
        {
            if (levelCount <= 0) throw new ArgumentOutOfRangeException(nameof(levelCount));

            LevelCount = levelCount;
            _dirtyByLevel = new HashSet<int3>[levelCount];
            for (int level = 0; level < levelCount; level++) _dirtyByLevel[level] = new HashSet<int3>();
        }

        /// <summary>Node at <paramref name="level"/> that summarises this brick.</summary>
        public static int3 AncestorOf(int3 brick, int level) => new(
            brick.x >> (level + 1),
            brick.y >> (level + 1),
            brick.z >> (level + 1));

        public int PendingAt(int level) => _dirtyByLevel[level].Count;

        public bool IsPending(int level, int3 node) => _dirtyByLevel[level].Contains(node);

        /// <summary>
        /// Records that a brick's content changed, invalidating every summary above it.
        ///
        /// Arithmetic shift keeps this correct for negative coordinates: the world extends both ways
        /// from the origin, and a divide would fold -1 and 0 onto the same parent.
        /// </summary>
        public void MarkBrick(int3 brick)
        {
            for (int level = 0; level < LevelCount; level++)
            {
                if (!_dirtyByLevel[level].Add(AncestorOf(brick, level))) CoalescedCount++;
            }
        }

        public void MarkBricks(IReadOnlyList<int3> bricks)
        {
            for (int i = 0; i < bricks.Count; i++) MarkBrick(bricks[i]);
        }

        /// <summary>
        /// Takes up to <paramref name="budget"/> nodes from a level for rebuilding this frame.
        ///
        /// Drained nodes are removed, so a caller that fails to rebuild one must mark it again
        /// rather than assume it will reappear. Anything still dirty simply waits for a later frame:
        /// a stale coarse summary over-reports occupancy, which costs a few wasted ray steps, and
        /// never under-reports into a hole.
        /// </summary>
        public int Drain(int level, int budget, List<int3> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if ((uint)level >= (uint)LevelCount)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (budget <= 0) return 0;

            HashSet<int3> pending = _dirtyByLevel[level];
            if (pending.Count == 0) return 0;

            _drainScratch.Clear();
            foreach (int3 node in pending)
            {
                _drainScratch.Add(node);
                if (_drainScratch.Count >= budget) break;
            }

            for (int i = 0; i < _drainScratch.Count; i++)
            {
                pending.Remove(_drainScratch[i]);
                destination.Add(_drainScratch[i]);
            }
            return _drainScratch.Count;
        }

        /// <summary>
        /// Drains across every level within one shared budget, coarsest first.
        ///
        /// Coarse nodes are what the renderer falls back to while finer detail is pending, so
        /// spending a tight budget on them keeps coverage honest even when the frame cannot afford
        /// to rebuild everything an edit touched.
        /// </summary>
        public int DrainCoarsestFirst(int budget, List<int3>[] destinationByLevel)
        {
            if (destinationByLevel == null) throw new ArgumentNullException(nameof(destinationByLevel));
            if (destinationByLevel.Length < LevelCount)
                throw new ArgumentException("One destination per level is required.",
                                            nameof(destinationByLevel));

            int drained = 0;
            for (int level = LevelCount - 1; level >= 0 && drained < budget; level--)
                drained += Drain(level, budget - drained, destinationByLevel[level]);
            return drained;
        }

        public void Clear()
        {
            for (int level = 0; level < LevelCount; level++) _dirtyByLevel[level].Clear();
            CoalescedCount = 0;
        }
    }
}
