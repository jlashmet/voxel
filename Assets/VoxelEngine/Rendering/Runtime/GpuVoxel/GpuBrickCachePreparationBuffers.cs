using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Owns the bounded GPU buffers and CPU staging arrays for one brick-cache preparation batch.
    /// Keeping resource ownership separate leaves <see cref="GpuBrickCachePreparation"/> focused on
    /// request staging and resolver dispatch semantics.
    /// </summary>
    internal sealed class GpuBrickCachePreparationBuffers : IDisposable
    {
        internal readonly int Capacity;
        internal readonly int BricksPerRequest;
        internal readonly ComputeBuffer RequestViews;
        internal readonly ComputeBuffer DirectoryHeader;
        internal readonly ComputeBuffer DenseEntries;
        internal readonly GpuBrickCacheRequestView[] RequestStaging;
        internal readonly uint[] HeaderStaging = new uint[2];

        private bool _disposed;

        internal long CommittedBytes =>
            (long)(Capacity + 1) * GpuBrickCacheRequestView.Stride
            + sizeof(uint) * 2L
            + (long)Capacity * BricksPerRequest * sizeof(uint);

        internal GpuBrickCachePreparationBuffers(int capacity, int edge)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (edge <= 0) throw new ArgumentOutOfRangeException(nameof(edge));

            Capacity = capacity;
            BricksPerRequest = checked(edge * edge * edge);
            // Reserve one extra request view as a permanent terminator. Metal cannot query a
            // StructuredBuffer's size from shader code, so the dense-view lookup walks active
            // records until it reaches this negative OutputBase sentinel instead.
            RequestViews = new ComputeBuffer(capacity + 1, GpuBrickCacheRequestView.Stride,
                                             ComputeBufferType.Structured);
            DirectoryHeader = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);
            DenseEntries = new ComputeBuffer(checked(capacity * BricksPerRequest), sizeof(uint),
                                             ComputeBufferType.Structured);
            RequestStaging = new GpuBrickCacheRequestView[capacity + 1];
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RequestViews?.Release();
            DirectoryHeader?.Release();
            DenseEntries?.Release();
        }
    }
}
