using System;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Resolves persistent world-brick directory entries into compact dense per-request tables.
    ///
    /// Resolution remains entirely on the GPU: the CPU uploads only the bounded request views and
    /// two directory metadata words. One dispatch prepares the whole extraction batch, so persistent
    /// hash traversal is removed from the density/meshing compilation unit without reintroducing a
    /// per-chunk preparation submission or CPU voxel-neighbourhood flattening.
    /// </summary>
    internal sealed class GpuBrickCachePreparation : IDisposable
    {
        internal const string ShaderResourcePath = "VoxelBrickCacheResolver";
        private const int ThreadGroupSize = 64;

        private static readonly int IdBrickMaterials = Shader.PropertyToID("_BrickMaterials");
        private static readonly int IdPersistentLookupHeader =
            Shader.PropertyToID("_PersistentLookupHeader");
        private static readonly int IdRequests =
            Shader.PropertyToID("_ResolvedBrickCacheRequests");
        private static readonly int IdWrite =
            Shader.PropertyToID("_ResolvedBrickCacheWrite");
        private static readonly int IdEdge =
            Shader.PropertyToID("_ResolvedBrickCacheEdge");
        private static readonly int IdRequestCount =
            Shader.PropertyToID("_ResolvedBrickCacheRequestCount");

        private readonly ComputeShader _shader;
        private readonly int _kernel;
        private readonly int _edge;
        private readonly GpuBrickCachePreparationBuffers _buffers;
        private bool _disposed;

        internal ComputeBuffer RequestViews => _buffers.RequestViews;
        internal ComputeBuffer DenseEntries => _buffers.DenseEntries;
        internal int BricksPerRequest => _buffers.BricksPerRequest;
        internal long CommittedBytes => _buffers.CommittedBytes;

        internal GpuBrickCachePreparation(int capacity, int edge, ComputeShader shader = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (edge <= 0) throw new ArgumentOutOfRangeException(nameof(edge));

            shader ??= Resources.Load<ComputeShader>(ShaderResourcePath);
            if (shader == null)
                throw new InvalidOperationException(
                    $"Missing Resources/{ShaderResourcePath}.compute for GPU brick-cache preparation.");

            _shader = shader;
            _kernel = shader.FindKernel("CSResolveBrickCache");
            _edge = edge;
            _buffers = new GpuBrickCachePreparationBuffers(capacity, edge);
        }

        internal void Dispatch(GpuVoxelBrickMirror mirror,
                               GpuChunkExtraction[] requests,
                               int recordCount)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (recordCount <= 0 || recordCount > _buffers.Capacity || recordCount > requests.Length)
                throw new ArgumentOutOfRangeException(nameof(recordCount));

            for (int i = 0; i < recordCount; i++)
            {
                int3 origin = requests[i].BrickCacheOrigin;
                _buffers.RequestStaging[i] = new GpuBrickCacheRequestView
                {
                    OriginX = origin.x,
                    OriginY = origin.y,
                    OriginZ = origin.z,
                    OutputBase = checked(i * _buffers.BricksPerRequest),
                };
            }
            _buffers.RequestViews.SetData(_buffers.RequestStaging, 0, 0, recordCount);

            _buffers.HeaderStaging[0] = unchecked((uint)mirror.DirectoryWordOffset);
            _buffers.HeaderStaging[1] = unchecked((uint)mirror.DirectoryMask);
            _buffers.DirectoryHeader.SetData(_buffers.HeaderStaging);

            _shader.SetBuffer(_kernel, IdBrickMaterials, mirror.Materials);
            _shader.SetBuffer(_kernel, IdPersistentLookupHeader, _buffers.DirectoryHeader);
            _shader.SetBuffer(_kernel, IdRequests, _buffers.RequestViews);
            _shader.SetBuffer(_kernel, IdWrite, _buffers.DenseEntries);
            _shader.SetInt(IdEdge, _edge);
            _shader.SetInt(IdRequestCount, recordCount);
            _shader.Dispatch(_kernel,
                             (_buffers.BricksPerRequest + ThreadGroupSize - 1) / ThreadGroupSize,
                             recordCount, 1);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _buffers?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuBrickCachePreparation));
        }
    }
}
