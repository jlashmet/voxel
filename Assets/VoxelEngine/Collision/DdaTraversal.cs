using System;
using Unity.Mathematics;

namespace VoxelEngine.Collision
{
    /// <summary>
    /// Shared deterministic integer line traversal used by authoritative collision queries.
    /// Residency and voxel storage are deliberately outside this traversal; callers combine the
    /// visited coordinates with the Storage read contract appropriate to their query.
    /// </summary>
    public static class DdaTraversal
    {
        /// <summary>Traverse all coordinates intersected by a line segment from start to end.</summary>
        public static void Traverse(int3 start, int3 end, Action<int3> visit)
        {
            Walk(start, end, c => { visit(c); return true; });
        }

        /// <summary>Traverse until the visitor returns false.</summary>
        public static void Traverse(int3 start, int3 end, Func<int3, bool> visit)
        {
            Walk(start, end, visit);
        }

        /// <summary>Allocation-free cursor over the authoritative integer walk.</summary>
        public struct Cursor
        {
            private int3 _current;
            private int _sx, _sy, _sz;
            private int _absDx, _absDy, _absDz;
            private int _dominantAxis;
            private int _errA, _errB;
            private int _remaining;
            private bool _started;

            public int3 Current => _current;
            public int3 EntryNormal { get; private set; }

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

                if (c._absDx == dominant) c._dominantAxis = 0;
                else if (c._absDy == dominant) c._dominantAxis = 1;
                else c._dominantAxis = 2;

                switch (c._dominantAxis)
                {
                    case 0:
                        c._errA = 2 * c._absDy - c._absDx;
                        c._errB = 2 * c._absDz - c._absDx;
                        break;
                    case 1:
                        c._errA = 2 * c._absDx - c._absDy;
                        c._errB = 2 * c._absDz - c._absDy;
                        break;
                    default:
                        c._errA = 2 * c._absDx - c._absDz;
                        c._errB = 2 * c._absDy - c._absDz;
                        break;
                }

                return c;
            }

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
