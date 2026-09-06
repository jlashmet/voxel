using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// World-scoped, demand-filled GPU voxel mirror used by all near-ring surface workers.
    ///
    /// Chunk admission only names the brick footprint it needs. This coordinator copies those
    /// bricks from one borrowed region view under a frame-time and upload-byte budget, then keeps
    /// them coherent from Storage's compact change journal. It never walks a whole 512-voxel-edge
    /// region to satisfy a 64-voxel chunk and never builds a per-worker CPU snapshot.
    /// </summary>
    internal static class GpuSurfaceMirrorCoordinator
    {
        private const long MinimumSharedMirrorBudgetBytes = 96L * 1024L * 1024L;
        private const int ChangeRecordsPerFrame = 128;
        private const int TrackedBlockCapacity = 65536;
        private const int TrackedRegionCapacity = 128;
        private const int BlocksPerTrackedRegionCapacity = 8192;
        private const int CoverageChecksPerPoll = 128;
        // Two descriptors keep one indivisible Metal count/write chain inside the presentation
        // budget once the dense scene itself consumes most of the GPU frame. Four lanes retain
        // eight-way mirror/admission concurrency without turning all eight chunks into one long
        // queue head that rendering cannot pre-empt.
        private const int CountBatchCapacity = 2;
        private const int CountBatchLaneCount = 4;
        internal const int MaxConcurrentExtractionChains =
            CountBatchCapacity * CountBatchLaneCount;
        private const int CountBatchMaxFillFrames = 2;
        internal const int DefaultUploadBudgetBytes = 256 * 1024;

        private static readonly Queue<int3> s_RecoveryRegions = new(TrackedRegionCapacity);
        private static readonly HashSet<int3> s_QueuedRecoveryRegions = new(TrackedRegionCapacity);
        private static readonly Dictionary<int3, Queue<int3>> s_PendingBlocksByRegion =
            new(TrackedRegionCapacity);
        private static readonly Dictionary<ActiveFootprint, int> s_DemandFootprints =
            new(32);
        private static readonly HashSet<int3> s_PendingBlocks = new(TrackedBlockCapacity);
        private static readonly HashSet<int3> s_ReadyBlocks = new(TrackedBlockCapacity);
        private static readonly Dictionary<int3, HashSet<int3>> s_ReadyBlocksByRegion =
            new(TrackedRegionCapacity);
        private static readonly Stack<Queue<int3>> s_BlockQueuePool =
            new(TrackedRegionCapacity);
        private static readonly Stack<HashSet<int3>> s_ReadySetPool =
            new(TrackedRegionCapacity);
        private static readonly List<int3> s_ChangedReadyScratch =
            new(BlocksPerTrackedRegionCapacity);
        private static readonly Queue<int3> s_ReadyResidencyOrder = new(TrackedBlockCapacity);
        private static readonly Queue<int3> s_MixedResidencyOrder = new(TrackedBlockCapacity);
        private static readonly HashSet<int3> s_MixedReadyBlocks = new(TrackedBlockCapacity);
        private static readonly Dictionary<int3, ulong> s_RegionLastSolidChangeVersion =
            new(TrackedRegionCapacity);
        private static readonly Dictionary<int3, int> s_ActiveRegionReaders =
            new(TrackedRegionCapacity);
        private static readonly Dictionary<ActiveFootprint, int> s_ActiveFootprints =
            new(32);
        private static readonly List<VoxelChangeRecord> s_Changes = new(ChangeRecordsPerFrame);

        private static GpuVoxelBrickMirror s_Mirror;
        private static GpuSurfacePageArena s_PageArena;
        private static readonly Dictionary<ChunkHandleKey, int> s_ChunkHandles = new();
        private static IRegionReadSource s_Storage;
        private static IVoxelChangeSource s_ChangeSource;
        private static ulong s_ChangeCursor;
        private static ulong s_MirroredVersion;
        private static ulong s_KnownRegionHistoryFromVersion;
        private static int s_ReferenceCount;
        private static int s_ActiveExtractionCount;
        private static int s_LastPrepareFrame = -1;
        private static int s_LastExtractionDispatchFrame = -1;
        private static GraphicsFence s_ExtractionFence;
        private static bool s_ExtractionFenceValid;
        private static uint s_CoverageEpoch;
        private static ulong s_OptionalNonResidentHaloBlocksAccepted;
        private static ulong s_ConcurrentDemandRecoverySlices;
        private static ulong s_CoreNonResidentCoverageChecks;
        private static ulong s_HistoryCoverageRejects;
        private static ulong s_ChangedRegionCoverageRejects;
        private static ulong s_CoveragePolls;
        private static ulong s_CoverageRounds;
        private static ulong s_CoverageReadyRounds;
        private static readonly CountBatchLane[] s_CountBatchLanes =
            new CountBatchLane[CountBatchLaneCount];
        private static ulong s_CountBatchReadbacks;
        private static ulong s_CountBatchRecords;
        private static ulong s_CountBatchArenaWaits;
        private static double s_MaxCountDispatchMsSinceReport;
        private static double s_MaxWriteDispatchMsSinceReport;
        private static double s_MaxCopyDispatchMsSinceReport;
        private static double s_MaxCompletionPollMsSinceReport;

        private sealed class CountBatchLane
        {
            internal readonly GpuSurfaceExtractionContext[] Contexts =
                new GpuSurfaceExtractionContext[CountBatchCapacity];
            internal readonly uint[] Tokens = new uint[CountBatchCapacity];
            internal readonly GpuChunkExtraction[] Requests =
                new GpuChunkExtraction[CountBatchCapacity];
            internal ComputeBuffer Counters;
            internal GpuSurfaceExtractor PrefixExtractor;
            internal GpuTransvoxelTables Tables;
            internal GpuSurfaceExtractor.CountBatchResources Resources;
            internal int Count;
            internal int FirstDispatchFrame = -1;
        }

        private readonly struct ChunkHandleKey : IEquatable<ChunkHandleKey>
        {
            internal readonly int3 Origin;
            internal readonly int SourceStep;
            internal ChunkHandleKey(int3 origin, int sourceStep)
            {
                Origin = origin; SourceStep = sourceStep;
            }
            public bool Equals(ChunkHandleKey other) =>
                SourceStep == other.SourceStep && math.all(Origin == other.Origin);
            public override bool Equals(object obj) => obj is ChunkHandleKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Origin.GetHashCode(), SourceStep);
        }

        internal static void ConfigurePageArena(GpuSurfacePageArena arena)
        {
            if (s_PageArena != null && !ReferenceEquals(s_PageArena, arena))
                throw new InvalidOperationException("The world already owns a GPU surface page arena.");
            s_PageArena = arena ?? throw new ArgumentNullException(nameof(arena));
        }

        internal static bool HasPageArena => s_PageArena != null;

        internal static void FlushPageArenaCommands(int frame) =>
            s_PageArena?.FlushHandleCommands(frame);

        internal static void DetachPageArena(GpuSurfacePageArena arena, int frame)
        {
            if (s_PageArena == null || !ReferenceEquals(s_PageArena, arena)) return;
            s_PageArena.FlushHandleCommands(frame);
            s_ChunkHandles.Clear();
            s_PageArena = null;
        }

        internal static int PrepareChunkHandle(int3 origin, int sourceStep, ulong generation)
        {
            if (s_PageArena == null) return 0;
            var key = new ChunkHandleKey(origin, sourceStep);
            if (!s_ChunkHandles.TryGetValue(key, out int handle))
            {
                if (!s_PageArena.TryAcquireHandle(out handle)) return -1;
                s_ChunkHandles.Add(key, handle);
            }
            s_PageArena.QueueGeneration(handle, generation);
            return handle;
        }

        internal static void ReleaseChunkHandle(int3 origin, int sourceStep, ulong generation)
        {
            if (s_PageArena == null) return;
            var key = new ChunkHandleKey(origin, sourceStep);
            if (!s_ChunkHandles.Remove(key, out int handle)) return;
            s_PageArena.QueueRelease(handle, generation);
        }

        private readonly struct ActiveFootprint : IEquatable<ActiveFootprint>
        {
            internal readonly int3 Origin;
            internal readonly int Edge;

            internal ActiveFootprint(int3 origin, int edge)
            {
                Origin = origin;
                Edge = edge;
            }

            public bool Equals(ActiveFootprint other) =>
                Edge == other.Edge && math.all(Origin == other.Origin);

            public override bool Equals(object obj) =>
                obj is ActiveFootprint other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Origin.GetHashCode(), Edge);
        }

        internal static GpuVoxelBrickMirror Acquire(long requestedBudgetBytes)
        {
            EnsureCollectionPools();
            if (s_Mirror == null)
            {
                long budget = Math.Max(MinimumSharedMirrorBudgetBytes,
                                       Math.Max(1L, requestedBudgetBytes) * 16L);
                s_Mirror = new GpuVoxelBrickMirror(
                    GpuBrickBufferLayout.SlotsForBudget(budget));
            }
            s_ReferenceCount++;
            return s_Mirror;
        }

        internal static void ReleaseReference()
        {
            if (s_ReferenceCount > 0) s_ReferenceCount--;
            if (s_ReferenceCount == 0) ResetWorld(disposeMirror: true);
        }

        /// <summary>Attaches the current world. Heavy mirror work remains in PrepareFrame.</summary>
        internal static bool PrepareFromBridge(ulong requiredGeneration)
        {
            if (s_Mirror == null || !SystemInfo.supportsComputeShaders) return false;
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView world)
                || world.Storage == null)
                return false;
            if (requiredGeneration > world.Storage.Version) return false;

            if (!ReferenceEquals(s_Storage, world.Storage)
                || !ReferenceEquals(s_ChangeSource, VoxelRenderBridge.Changes))
                AttachWorld(world.Storage, VoxelRenderBridge.Changes);
            return requiredGeneration <= world.Storage.Version;
        }

        /// <summary>
        /// Replays edits and copies requested bricks once per rendered frame. SetData/scatter is
        /// performed here, never during a worker's chunk admission.
        /// </summary>
        internal static void PrepareFrame(IRegionReadSource storage, IVoxelChangeSource changes,
                                          int frame, double budgetMs,
                                          int uploadBudgetBytes = DefaultUploadBudgetBytes)
        {
            if (s_Mirror == null || storage == null || budgetMs <= 0.0) return;
            if (!ReferenceEquals(s_Storage, storage) || !ReferenceEquals(s_ChangeSource, changes))
                AttachWorld(storage, changes);
            if (s_LastPrepareFrame == frame) return;
            s_LastPrepareFrame = frame;

            double deadline = Time.realtimeSinceStartupAsDouble + budgetMs * 0.001;
            if (s_MirroredVersion != storage.Version
                && Time.realtimeSinceStartupAsDouble < deadline)
                SynchronizeChanges(storage.Version);
            if (Time.realtimeSinceStartupAsDouble < deadline)
                ProcessRecovery(deadline, Math.Max(0, uploadBudgetBytes));
            s_Mirror.FlushPendingUploads();
            AdvanceCountBatches(frame);
        }

        /// <summary>
        /// Appends one immutable chunk descriptor to a cross-chunk lane. On seal, batch-wide GPU
        /// count/prefix, page allocation, all-category generation, and publication execute without
        /// transferring bookkeeping to the CPU. A lane seals at eight descriptors or after a
        /// bounded delay, so sparse demand cannot wait forever.
        /// </summary>
        internal static bool TryDispatchCountBatch(GpuSurfaceExtractionContext context,
                                                   uint token,
                                                   GpuSurfaceExtractor extractor,
                                                   GpuTransvoxelTables tables,
                                                   in GpuChunkExtraction request,
                                                   int frame)
        {
            if (context == null || extractor == null || tables == null || s_Mirror == null)
                return false;
            EnsureCountBatchLanes();

            CountBatchLane lane = null;
            for (int i = 0; i < s_CountBatchLanes.Length; i++)
            {
                CountBatchLane candidate = s_CountBatchLanes[i];
                if (candidate.Count < CountBatchCapacity)
                {
                    lane = candidate;
                    break;
                }
            }
            if (lane == null) return false;

            int record = lane.Count++;
            if (record == 0)
            {
                lane.FirstDispatchFrame = frame;
                lane.PrefixExtractor = extractor;
                lane.Tables = tables;
                lane.Resources ??= extractor.CreateCountBatchResources(CountBatchCapacity);
            }
            lane.Contexts[record] = context;
            lane.Tokens[record] = token;
            lane.Requests[record] = request;
            double dispatchStarted = Time.realtimeSinceStartupAsDouble;
            s_MaxCountDispatchMsSinceReport = Math.Max(
                s_MaxCountDispatchMsSinceReport,
                (Time.realtimeSinceStartupAsDouble - dispatchStarted) * 1000.0);
            s_CountBatchRecords++;
            if (lane.Count == CountBatchCapacity) SealCountBatch(lane);
            return true;
        }

        private static void EnsureCountBatchLanes()
        {
            for (int i = 0; i < s_CountBatchLanes.Length; i++)
            {
                if (s_CountBatchLanes[i] == null) s_CountBatchLanes[i] = new CountBatchLane();
                CountBatchLane lane = s_CountBatchLanes[i];
                lane.Counters ??= new ComputeBuffer(
                    GpuSurfaceExtractor.BatchHeaderWords
                    + CountBatchCapacity * GpuSurfaceExtractor.BatchRecordWords,
                    sizeof(uint), ComputeBufferType.Structured);
            }
        }

        private static void AdvanceCountBatches(int frame)
        {
            for (int laneIndex = 0; laneIndex < s_CountBatchLanes.Length; laneIndex++)
            {
                CountBatchLane lane = s_CountBatchLanes[laneIndex];
                if (lane == null) continue;
                if (lane.Count > 0 && frame - lane.FirstDispatchFrame >= CountBatchMaxFillFrames)
                    SealCountBatch(lane);
            }
        }

        private static void ResetCountBatchLane(CountBatchLane lane)
        {
            for (int record = 0; record < lane.Count; record++)
            {
                lane.Contexts[record] = null;
                lane.Tokens[record] = 0;
                lane.Requests[record] = default;
            }
            lane.Count = 0;
            lane.FirstDispatchFrame = -1;
            lane.PrefixExtractor = null;
            lane.Tables = null;
        }

        private static void SealCountBatch(CountBatchLane lane)
        {
            if (lane == null || lane.Count == 0) return;
            if (s_PageArena == null)
                throw new InvalidOperationException(
                    "Production GPU extraction requires the GPU-owned page arena.");
            int frame = Time.frameCount;

            // Submission is not completion. Without an in-flight bound, GPU-only publication can
            // enqueue several expensive count/write chains ahead of rendering; the main and render
            // threads remain cheap, then presentation absorbs the accumulated queue as a 100+ ms
            // hitch. A graphics fence transfers no voxel/count/allocation data to the CPU. Its
            // nonblocking status is solely queue backpressure, matching an ordinary render graph.
            if (s_ExtractionFenceValid && !s_ExtractionFence.passed)
            {
                s_CountBatchArenaWaits++;
                return;
            }
            s_ExtractionFenceValid = false;
            if (!TryReserveExtractionDispatch(frame)) return;

            s_PageArena.FlushHandleCommands(frame);
            lane.PrefixExtractor.DispatchCountBatch(
                s_Mirror, lane.Tables, lane.Requests, lane.Count,
                lane.Counters, lane.Resources);
            lane.PrefixExtractor.PrefixCountBatch(
                lane.Counters, lane.Count,
                SurfaceGeometryArena.VertexAlignment,
                SurfaceGeometryArena.IndexAlignment);
            s_PageArena.AllocateBatch(lane.Resources.Chunks, lane.Counters, lane.Count,
                                      GpuSurfaceExtractor.BatchRecordWords, frame);
            lane.PrefixExtractor.DispatchBaseWriteBatch(
                s_Mirror, lane.Tables, lane.Count, lane.Counters, lane.Resources,
                s_PageArena.Vertices, s_PageArena.Indices,
                pageArena: s_PageArena, frame: frame);
            s_ExtractionFence = Graphics.CreateGraphicsFence(
                GraphicsFenceType.CPUSynchronisation,
                SynchronisationStageFlags.ComputeProcessing);
            s_ExtractionFenceValid = true;
            for (int record = 0; record < lane.Count; record++)
                lane.Contexts[record]?.CompletePagedBatch(
                    lane.Tokens[record], lane.Requests[record].Handle);
            ResetCountBatchLane(lane);
        }

        private static void ResetCountBatches()
        {
            for (int i = 0; i < s_CountBatchLanes.Length; i++)
            {
                CountBatchLane lane = s_CountBatchLanes[i];
                if (lane == null) continue;
                for (int record = 0; record < lane.Count; record++)
                    lane.Contexts[record]?.FailPagedBatch(lane.Tokens[record]);
                lane.Counters?.Release();
                lane.Counters = null;
                lane.Resources?.Dispose();
                lane.Resources = null;
                ResetCountBatchLane(lane);
            }
            s_CountBatchReadbacks = 0;
            s_CountBatchRecords = 0;
            s_CountBatchArenaWaits = 0;
        }

        internal static void RequestCoverage(int3 brickCacheOrigin, int brickCacheEdge,
                                             int3 coreMinVoxel,
                                             int3 coreMaxVoxelExclusive)
        {
            if (brickCacheEdge <= 0) return;
            ChangeDemandFootprint(brickCacheOrigin, brickCacheEdge, 1);
        }

        internal static void ReleaseCoverage(int3 brickCacheOrigin, int brickCacheEdge,
                                             int3 coreMinVoxel,
                                             int3 coreMaxVoxelExclusive)
        {
            if (brickCacheEdge <= 0) return;
            ChangeDemandFootprint(brickCacheOrigin, brickCacheEdge, -1);

            if (s_DemandFootprints.Count == 0) ClearRecoveryQueues();
        }

        internal static bool Covers(int3 brickCacheOrigin, int brickCacheEdge,
                                    int3 coreMinVoxel, int3 coreMaxVoxelExclusive,
                                    ulong requiredGeneration, ref int scanCursor,
                                    ref bool roundIncomplete)
        {
            s_CoveragePolls++;
            if (s_Storage == null || brickCacheEdge <= 0)
                return false;
            if (requiredGeneration < s_KnownRegionHistoryFromVersion)
            {
                s_HistoryCoverageRejects++;
                return false;
            }

            int regionShift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int blockShift = VoxelReadGrid.BlockEdgeLog2;
            int blockCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;
            int stop = Math.Min(blockCount, scanCursor + CoverageChecksPerPoll);
            for (; scanCursor < stop; scanCursor++)
            {
                int x = scanCursor % brickCacheEdge;
                int yz = scanCursor / brickCacheEdge;
                int y = yz % brickCacheEdge;
                int z = yz / brickCacheEdge;
                int3 block = new(x, y, z);
                block += brickCacheOrigin;
                int3 region = block >> regionShift;
                if (!s_Storage.IsRegionResident(region))
                {
                    int3 blockMinVoxel = block << blockShift;
                    int3 blockMaxVoxelExclusive =
                        blockMinVoxel + new int3(VoxelReadGrid.BlockEdge);
                    bool intersectsCore = math.all(blockMaxVoxelExclusive > coreMinVoxel)
                                       && math.all(blockMinVoxel < coreMaxVoxelExclusive);
                    if (intersectsCore)
                    {
                        s_CoreNonResidentCoverageChecks++;
                        roundIncomplete = true;
                    }
                    else s_OptionalNonResidentHaloBlocksAccepted++;
                    continue;
                }
                if (s_PendingBlocks.Contains(block) || !s_ReadyBlocks.Contains(block))
                {
                    QueueRecoveryBlock(block);
                    roundIncomplete = true;
                    continue;
                }
                if (s_RegionLastSolidChangeVersion.TryGetValue(region, out ulong changedAt)
                    && changedAt > requiredGeneration)
                {
                    s_ChangedRegionCoverageRejects++;
                    roundIncomplete = true;
                }
            }

            if (scanCursor < blockCount) return false;
            s_CoverageRounds++;
            bool covered = !roundIncomplete;
            if (covered) s_CoverageReadyRounds++;
            scanCursor = 0;
            roundIncomplete = false;
            return covered;
        }

        internal static bool TryBeginExtraction(int3 brickCacheOrigin, int brickCacheEdge)
        {
            // A direct ComputeShader dispatch shares Metal's graphics queue with rendering. Do not
            // admit more complete count/write/copy chains than one count lane can service; deeper
            // queues increase presentation latency without increasing useful parallelism.
            if (s_ActiveExtractionCount >= MaxConcurrentExtractionChains) return false;
            s_ActiveExtractionCount++;
            ChangeActiveFootprint(brickCacheOrigin, brickCacheEdge, 1);
            ChangeActiveRegionReaders(brickCacheOrigin, brickCacheEdge, 1);
            return true;
        }

        internal static bool TryReserveExtractionDispatch(int frame)
        {
            // Metal still serializes the large extraction kernels even when their outputs are
            // private. Keep each count or ordered write/copy/publication chain globally bounded to
            // one stage per frame; multiple large stages caused 80-305 ms traversal stalls.
            if (s_LastExtractionDispatchFrame == frame) return false;
            s_LastExtractionDispatchFrame = frame;
            return true;
        }

        internal static void RecordWriteDispatch(double milliseconds) =>
            s_MaxWriteDispatchMsSinceReport = Math.Max(
                s_MaxWriteDispatchMsSinceReport, Math.Max(0.0, milliseconds));

        internal static void RecordCopyDispatch(double milliseconds) =>
            s_MaxCopyDispatchMsSinceReport = Math.Max(
                s_MaxCopyDispatchMsSinceReport, Math.Max(0.0, milliseconds));

        internal static void RecordCompletionPoll(double milliseconds) =>
            s_MaxCompletionPollMsSinceReport = Math.Max(
                s_MaxCompletionPollMsSinceReport, Math.Max(0.0, milliseconds));

        internal static string ConsumeExtractionDispatchTimings()
        {
            string result = $"countMax={s_MaxCountDispatchMsSinceReport:0.000}"
                          + $" writeMax={s_MaxWriteDispatchMsSinceReport:0.000}"
                          + $" copyMax={s_MaxCopyDispatchMsSinceReport:0.000}"
                          + $" pollMax={s_MaxCompletionPollMsSinceReport:0.000}";
            s_MaxCountDispatchMsSinceReport = 0.0;
            s_MaxWriteDispatchMsSinceReport = 0.0;
            s_MaxCopyDispatchMsSinceReport = 0.0;
            s_MaxCompletionPollMsSinceReport = 0.0;
            return result;
        }

        internal static void EndExtraction(int3 brickCacheOrigin, int brickCacheEdge)
        {
            if (s_ActiveExtractionCount > 0) s_ActiveExtractionCount--;
            ChangeActiveFootprint(brickCacheOrigin, brickCacheEdge, -1);
            ChangeActiveRegionReaders(brickCacheOrigin, brickCacheEdge, -1);
        }

        internal static int ReadyRegionCount => s_ReadyBlocksByRegion.Count;
        internal static int ReadyBlockCount => s_ReadyBlocks.Count;
        internal static int PendingBlockCount => s_PendingBlocks.Count;
        internal static int MirrorSlotCapacity => s_Mirror?.SlotCapacity ?? 0;
        internal static int ResidentMixedBrickCount => s_Mirror?.ResidentBricks ?? 0;
        internal static int ActiveRegionCount => s_ActiveRegionReaders.Count;
        internal static int ActiveExtractions => s_ActiveExtractionCount;
        internal static ulong MirroredVersion => s_MirroredVersion;
        internal static uint CoverageEpoch => s_CoverageEpoch;
        internal static bool RecoveryComplete => s_PendingBlocks.Count == 0;
        internal static ulong OptionalNonResidentHaloBlocksAccepted =>
            s_OptionalNonResidentHaloBlocksAccepted;
        internal static ulong ConcurrentDemandRecoverySlices => s_ConcurrentDemandRecoverySlices;
        internal static int DemandFootprintCount => s_DemandFootprints.Count;
        internal static ulong CoreNonResidentCoverageChecks =>
            s_CoreNonResidentCoverageChecks;
        internal static ulong HistoryCoverageRejects => s_HistoryCoverageRejects;
        internal static ulong ChangedRegionCoverageRejects =>
            s_ChangedRegionCoverageRejects;
        internal static ulong CoveragePolls => s_CoveragePolls;
        internal static ulong CoverageRounds => s_CoverageRounds;
        internal static ulong CoverageReadyRounds => s_CoverageReadyRounds;
        internal static ulong CountBatchReadbacks => s_CountBatchReadbacks;
        internal static ulong CountBatchRecords => s_CountBatchRecords;
        internal static ulong CountBatchArenaWaits => s_CountBatchArenaWaits;

        private static void AttachWorld(IRegionReadSource storage, IVoxelChangeSource changes)
        {
            ResetWorld(disposeMirror: false);
            unchecked { s_CoverageEpoch++; }
            s_Mirror.Clear();
            s_Storage = storage;
            s_ChangeSource = changes;
            s_ChangeCursor = changes?.CurrentVersion ?? storage.Version;
            s_MirroredVersion = storage.Version;
            s_KnownRegionHistoryFromVersion = storage.Version;
        }

        private static bool SynchronizeChanges(ulong targetGeneration)
        {
            if (s_Storage == null) return false;
            if (s_ChangeSource == null)
            {
                InvalidateAll(targetGeneration);
                return true;
            }

            s_Changes.Clear();
            bool valid = s_ChangeSource.ReadSince(ref s_ChangeCursor, s_Changes,
                                                  ChangeRecordsPerFrame, out bool hasMore);
            if (!valid)
            {
                InvalidateAll(targetGeneration);
                return true;
            }

            for (int i = 0; i < s_Changes.Count; i++) ApplyChange(s_Changes[i]);
            if (hasMore || s_ChangeCursor < targetGeneration) return false;
            s_MirroredVersion = targetGeneration;
            return true;
        }

        private static void InvalidateAll(ulong targetGeneration)
        {
            unchecked { s_CoverageEpoch++; }
            s_Mirror.Clear();
            ClearReadyBlocks();
            s_ReadyResidencyOrder.Clear();
            s_MixedResidencyOrder.Clear();
            s_MixedReadyBlocks.Clear();
            ClearRecoveryQueues();
            s_RegionLastSolidChangeVersion.Clear();
            s_KnownRegionHistoryFromVersion = targetGeneration;
            s_MirroredVersion = targetGeneration;
        }

        private static void ApplyChange(in VoxelChangeRecord change)
        {
            if ((change.Kind & ~VoxelChangeKind.Water) == VoxelChangeKind.None) return;
            if (!s_RegionLastSolidChangeVersion.TryGetValue(change.Region, out ulong previous)
                || change.Version > previous)
                s_RegionLastSolidChangeVersion[change.Region] = change.Version;

            if (!s_ReadyBlocksByRegion.TryGetValue(
                    change.Region, out HashSet<int3> readyInRegion))
                return;
            bool wholeRegion = math.any(change.MaxVoxelExclusive <= change.MinVoxel);
            int3 min = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 max = wholeRegion
                ? default
                : (change.MaxVoxelExclusive - 1) >> VoxelReadGrid.BlockEdgeLog2;
            s_ChangedReadyScratch.Clear();
            int3 size = wholeRegion ? default : max - min + 1;
            long changedVolume = wholeRegion
                ? long.MaxValue : (long)size.x * size.y * size.z;
            if (!wholeRegion && changedVolume < readyInRegion.Count)
            {
                for (int z = min.z; z <= max.z; z++)
                for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                {
                    int3 block = new(x, y, z);
                    if (readyInRegion.Contains(block)) s_ChangedReadyScratch.Add(block);
                }
            }
            else
            {
                foreach (int3 block in readyInRegion)
                {
                    if (wholeRegion || math.all(block >= min & block <= max))
                        s_ChangedReadyScratch.Add(block);
                }
            }
            if (s_ChangedReadyScratch.Count > 0)
                unchecked { s_CoverageEpoch++; }
            for (int i = 0; i < s_ChangedReadyScratch.Count; i++)
            {
                int3 block = s_ChangedReadyScratch[i];
                // An in-flight dispatch may still resolve this exact directory entry. Keep its
                // immutable old generation reachable until that dispatch releases the footprint;
                // changedAt above prevents any new request for the old generation from admitting.
                if (!IsBlockActive(block))
                {
                    RemoveReadyBlock(block);
                    s_MixedReadyBlocks.Remove(block);
                }
                QueueRecoveryBlock(block);
            }
        }

        private static void QueueRecoveryBlock(int3 block)
        {
            if (!IsBlockDemanded(block)) return;
            if (!s_PendingBlocks.Add(block)) return;
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!s_PendingBlocksByRegion.TryGetValue(region, out Queue<int3> blocks))
            {
                blocks = s_BlockQueuePool.Count > 0
                    ? s_BlockQueuePool.Pop()
                    : new Queue<int3>(BlocksPerTrackedRegionCapacity);
                s_PendingBlocksByRegion.Add(region, blocks);
            }
            blocks.Enqueue(block);
            if (s_QueuedRecoveryRegions.Add(region)) s_RecoveryRegions.Enqueue(region);
        }

        private static void ProcessRecovery(double deadlineSeconds, int uploadBudgetBytes)
        {
            int stagedBytes = 0;
            int consecutiveBlockedRegions = 0;
            bool concurrentProgressRecorded = false;
            while (s_RecoveryRegions.Count > 0
                   && Time.realtimeSinceStartupAsDouble < deadlineSeconds)
            {
                int3 region = s_RecoveryRegions.Dequeue();
                s_QueuedRecoveryRegions.Remove(region);
                if (!s_PendingBlocksByRegion.TryGetValue(region, out Queue<int3> blocks)
                    || blocks.Count == 0)
                    continue;

                bool resident = s_Storage.TryAcquireRegion(region, out RegionReadView view);
                int blocksLeftToInspect = blocks.Count;
                bool madeProgress = false;
                while (blocks.Count > 0
                       && blocksLeftToInspect-- > 0
                       && Time.realtimeSinceStartupAsDouble < deadlineSeconds)
                {
                    if (stagedBytes >= uploadBudgetBytes && stagedBytes > 0)
                    {
                        RequeueRegion(region);
                        return;
                    }

                    int3 worldBlock = blocks.Dequeue();
                    // Queue<T> cannot erase arbitrary coordinates when a pending chunk is
                    // superseded. The demand set is authoritative; discard the physical stale
                    // entry without borrowing Storage or consuming upload bytes.
                    if (!s_PendingBlocks.Contains(worldBlock)
                        || !IsBlockDemanded(worldBlock))
                    {
                        s_PendingBlocks.Remove(worldBlock);
                        madeProgress = true;
                        RecordConcurrentRecoveryProgress(ref concurrentProgressRecorded);
                        continue;
                    }
                    if (IsBlockActive(worldBlock))
                    {
                        blocks.Enqueue(worldBlock);
                        continue;
                    }

                    madeProgress = true;
                    s_PendingBlocks.Remove(worldBlock);
                    int3 localBlock = worldBlock
                        - (region << VoxelReadGrid.BlocksPerRegionEdgeLog2);
                    if (!resident)
                    {
                        s_Mirror.Remove(worldBlock);
                        RemoveReadyBlock(worldBlock);
                        RecordConcurrentRecoveryProgress(ref concurrentProgressRecorded);
                        continue;
                    }
                    if (!view.TryGetBlock(localBlock, out VoxelReadBlock block))
                    {
                        QueueRecoveryBlock(worldBlock);
                        continue;
                    }

                    VoxelBrickDelta delta = block.Kind switch
                    {
                        VoxelReadBlockKind.Empty =>
                            VoxelBrickDelta.EmptyAt(worldBlock, view.Version),
                        VoxelReadBlockKind.Uniform =>
                            VoxelBrickDelta.UniformAt(
                                worldBlock, view.Version, block.UniformMaterial),
                        _ => VoxelBrickDelta.MixedAt(worldBlock, view.Version, 0),
                    };
                    GpuBrickPublish result = PublishBlock(
                        in delta, in view, localBlock, block.Kind);
                    if (result is GpuBrickPublish.NoSlot or GpuBrickPublish.PayloadMissing
                        or GpuBrickPublish.Stale)
                    {
                        QueueRecoveryBlock(worldBlock);
                        RequeueRegion(region);
                        return;
                    }

                    AddReadyBlock(worldBlock);
                    RecordConcurrentRecoveryProgress(ref concurrentProgressRecorded);
                    if (block.Kind == VoxelReadBlockKind.Mixed)
                    {
                        if (s_MixedReadyBlocks.Add(worldBlock))
                            s_MixedResidencyOrder.Enqueue(worldBlock);
                    }
                    else
                    {
                        s_MixedReadyBlocks.Remove(worldBlock);
                    }
                    if (result == GpuBrickPublish.Uploaded)
                    {
                        s_Mirror.Pin(worldBlock);
                        stagedBytes += GpuBrickBufferLayout.BytesPerMixedBrick;
                    }
                }

                if (blocks.Count > 0) RequeueRegion(region);
                else
                {
                    s_PendingBlocksByRegion.Remove(region);
                    blocks.Clear();
                    s_BlockQueuePool.Push(blocks);
                }

                if (madeProgress)
                {
                    consecutiveBlockedRegions = 0;
                }
                else if (blocks.Count > 0)
                {
                    consecutiveBlockedRegions++;
                    if (consecutiveBlockedRegions >= s_RecoveryRegions.Count) return;
                }
            }
        }

        private static void RecordConcurrentRecoveryProgress(ref bool recorded)
        {
            if (recorded || s_ActiveExtractionCount == 0) return;
            recorded = true;
            s_ConcurrentDemandRecoverySlices++;
        }

        private static GpuBrickPublish PublishBlock(
            in VoxelBrickDelta delta, in RegionReadView view, int3 localBlock,
            VoxelReadBlockKind kind)
        {
            GpuBrickPublish result = kind == VoxelReadBlockKind.Mixed
                ? s_Mirror.Publish(delta, in view, localBlock)
                : s_Mirror.Publish(delta, default(NativeArray<byte>),
                                   default(NativeArray<ushort>),
                                   default(NativeArray<byte>), 0, false);
            if (result != GpuBrickPublish.NoSlot || !TryEvictInactiveMixedBlock())
                return result;
            return kind == VoxelReadBlockKind.Mixed
                ? s_Mirror.Publish(delta, in view, localBlock)
                : s_Mirror.Publish(delta, default(NativeArray<byte>),
                                   default(NativeArray<ushort>),
                                   default(NativeArray<byte>), 0, false);
        }

        /// <summary>
        /// Geometry no longer reads its source bricks after count/write completes, so a cold brick
        /// outside every demanded or active extraction may be reclaimed. Removing its directory
        /// entry and readiness bit together prevents a reused slot from being addressed by the old
        /// world coordinate. Do not advance the global coverage epoch here: demand pinning proves
        /// the evicted block belongs to no pending scan, while restarting every unrelated scan on
        /// each capacity eviction creates a permanent liveness failure during camera motion.
        /// </summary>
        private static bool TryEvictInactiveMixedBlock()
        {
            int attempts = s_MixedResidencyOrder.Count;
            while (attempts-- > 0 && s_MixedResidencyOrder.Count > 0)
            {
                int3 block = s_MixedResidencyOrder.Dequeue();
                if (!s_MixedReadyBlocks.Contains(block)) continue;
                if (IsBlockDemanded(block) || IsBlockActive(block))
                {
                    s_MixedResidencyOrder.Enqueue(block);
                    continue;
                }

                s_MixedReadyBlocks.Remove(block);
                RemoveReadyBlock(block);
                s_Mirror.Remove(block);
                return true;
            }
            return false;
        }

        private static void RequeueRegion(int3 region)
        {
            if (s_QueuedRecoveryRegions.Add(region)) s_RecoveryRegions.Enqueue(region);
        }

        private static void ChangeDemandFootprint(int3 origin, int edge, int delta)
        {
            var footprint = new ActiveFootprint(origin, edge);
            s_DemandFootprints.TryGetValue(footprint, out int readers);
            readers += delta;
            if (readers > 0)
            {
                s_DemandFootprints[footprint] = readers;
                return;
            }

            s_DemandFootprints.Remove(footprint);
        }

        private static bool IsBlockDemanded(int3 block)
        {
            foreach (ActiveFootprint footprint in s_DemandFootprints.Keys)
            {
                int3 end = footprint.Origin + new int3(footprint.Edge);
                if (math.all(block >= footprint.Origin & block < end)) return true;
            }
            return false;
        }

        private static void AddReadyBlock(int3 block)
        {
            if (s_ReadyBlocks.Contains(block)) return;
            if (s_ReadyBlocks.Count >= TrackedBlockCapacity)
                TryEvictInactiveReadyBlock();
            if (!s_ReadyBlocks.Add(block)) return;
            s_ReadyResidencyOrder.Enqueue(block);
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!s_ReadyBlocksByRegion.TryGetValue(region, out HashSet<int3> blocks))
            {
                blocks = s_ReadySetPool.Count > 0
                    ? s_ReadySetPool.Pop()
                    : new HashSet<int3>(BlocksPerTrackedRegionCapacity);
                s_ReadyBlocksByRegion.Add(region, blocks);
            }
            blocks.Add(block);
        }

        private static bool TryEvictInactiveReadyBlock()
        {
            int attempts = s_ReadyResidencyOrder.Count;
            while (attempts-- > 0 && s_ReadyResidencyOrder.Count > 0)
            {
                int3 block = s_ReadyResidencyOrder.Dequeue();
                if (!s_ReadyBlocks.Contains(block)) continue;
                if (IsBlockDemanded(block) || IsBlockActive(block))
                {
                    s_ReadyResidencyOrder.Enqueue(block);
                    continue;
                }

                RemoveReadyBlock(block);
                s_MixedReadyBlocks.Remove(block);
                s_Mirror.Remove(block);
                return true;
            }
            return false;
        }

        private static void RemoveReadyBlock(int3 block)
        {
            if (!s_ReadyBlocks.Remove(block)) return;
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!s_ReadyBlocksByRegion.TryGetValue(region, out HashSet<int3> blocks)) return;
            blocks.Remove(block);
            if (blocks.Count == 0)
            {
                s_ReadyBlocksByRegion.Remove(region);
                blocks.Clear();
                s_ReadySetPool.Push(blocks);
            }
        }

        private static void ChangeActiveRegionReaders(int3 brickCacheOrigin, int brickCacheEdge,
                                                      int delta)
        {
            if (brickCacheEdge <= 0 || delta == 0) return;
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int3 first = brickCacheOrigin >> shift;
            int3 last = (brickCacheOrigin + new int3(brickCacheEdge - 1)) >> shift;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                int3 region = new(x, y, z);
                s_ActiveRegionReaders.TryGetValue(region, out int readers);
                readers += delta;
                if (readers > 0) s_ActiveRegionReaders[region] = readers;
                else s_ActiveRegionReaders.Remove(region);
            }
        }

        private static void ChangeActiveFootprint(int3 brickCacheOrigin, int brickCacheEdge,
                                                  int delta)
        {
            if (brickCacheEdge <= 0 || delta == 0) return;
            var footprint = new ActiveFootprint(brickCacheOrigin, brickCacheEdge);
            s_ActiveFootprints.TryGetValue(footprint, out int readers);
            readers += delta;
            if (readers > 0) s_ActiveFootprints[footprint] = readers;
            else s_ActiveFootprints.Remove(footprint);
        }

        private static bool IsBlockActive(int3 block)
        {
            foreach (ActiveFootprint footprint in s_ActiveFootprints.Keys)
            {
                int3 end = footprint.Origin + new int3(footprint.Edge);
                if (math.all(block >= footprint.Origin & block < end)) return true;
            }
            return false;
        }

        private static void ClearRecoveryQueues()
        {
            s_RecoveryRegions.Clear();
            s_QueuedRecoveryRegions.Clear();
            foreach (Queue<int3> blocks in s_PendingBlocksByRegion.Values)
            {
                blocks.Clear();
                s_BlockQueuePool.Push(blocks);
            }
            s_PendingBlocksByRegion.Clear();
            s_PendingBlocks.Clear();
        }

        private static void ClearReadyBlocks()
        {
            s_ReadyBlocks.Clear();
            foreach (HashSet<int3> blocks in s_ReadyBlocksByRegion.Values)
            {
                blocks.Clear();
                s_ReadySetPool.Push(blocks);
            }
            s_ReadyBlocksByRegion.Clear();
        }

        private static void EnsureCollectionPools()
        {
            while (s_BlockQueuePool.Count < TrackedRegionCapacity)
                s_BlockQueuePool.Push(
                    new Queue<int3>(BlocksPerTrackedRegionCapacity));
            while (s_ReadySetPool.Count < TrackedRegionCapacity)
                s_ReadySetPool.Push(
                    new HashSet<int3>(BlocksPerTrackedRegionCapacity));
        }

        private static void ResetWorld(bool disposeMirror)
        {
            s_Storage = null;
            s_ChangeSource = null;
            s_ChangeCursor = 0;
            s_MirroredVersion = 0;
            s_KnownRegionHistoryFromVersion = 0;
            s_ActiveExtractionCount = 0;
            s_LastPrepareFrame = -1;
            s_LastExtractionDispatchFrame = -1;
            s_ExtractionFenceValid = false;
            s_OptionalNonResidentHaloBlocksAccepted = 0;
            s_ConcurrentDemandRecoverySlices = 0;
            s_CoreNonResidentCoverageChecks = 0;
            s_HistoryCoverageRejects = 0;
            s_ChangedRegionCoverageRejects = 0;
            s_CoveragePolls = 0;
            s_CoverageRounds = 0;
            s_CoverageReadyRounds = 0;
            ResetCountBatches();
            ClearRecoveryQueues();
            ClearReadyBlocks();
            s_ChangedReadyScratch.Clear();
            s_ReadyResidencyOrder.Clear();
            s_MixedResidencyOrder.Clear();
            s_MixedReadyBlocks.Clear();
            s_RegionLastSolidChangeVersion.Clear();
            s_ActiveRegionReaders.Clear();
            s_ActiveFootprints.Clear();
            s_DemandFootprints.Clear();
            s_Changes.Clear();

            if (!disposeMirror || s_Mirror == null) return;
            s_Mirror.Dispose();
            s_Mirror = null;
        }
    }
}
