using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// GPU-owned presentation arena for near-ring chunks. CPU handles identify authoritative
    /// chunks, but all size-dependent state remains on the GPU: page allocation, active/staging
    /// bank selection, pending candidates, explicit publication, stale rejection, and reclamation.
    /// </summary>
    internal sealed class GpuSurfacePageArena : IDisposable
    {
        internal const int VertexPageSize = 1024;
        internal const int IndexPageSize = 2048;
        internal const int MaxVertexPagesPerChunk = 512;
        internal const int MaxIndexPagesPerChunk = 512;
        internal const int RetirementDelayFrames = 4;
        private const int ArenaStateWords = 7;
        private const int ChunkRecordWords = 8;
        private const int RetiredPageWords = 2;
        private const int HandleCommandCapacity = 1024;
        private const int ThreadGroupSize = 64;

        private enum HandleState : byte
        {
            Free,
            Acquired,
            ReleaseQueued,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HandleCommand
        {
            internal uint Handle;
            internal uint GenerationLow;
            internal uint GenerationHigh;
            internal uint Release;
            internal const int Stride = sizeof(uint) * 4;
        }

        private static readonly int IdBatchChunks = Shader.PropertyToID("_BatchChunks");
        private static readonly int IdBatchCounters = Shader.PropertyToID("_BatchCounters");
        private static readonly int IdBatchCountersRead = Shader.PropertyToID("_BatchCountersRead");
        private static readonly int IdArenaState = Shader.PropertyToID("_ArenaState");
        private static readonly int IdFreeVertexPages = Shader.PropertyToID("_FreeVertexPages");
        private static readonly int IdFreeIndexPages = Shader.PropertyToID("_FreeIndexPages");
        private static readonly int IdRetiredVertexPages = Shader.PropertyToID("_RetiredVertexPages");
        private static readonly int IdRetiredIndexPages = Shader.PropertyToID("_RetiredIndexPages");
        private static readonly int IdRetiredVertexPagesRead =
            Shader.PropertyToID("_RetiredVertexPagesRead");
        private static readonly int IdRetiredIndexPagesRead =
            Shader.PropertyToID("_RetiredIndexPagesRead");
        private static readonly int IdDesiredGenerations = Shader.PropertyToID("_DesiredGenerations");
        private static readonly int IdDesiredGenerationsRead =
            Shader.PropertyToID("_DesiredGenerationsRead");
        private static readonly int IdLiveChunkGeometry = Shader.PropertyToID("_LiveChunkGeometry");
        private static readonly int IdPendingChunkGeometry = Shader.PropertyToID("_PendingChunkGeometry");
        private static readonly int IdVertexPageTable = Shader.PropertyToID("_VertexPageTable");
        private static readonly int IdIndexPageTable = Shader.PropertyToID("_IndexPageTable");
        private static readonly int IdLiveChunkGeometryRead =
            Shader.PropertyToID("_LiveChunkGeometryRead");
        private static readonly int IdPendingChunkGeometryRead =
            Shader.PropertyToID("_PendingChunkGeometryRead");
        private static readonly int IdVertexPageTableRead =
            Shader.PropertyToID("_VertexPageTableRead");
        private static readonly int IdIndexPageTableRead =
            Shader.PropertyToID("_IndexPageTableRead");
        private static readonly int IdHandleCommands = Shader.PropertyToID("_HandleCommands");
        private static readonly int IdBatchRecordCount = Shader.PropertyToID("_BatchRecordCount");
        private static readonly int IdBatchRecordWords = Shader.PropertyToID("_BatchRecordWords");
        private static readonly int IdVertexPageSize = Shader.PropertyToID("_VertexPageSize");
        private static readonly int IdIndexPageSize = Shader.PropertyToID("_IndexPageSize");
        private static readonly int IdMaxVertexPages = Shader.PropertyToID("_MaxVertexPagesPerChunk");
        private static readonly int IdMaxIndexPages = Shader.PropertyToID("_MaxIndexPagesPerChunk");
        private static readonly int IdVertexPageCount = Shader.PropertyToID("_VertexPageCount");
        private static readonly int IdIndexPageCount = Shader.PropertyToID("_IndexPageCount");
        private static readonly int IdArenaEpoch = Shader.PropertyToID("_ArenaEpoch");
        private static readonly int IdRetirementDelay = Shader.PropertyToID("_RetirementDelay");
        private static readonly int IdHandleCommandCount = Shader.PropertyToID("_HandleCommandCount");
        private static readonly int IdPendingHandle = Shader.PropertyToID("_PendingHandle");
        private static readonly int IdPendingGenerationLow = Shader.PropertyToID("_PendingGenerationLow");
        private static readonly int IdPendingGenerationHigh = Shader.PropertyToID("_PendingGenerationHigh");

        private readonly ComputeShader _shader;
        private readonly int _allocateKernel;
        private readonly int _publishKernel;
        private readonly int _commitKernel;
        private readonly int _abortKernel;
        private readonly int _handleKernel;
        private readonly Stack<int> _freeHandles;
        private readonly HandleState[] _handleStates;
        private readonly List<int> _releasedHandles = new(HandleCommandCapacity);
        private readonly Dictionary<int, int> _commandIndexByHandle = new(HandleCommandCapacity);
        private readonly HandleCommand[] _commandStaging = new HandleCommand[HandleCommandCapacity];
        private int _commandCount;
        private bool _disposed;

        internal readonly int HandleCapacity;
        internal readonly int VertexPageCount;
        internal readonly int IndexPageCount;
        internal readonly ComputeBuffer Vertices;
        internal readonly ComputeBuffer Indices;
        internal readonly ComputeBuffer ArenaState;
        internal readonly ComputeBuffer FreeVertexPages;
        internal readonly ComputeBuffer FreeIndexPages;
        internal readonly ComputeBuffer RetiredVertexPages;
        internal readonly ComputeBuffer RetiredIndexPages;
        internal readonly ComputeBuffer DesiredGenerations;
        internal readonly ComputeBuffer LiveChunkGeometry;
        internal readonly ComputeBuffer PendingChunkGeometry;
        internal readonly ComputeBuffer VertexPageTable;
        internal readonly ComputeBuffer IndexPageTable;
        internal readonly ComputeBuffer HandleCommands;

        internal GpuSurfacePageArena(ComputeShader shader, int vertexCapacity,
                                     int indexCapacity, int handleCapacity)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            if (vertexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(vertexCapacity));
            if (indexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(indexCapacity));
            if (handleCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(handleCapacity));
            HandleCapacity = handleCapacity;
            VertexPageCount = Math.Max(1, vertexCapacity / VertexPageSize);
            IndexPageCount = Math.Max(1, indexCapacity / IndexPageSize);
            _allocateKernel = shader.FindKernel("CSAllocateBatchPages");
            _publishKernel = shader.FindKernel("CSPublishBatchPages");
            _commitKernel = shader.FindKernel("CSCommitPendingPages");
            _abortKernel = shader.FindKernel("CSAbortPendingPages");
            _handleKernel = shader.FindKernel("CSApplyHandleCommands");

            Vertices = new ComputeBuffer(VertexPageCount * VertexPageSize,
                GpuSurfaceExtractor.ReadbackVertex.Stride, ComputeBufferType.Structured);
            Indices = new ComputeBuffer(IndexPageCount * IndexPageSize,
                sizeof(uint), ComputeBufferType.Structured);
            ArenaState = new ComputeBuffer(ArenaStateWords, sizeof(uint), ComputeBufferType.Structured);
            FreeVertexPages = new ComputeBuffer(VertexPageCount, sizeof(uint), ComputeBufferType.Structured);
            FreeIndexPages = new ComputeBuffer(IndexPageCount, sizeof(uint), ComputeBufferType.Structured);
            RetiredVertexPages = new ComputeBuffer(VertexPageCount, RetiredPageWords * sizeof(uint), ComputeBufferType.Structured);
            RetiredIndexPages = new ComputeBuffer(IndexPageCount, RetiredPageWords * sizeof(uint), ComputeBufferType.Structured);
            DesiredGenerations = new ComputeBuffer(handleCapacity, sizeof(uint) * 2, ComputeBufferType.Structured);
            LiveChunkGeometry = new ComputeBuffer(handleCapacity, ChunkRecordWords * sizeof(uint), ComputeBufferType.Structured);
            PendingChunkGeometry = new ComputeBuffer(handleCapacity, ChunkRecordWords * sizeof(uint), ComputeBufferType.Structured);
            VertexPageTable = new ComputeBuffer(handleCapacity * 2 * MaxVertexPagesPerChunk,
                sizeof(uint), ComputeBufferType.Structured);
            IndexPageTable = new ComputeBuffer(handleCapacity * 2 * MaxIndexPagesPerChunk,
                sizeof(uint), ComputeBufferType.Structured);
            HandleCommands = new ComputeBuffer(HandleCommandCapacity, HandleCommand.Stride,
                ComputeBufferType.Structured);

            var vertexPages = new uint[VertexPageCount];
            var indexPages = new uint[IndexPageCount];
            for (int i = 0; i < vertexPages.Length; i++) vertexPages[i] = (uint)i;
            for (int i = 0; i < indexPages.Length; i++) indexPages[i] = (uint)i;
            FreeVertexPages.SetData(vertexPages);
            FreeIndexPages.SetData(indexPages);
            ArenaState.SetData(new uint[]
            {
                (uint)VertexPageCount, (uint)IndexPageCount, 0, 0, 0, 0, 0
            });
            var zeroRecords = new uint[handleCapacity * ChunkRecordWords];
            LiveChunkGeometry.SetData(zeroRecords);
            PendingChunkGeometry.SetData(zeroRecords);
            DesiredGenerations.SetData(new uint[handleCapacity * 2]);

            _freeHandles = new Stack<int>(handleCapacity);
            _handleStates = new HandleState[handleCapacity];
            for (int handle = handleCapacity - 1; handle >= 0; handle--) _freeHandles.Push(handle);
            BindAllKernels();
        }

        internal bool TryAcquireHandle(out int handle)
        {
            ThrowIfDisposed();
            if (_freeHandles.Count == 0) { handle = -1; return false; }
            handle = _freeHandles.Pop();
            _handleStates[handle] = HandleState.Acquired;
            return true;
        }

        internal void QueueGeneration(int handle, ulong generation)
        {
            ValidateHandle(handle);
            if (_handleStates[handle] != HandleState.Acquired)
                throw new InvalidOperationException(
                    "A GPU generation command requires an acquired handle without a queued release.");
            QueueCommand(handle, generation, release: false);
        }

        internal void QueueRelease(int handle, ulong generation)
        {
            ValidateHandle(handle);
            // Release is terminal for this host acquisition. Duplicate calls before or after
            // its flush must not enqueue duplicate GPU writers or return the handle twice.
            // Reincarnation/late-GPU-command safety still requires the request identity contract;
            // this state tracks host ownership, not completion of submitted GPU work.
            if (_handleStates[handle] != HandleState.Acquired) return;
            QueueCommand(handle, generation, release: true);
            _handleStates[handle] = HandleState.ReleaseQueued;
            _releasedHandles.Add(handle);
        }

        internal void FlushHandleCommands(int frame)
        {
            ThrowIfDisposed();
            if (_commandCount == 0) return;
            HandleCommands.SetData(_commandStaging, 0, 0, _commandCount);
            SetEpoch(frame);
            _shader.SetInt(IdHandleCommandCount, _commandCount);
            _shader.Dispatch(_handleKernel, (_commandCount + ThreadGroupSize - 1) / ThreadGroupSize, 1, 1);
            _commandCount = 0;
            _commandIndexByHandle.Clear();
            for (int i = 0; i < _releasedHandles.Count; i++)
            {
                int handle = _releasedHandles[i];
                _handleStates[handle] = HandleState.Free;
                _freeHandles.Push(handle);
            }
            _releasedHandles.Clear();
        }

        internal void AllocateBatch(ComputeBuffer descriptors, ComputeBuffer counters,
                                    int recordCount, int recordWords, int frame)
        {
            ValidateBatch(descriptors, counters, recordCount, recordWords);
            SetEpoch(frame);
            _shader.SetBuffer(_allocateKernel, IdBatchChunks, descriptors);
            _shader.SetBuffer(_allocateKernel, IdBatchCounters, counters);
            _shader.SetBuffer(_allocateKernel, IdBatchCountersRead, counters);
            _shader.SetInt(IdBatchRecordCount, recordCount);
            _shader.SetInt(IdBatchRecordWords, recordWords);
            _shader.Dispatch(_allocateKernel, 1, 1, 1);
        }

        /// <summary>
        /// Finalizes a written batch without making it live. The compute kernel leaves a current
        /// candidate in PendingChunkGeometry and converts a superseded candidate to Stale. The CPU
        /// must later call CommitPending or AbortPending for the exact renderer generation.
        /// </summary>
        internal void PublishBatch(ComputeBuffer descriptors, ComputeBuffer counters,
                                   int recordCount, int recordWords, int frame)
        {
            ValidateBatch(descriptors, counters, recordCount, recordWords);
            SetEpoch(frame);
            _shader.SetBuffer(_publishKernel, IdBatchChunks, descriptors);
            _shader.SetBuffer(_publishKernel, IdBatchCounters, counters);
            _shader.SetBuffer(_publishKernel, IdBatchCountersRead, counters);
            _shader.SetInt(IdBatchRecordCount, recordCount);
            _shader.SetInt(IdBatchRecordWords, recordWords);
            _shader.Dispatch(_publishKernel, 1, 1, 1);
        }

        internal void CommitPending(int handle, ulong generation, int frame) =>
            ResolvePending(_commitKernel, handle, generation, frame);

        internal void AbortPending(int handle, ulong generation, int frame) =>
            ResolvePending(_abortKernel, handle, generation, frame);

        private void ResolvePending(int kernel, int handle, ulong generation, int frame)
        {
            ValidateHandle(handle);
            SetEpoch(frame);
            _shader.SetInt(IdPendingHandle, handle);
            _shader.SetInt(IdPendingGenerationLow, unchecked((int)(uint)generation));
            _shader.SetInt(IdPendingGenerationHigh, unchecked((int)(uint)(generation >> 32)));
            _shader.Dispatch(kernel, 1, 1, 1);
        }

        private void BindAllKernels()
        {
            int[] kernels =
            {
                _allocateKernel, _publishKernel, _commitKernel, _abortKernel, _handleKernel
            };
            foreach (int kernel in kernels)
            {
                _shader.SetBuffer(kernel, IdArenaState, ArenaState);
                _shader.SetBuffer(kernel, IdFreeVertexPages, FreeVertexPages);
                _shader.SetBuffer(kernel, IdFreeIndexPages, FreeIndexPages);
                _shader.SetBuffer(kernel, IdRetiredVertexPages, RetiredVertexPages);
                _shader.SetBuffer(kernel, IdRetiredIndexPages, RetiredIndexPages);
                _shader.SetBuffer(kernel, IdRetiredVertexPagesRead, RetiredVertexPages);
                _shader.SetBuffer(kernel, IdRetiredIndexPagesRead, RetiredIndexPages);
                _shader.SetBuffer(kernel, IdDesiredGenerations, DesiredGenerations);
                _shader.SetBuffer(kernel, IdDesiredGenerationsRead, DesiredGenerations);
                _shader.SetBuffer(kernel, IdLiveChunkGeometry, LiveChunkGeometry);
                _shader.SetBuffer(kernel, IdPendingChunkGeometry, PendingChunkGeometry);
                _shader.SetBuffer(kernel, IdLiveChunkGeometryRead, LiveChunkGeometry);
                _shader.SetBuffer(kernel, IdPendingChunkGeometryRead, PendingChunkGeometry);
                _shader.SetBuffer(kernel, IdVertexPageTable, VertexPageTable);
                _shader.SetBuffer(kernel, IdIndexPageTable, IndexPageTable);
                _shader.SetBuffer(kernel, IdVertexPageTableRead, VertexPageTable);
                _shader.SetBuffer(kernel, IdIndexPageTableRead, IndexPageTable);
                _shader.SetBuffer(kernel, IdHandleCommands, HandleCommands);
            }
            _shader.SetInt(IdVertexPageSize, VertexPageSize);
            _shader.SetInt(IdIndexPageSize, IndexPageSize);
            _shader.SetInt(IdMaxVertexPages, MaxVertexPagesPerChunk);
            _shader.SetInt(IdMaxIndexPages, MaxIndexPagesPerChunk);
            _shader.SetInt(IdVertexPageCount, VertexPageCount);
            _shader.SetInt(IdIndexPageCount, IndexPageCount);
            _shader.SetInt(IdRetirementDelay, RetirementDelayFrames);
        }

        private void QueueCommand(int handle, ulong generation, bool release)
        {
            var command = new HandleCommand
            {
                Handle = (uint)handle,
                GenerationLow = (uint)generation,
                GenerationHigh = (uint)(generation >> 32),
                Release = release ? 1u : 0u,
            };
            if (_commandIndexByHandle.TryGetValue(handle, out int existingIndex))
            {
                // One GPU thread owns each handle per flush. A newer generation may replace
                // an earlier generation; a release may replace those updates. QueueGeneration
                // rejects further writes once release is queued, so cleanup cannot be erased.
                _commandStaging[existingIndex] = command;
                return;
            }

            if (_commandCount == HandleCommandCapacity)
                FlushHandleCommands(Time.frameCount);
            _commandIndexByHandle.Add(handle, _commandCount);
            _commandStaging[_commandCount++] = command;
        }

        private void SetEpoch(int frame) =>
            _shader.SetInt(IdArenaEpoch, unchecked((int)(uint)Math.Max(0, frame)));

        private void ValidateBatch(ComputeBuffer descriptors, ComputeBuffer counters,
                                   int recordCount, int recordWords)
        {
            ThrowIfDisposed();
            if (descriptors == null) throw new ArgumentNullException(nameof(descriptors));
            if (counters == null) throw new ArgumentNullException(nameof(counters));
            if (recordCount <= 0) throw new ArgumentOutOfRangeException(nameof(recordCount));
            if (recordWords < 17) throw new ArgumentOutOfRangeException(nameof(recordWords));
        }

        private void ValidateHandle(int handle)
        {
            ThrowIfDisposed();
            if ((uint)handle >= (uint)HandleCapacity)
                throw new ArgumentOutOfRangeException(nameof(handle));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfacePageArena));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _commandIndexByHandle.Clear();
            _releasedHandles.Clear();
            _freeHandles.Clear();
            Vertices?.Release(); Indices?.Release(); ArenaState?.Release();
            FreeVertexPages?.Release(); FreeIndexPages?.Release();
            RetiredVertexPages?.Release(); RetiredIndexPages?.Release();
            DesiredGenerations?.Release(); LiveChunkGeometry?.Release();
            PendingChunkGeometry?.Release(); VertexPageTable?.Release();
            IndexPageTable?.Release(); HandleCommands?.Release();
        }
    }
}
