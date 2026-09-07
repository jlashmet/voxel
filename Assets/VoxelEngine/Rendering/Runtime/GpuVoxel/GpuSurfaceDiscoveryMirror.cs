using System;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Versioned discovery evidence for GPU presentation queries, never world truth.</summary>
    internal sealed class GpuSurfaceDiscoveryMirror : IDisposable
    {
        private const int Frames = 3;
        private const int MapCapacity = SurfaceDiscoveryCoverage.MaximumRegions * 2;
        private readonly ComputeShader _shader;
        private readonly ComputeBuffer[] _words = new ComputeBuffer[Frames];
        private readonly ComputeBuffer[] _map = new ComputeBuffer[Frames];
        private readonly ulong[] _versions = new ulong[Frames];
        private readonly uint[] _staging = new uint[SurfaceDiscoveryCoverage.MaximumRegions * SurfaceDiscoveryCoverage.GpuWordsPerRegion];
        private SurfaceDiscoveryCoverage _source;
        private int _slot = -1, _count;
        private readonly int _clear, _index;
        private bool _disposed;
        internal int UploadCount { get; private set; }
        internal long ResidentBytes => (long)Frames * (_staging.Length + MapCapacity) * sizeof(uint);

        internal GpuSurfaceDiscoveryMirror(ComputeShader shader)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            _clear = shader.FindKernel("CSClearRegionMap");
            _index = shader.FindKernel("CSIndexRegions");
            try
            {
                for (int i = 0; i < Frames; i++)
                {
                    _words[i] = new ComputeBuffer(_staging.Length, sizeof(uint));
                    _map[i] = new ComputeBuffer(MapCapacity, sizeof(uint));
                }
            }
            catch { Dispose(); throw; }
        }

        internal void Prepare(SurfaceDiscoveryCoverage source, int frame)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceDiscoveryMirror));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!ReferenceEquals(source, _source))
            {
                _source = source;
                Array.Clear(_versions, 0, _versions.Length);
            }
            _slot = (frame % Frames + Frames) % Frames;
            _count = source.Count;
            if (_versions[_slot] == source.Version) return;
            int count = source.CopyGpuWords(_staging);
            if (count > 0) _words[_slot].SetData(_staging, 0, 0, count * SurfaceDiscoveryCoverage.GpuWordsPerRegion);
            Bind(_shader, _clear);
            _shader.Dispatch(_clear, (MapCapacity + 63) / 64, 1, 1);
            if (count > 0)
            {
                Bind(_shader, _index);
                _shader.Dispatch(_index, (count + 63) / 64, 1, 1);
            }
            _versions[_slot] = source.Version;
            UploadCount++;
        }

        internal void Bind(ComputeShader shader, int kernel)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceDiscoveryMirror));
            if (_slot < 0) throw new InvalidOperationException("Prepare discovery before querying it.");
            shader.SetInt("_DiscoveryRegionCount", _count);
            shader.SetInt("_DiscoveryMapCapacity", MapCapacity);
            shader.SetBuffer(kernel, "_DiscoveryWords", _words[_slot]);
            shader.SetBuffer(kernel, "_DiscoveryMap", _map[_slot]);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < Frames; i++) { _words[i]?.Release(); _map[i]?.Release(); }
        }
    }
}
