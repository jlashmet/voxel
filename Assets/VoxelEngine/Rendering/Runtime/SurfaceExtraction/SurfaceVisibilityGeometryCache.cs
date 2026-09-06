using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Caches only camera/ring geometry, never chunk readiness or world state. A moving query
    /// bypasses insertion; a stationary query warms once and can reuse during ongoing streaming.
    /// </summary>
    internal sealed class SurfaceVisibilityGeometryCache
    {
        private const int MaximumCachedCoordinates = 4096;
        private readonly Dictionary<int3, byte> _classifications = new();
        private readonly Plane[] _planes = new Plane[6];
        private Vector3 _position;
        private float _voxelSize, _inner, _outer;
        private bool _suspended, _hasQuery, _enabled;

        internal void Disable() => _enabled = false;

        internal void Prepare(Plane[] planes, Vector3 position, float voxelSize,
                              float inner, float outer, bool suspended)
        {
            bool same = _hasQuery && _position.Equals(position)
                && _voxelSize.Equals(voxelSize) && _inner.Equals(inner)
                && _outer.Equals(outer) && _suspended == suspended;
            for (int i = 0; i < 6; i++)
                same &= _planes[i].normal.Equals(planes[i].normal)
                     && _planes[i].distance.Equals(planes[i].distance);
            _enabled = same;
            if (same) return;
            _classifications.Clear();
            _hasQuery = true;
            _position = position;
            _voxelSize = voxelSize;
            _inner = inner;
            _outer = outer;
            _suspended = suspended;
            for (int i = 0; i < 6; i++) _planes[i] = planes[i];
        }

        internal bool TryGet(int3 coordinate, out byte classification)
        {
            classification = 0;
            return _enabled && _classifications.TryGetValue(coordinate, out classification);
        }

        internal void Store(int3 coordinate, byte classification)
        {
            if (_enabled && _classifications.Count < MaximumCachedCoordinates)
                _classifications[coordinate] = classification;
        }
    }
}
