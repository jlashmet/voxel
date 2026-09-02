using System;
using System.Runtime.InteropServices;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RequestView
        {
            internal int OriginX;
            internal int OriginY;
            internal int OriginZ;
            internal int OutputBase;

            internal const int Stride = sizeof(int) * 4;
        }

        private readonly ComputeShader _shader;
        private readonly int _kernel;
        private readonly int _capacity;
        private readonly int _edge;
        private readonly int _bricksPerRequest;
        private readonly ComputeBuffer _requestViews;
        private readonly ComputeBuffer _directoryHeader;
        private readonly ComputeBuffer _denseEntries;
        private readonly RequestView[] _requestStaging;
        private readonly uint[] _headerStaging = new uint[2];
        private bool _disposed;

        internal ComputeBuffer RequestViews => _requestViews;
        internal ComputeBuffer DenseEntries => _denseEntries;
        internal int BricksPerRequest => _bricksPerRequest;
        internal long CommittedBytes =>
            (long)_capacity * RequestView.Stride
            + sizeof(uint) * 2L
            + (long)_capacity * _bricksPerRequest * sizeof(uint);

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
            _capacity = capacity;
            _edge = edge;
            _bricksPerRequest = checked(edge * edge * edge);
            _requestViews = new ComputeBuffer(capacity, RequestView.Stride,
                                              ComputeBufferType.Structured);
            _directoryHeader = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);
            _denseEntries = new ComputeBuffer(checked(capacity * _bricksPerRequest), sizeof(uint),
                                              ComputeBufferType.Structured);
            _requestStaging = new RequestView[capacity];
        }

        internal void Dispatch(GpuVoxelBrickMirror mirror,
                               GpuChunkExtraction[] requests,
                               int recordCount)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (recordCount <= 0 || recordCount > _capacity || recordCount > requests.Length)
                throw new ArgumentOutOfRangeException(nameof(recordCount));

            for (int i = 0; i < recordCount; i++)
            {
                int3 origin = requests[i].BrickCacheOrigin;
                _requestStaging[i] = new RequestView
                {
                    OriginX = origin.x,
                    OriginY = origin.y,
                    OriginZ = origin.z,
                    OutputBase = checked(i * _bricksPerRequest),
                };
            }
            _requestViews.SetData(_requestStaging, 0, 0, recordCount);

            _headerStaging[0] = unchecked((uint)mirror.DirectoryWordOffset);
            _headerStaging[1] = unchecked((uint)mirror.DirectoryMask);
            _directoryHeader.SetData(_headerStaging);

            _shader.SetBuffer(_kernel, IdBrickMaterials, mirror.Materials);
            _shader.SetBuffer(_kernel, IdPersistentLookupHeader, _directoryHeader);
            _shader.SetBuffer(_kernel, IdRequests, _requestViews);
            _shader.SetBuffer(_kernel, IdWrite, _denseEntries);
            _shader.SetInt(IdEdge, _edge);
            _shader.SetInt(IdRequestCount, recordCount);
            _shader.Dispatch(_kernel, (_bricksPerRequest + ThreadGroupSize - 1) / ThreadGroupSize,
                             recordCount, 1);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _requestViews?.Release();
            _directoryHeader?.Release();
            _denseEntries?.Release();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuBrickCachePreparation));
        }
    }
}
