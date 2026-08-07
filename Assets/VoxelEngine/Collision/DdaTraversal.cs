using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Collision
{
    /// <summary>
    /// Shared Digital Differential Analysis (DDA) traversal used by both raycast and render
    /// raymarch. Iterates through bricks along a line or segment in voxel space using integer-
    /// only stepping, respecting region boundaries so that non-resident regions are read as
    /// empty (never throw).
    ///
    /// This is the single point of truth for all linear traversal across brick coordinates.
    /// Both <see cref="VoxelRaycast"/> and the rendering raymarch use this same algorithm,
    /// guaranteeing visual/collision parity (Constitution Principle II: Single source of truth).
    ///
    /// The algorithm is a 3D DDA (Amanatides &amp; Woo, "Fast Ray Tracing Using Hyper-Rectangular
    /// Grids", 1988) adapted for discrete brick coordinates with region boundary awareness.
    /// All stepping is integer arithmetic — no floating point enters the traversal loop
    /// (Constitution Principle I: Determinism).
    /// </summary>
    public static class DdaTraversal
    {
        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Traverse all brick coordinates intersected by a line segment from start to end.
        ///
        /// Each visited brick coordinate is passed to the visitor callback. The traversal
        /// respects region boundaries: bricks in non-resident regions are invisible to the
        /// visitor (the region's occupancy reads as all-zero).
        /// </summary>
        /// <param name="start">Starting voxel coordinate of the ray segment.</param>
        /// <param name="end">Ending voxel coordinate of the ray segment.</param>
        /// <param name="table">Region table providing residency checks for each visited brick.</param>
        /// <param name="visit">Callback invoked for every brick coordinate intersected by the segment.</param>
        public static void Traverse(int3 start, int3 end, in RegionTable table, Action<int3> visit)
        {
            Walk(start, end, c => { visit(c); return true; });
        }

        /// <summary>
        /// Traverse all brick coordinates intersected by a line segment from start to end,
        /// stopping early when the visitor returns false.
        /// </summary>
        /// <param name="start">Starting voxel coordinate.</param>
        /// <param name="end">Ending voxel coordinate.</param>
        /// <param name="table">Region table for residency checks.</param>
        /// <param name="visit">Callback returning true to continue, false to stop early.</param>
        public static void Traverse(int3 start, int3 end, in RegionTable table, Func<int3, bool> visit)
        {
            Walk(start, end, visit);
        }

        // -- cursor ---------------------------------------------------------------

        /// <summary>
        /// Allocation-free cursor over the same integer walk the callback overloads use.
        ///
        /// This is the form both <see cref="VoxelRaycast"/> and the render raymarch drive.
        /// The callback overloads are convenient but take a delegate, which allocates and
        /// cannot be Bursted; a struct cursor keeps the *one* traversal (Constitution
        /// Principle II) usable from the hot paths that actually matter.
        ///
        /// Usage:
        /// <code>
        /// var cursor = DdaTraversal.Cursor.Between(start, end);
        /// while (cursor.MoveNext()) { var brick = cursor.Current; ... }
        /// </code>
        /// </summary>
        public struct Cursor
        {
            private int3 _current;
            private int _sx, _sy, _sz;
            private int _absDx, _absDy, _absDz;
            private int _dominantAxis;
            private int _errA, _errB;
            private int _remaining;
            private bool _started;

            /// <summary>The brick coordinate the cursor currently sits on.</summary>
            public int3 Current => _current;

            /// <summary>
            /// Face normal of the axis crossed to enter <see cref="Current"/>, pointing back
            /// along the ray. Zero on the first brick, which was entered from nowhere.
            /// </summary>
            public int3 EntryNormal { get; private set; }

            /// <summary>Builds a cursor walking from start to end, inclusive of both.</summary>
            public static Cursor Between(int3 start, int3 end)
            {
                var c = default(Cursor);
                c._current = start;
                c._started = false;
                c.EntryNormal = int3.zero;

                int dx = end.x - start.x;
                int dy = end.y - start.y;
                int dz = end.z - start.z;

                c._absDx = math.abs(dx);
                c._absDy = math.abs(dy);
                c._absDz = math.abs(dz);

                c._sx = dx >= 0 ? 1 : -1;
                c._sy = dy >= 0 ? 1 : -1;
                c._sz = dz >= 0 ? 1 : -1;

                int dominant = math.max(c._absDx, math.max(c._absDy, c._absDz));
                c._remaining = dominant;

                // Ties resolve x > y > z so that every caller picks the same dominant axis.
                if (c._absDx == dominant) c._dominantAxis = 0;
                else if (c._absDy == dominant) c._dominantAxis = 1;
                else c._dominantAxis = 2;

                switch (c._dominantAxis)
                {
                    case 0:
                        c._errA = 2 * c._absDy - c._absDx; // y
                        c._errB = 2 * c._absDz - c._absDx; // z
                        break;
                    case 1:
                        c._errA = 2 * c._absDx - c._absDy; // x
                        c._errB = 2 * c._absDz - c._absDy; // z
                        break;
                    default:
                        c._errA = 2 * c._absDx - c._absDz; // x
                        c._errB = 2 * c._absDy - c._absDz; // y
                        break;
                }

                return c;
            }

            /// <summary>
            /// Advances to the next brick. Returns false once the endpoint is passed.
            /// The first call yields the start brick without stepping.
            /// </summary>
            public bool MoveNext()
            {
                if (!_started)
                {
                    _started = true;
                    return true;
                }

                if (_remaining <= 0) return false;
                _remaining--;

                switch (_dominantAxis)
                {
                    case 0:
                        if (_errA > 0) { _current.y += _sy; _errA -= 2 * _absDx; }
                        if (_errB > 0) { _current.z += _sz; _errB -= 2 * _absDx; }
                        _errA += 2 * _absDy;
                        _errB += 2 * _absDz;
                        _current.x += _sx;
                        EntryNormal = new int3(-_sx, 0, 0);
                        break;

                    case 1:
                        if (_errA > 0) { _current.x += _sx; _errA -= 2 * _absDy; }
                        if (_errB > 0) { _current.z += _sz; _errB -= 2 * _absDy; }
                        _errA += 2 * _absDx;
                        _errB += 2 * _absDz;
                        _current.y += _sy;
                        EntryNormal = new int3(0, -_sy, 0);
                        break;

                    default:
                        if (_errA > 0) { _current.x += _sx; _errA -= 2 * _absDz; }
                        if (_errB > 0) { _current.y += _sy; _errB -= 2 * _absDz; }
                        _errA += 2 * _absDx;
                        _errB += 2 * _absDy;
                        _current.z += _sz;
                        EntryNormal = new int3(0, 0, -_sz);
                        break;
                }

                return true;
            }
        }

        // -- integer traversal core -----------------------------------------------

        /// <summary>
        /// Exact integer 3D line walk from <paramref name="start"/> to <paramref name="end"/>,
        /// inclusive of both endpoints.
        ///
        /// Integer Bresenham rather than a float DDA. This is the single traversal both the
        /// collision raycast and the render raymarch derive from (Constitution Principle II),
        /// so any float in here would let two machines — or the CPU and the GPU — disagree
        /// about which voxel a ray hit. The error accumulators below are exact: identical
        /// inputs visit an identical sequence of bricks on every target.
        ///
        /// The dominant axis advances every step; the two minor axes advance when their
        /// accumulated error crosses half the dominant delta.
        /// </summary>
        private static void Walk(int3 start, int3 end, Func<int3, bool> visit)
        {
            var cursor = Cursor.Between(start, end);
            while (cursor.MoveNext())
            {
                if (!visit(cursor.Current)) return;
            }
        }
    }
}
