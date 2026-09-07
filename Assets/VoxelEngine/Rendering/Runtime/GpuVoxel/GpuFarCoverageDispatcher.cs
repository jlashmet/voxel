using System;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>GPU proof of current selected near geometry or completed empty discovery.</summary>
    internal sealed class GpuFarCoverageDispatcher : IDisposable
    {
        private readonly ComputeShader _shader;
        private readonly GpuSurfaceDiscoveryMirror _discovery;
        private readonly ComputeBuffer _map;
        private readonly int _clear, _index, _query;
        private bool _disposed, _prepared;
        private readonly Vector4[] _planes = new Vector4[6];
        internal long ResidentBytes => _discovery.ResidentBytes + (long)_map.count * _map.stride;

        internal GpuFarCoverageDispatcher(ComputeShader shader, ComputeShader discoveryShader, int nodeCapacity)
        {
            if (nodeCapacity <= 0 || nodeCapacity > 65536) throw new ArgumentOutOfRangeException(nameof(nodeCapacity));
            _shader = shader != null ? UnityEngine.Object.Instantiate(shader) : throw new ArgumentNullException(nameof(shader));
            _clear = shader.FindKernel("CSClearCoverage");
            _index = shader.FindKernel("CSIndexCoverage");
            _query = shader.FindKernel("CSQueryCoverage");
            try
            {
                _discovery = new GpuSurfaceDiscoveryMirror(discoveryShader);
                _map = new ComputeBuffer(Mathf.NextPowerOfTwo(nodeCapacity * 2), 4);
            }
            catch { _discovery?.Dispose(); UnityEngine.Object.DestroyImmediate(_shader); throw; }
        }

        // proofAllowed is the host's journal/residency synchronization gate, not GPU world truth.
        internal void Prepare(SurfaceDiscoveryCoverage discovery, ComputeBuffer nodes, int nodeCount,
            ComputeBuffer selected, ComputeBuffer states, int frame, bool proofAllowed, Vector3 cameraPosition,
            float voxelSize, float maximumRadius, Plane[] planes = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarCoverageDispatcher));
            if (nodes == null || selected == null || states == null) throw new ArgumentNullException();
            if (nodeCount < 0 || nodeCount > nodes.count || nodeCount * 2 > _map.count)
                throw new ArgumentOutOfRangeException(nameof(nodeCount));
            _discovery.Prepare(discovery, frame);
            _shader.SetInt("_CoverageNodeCount", nodeCount);
            _shader.SetInt("_CoverageMapCapacity", _map.count);
            _shader.SetInt("_CoverageAllowed", proofAllowed ? 1 : 0);
            _shader.SetInt("_CoverageFrustumEnabled", planes != null ? 1 : 0);
            if(planes != null)
            {
                if(planes.Length != 6)throw new ArgumentException("Six frustum planes are required.",nameof(planes));
                for(int i=0;i<6;i++)_planes[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
                _shader.SetVectorArray("_CoveragePlanes",_planes);
            }
            _shader.SetFloat("_CoverageVoxelSize", voxelSize);
            _shader.SetFloat("_CoverageRadius", maximumRadius);
            _shader.SetVector("_CoverageCamera", cameraPosition);
            _shader.SetBuffer(_clear, "_CoverageMap", _map);
            _shader.Dispatch(_clear, (_map.count + 63) / 64, 1, 1);
            _shader.SetBuffer(_index, "_CoverageMap", _map);
            _shader.SetBuffer(_index, "_CoverageNodes", nodes);
            _shader.SetBuffer(_index, "_CoverageSelected", selected);
            _shader.SetBuffer(_index, "_CoverageNodeStates", states);
            if (nodeCount > 0) _shader.Dispatch(_index, (nodeCount + 63) / 64, 1, 1);
            _shader.SetBuffer(_query, "_CoverageMap", _map);
            _shader.SetBuffer(_query, "_CoverageNodes", nodes);
            _discovery.Bind(_shader, _query);
            _prepared = true;
        }

        internal void Query(ComputeBuffer bounds, ComputeBuffer results, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarCoverageDispatcher));
            if (!_prepared) throw new InvalidOperationException("Prepare coverage before querying it.");
            if (bounds == null || results == null) throw new ArgumentNullException();
            if (count < 0 || count > bounds.count || count > results.count) throw new ArgumentOutOfRangeException(nameof(count));
            _shader.SetInt("_CoverageQueryCount", count);
            _shader.SetBuffer(_query, "_CoverageQueries", bounds);
            _shader.SetBuffer(_query, "_CoverageResults", results);
            if (count > 0) _shader.Dispatch(_query, Math.Min(1024,count), (count+1023)/1024, 1);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _map?.Release(); _discovery?.Dispose();
            UnityEngine.Object.DestroyImmediate(_shader);
        }
    }
}
