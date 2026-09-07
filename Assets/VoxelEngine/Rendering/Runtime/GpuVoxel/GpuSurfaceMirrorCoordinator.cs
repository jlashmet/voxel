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
        private static readonly Dictionary<ActiveFootprint, uint> s_EditWatchEpochs = new(32);
        private static readonly Dictionary<ActiveFootprint, int> s_EditWatchReaders = new(32);
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
        internal static ulong AllocationFailures { get; private set; }
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
            internal ComputeBuffer Outcomes;
            internal readonly uint[] OutcomeWords = new uint[CountBatchCapacity * 4];
            internal bool OutcomeReady, OutcomeFailed, Retired;
            internal readonly Action<AsyncGPUReadbackRequest> OutcomeCallback;
            internal readonly Action<AsyncGPUReadbackRequest> FailedSubmissionCallback;

            private GpuVoxelBrickMirror _submittedMirror;
            private GpuSurfacePageArena _submittedArena;
            private GpuSurfaceExtractor _submittedExtractor;
            private GpuTransvoxelTables _submittedTables;
            private byte _resourceOwners;
            private int _retainedReaderRecords;
            private int _submittedCacheEdge;
            private ulong _submittedWorldEpoch;

            internal void RetainSubmissionResources(GpuVoxelBrickMirror mirror, GpuSurfacePageArena arena)
            {
                _submittedMirror = mirror;
                _submittedArena = arena;
                _submittedExtractor = PrefixExtractor;
                _submittedTables = Tables;
                _submittedWorldEpoch = s_ResourceWorldEpoch;
                _submittedCacheEdge = PrefixExtractor.BrickCacheEdge;
                try
                {
                    mirror.RetainSubmission(); _resourceOwners |= 1;
                    arena.RetainSubmission(); _resourceOwners |= 2;
                    PrefixExtractor.RetainSubmission(); _resourceOwners |= 4;
                    Tables.RetainSubmission(); _resourceOwners |= 8;
                    for (int record = 0; record < Count; record++)
                    {
                        ChangeActiveFootprint(Requests[record].BrickCacheOrigin, _submittedCacheEdge, 1);
                        ChangeActiveRegionReaders(Requests[record].BrickCacheOrigin, _submittedCacheEdge, 1);
                        _retainedReaderRecords++;
                    }
                }
                catch { ReleaseSubmissionResources(); throw; }
            }

            private void ReleaseSubmissionResources()
            {
                // A retired world's callback must never decrement the new world's readers.
                if (_submittedWorldEpoch == s_ResourceWorldEpoch)
                    for (int record = 0; record < _retainedReaderRecords; record++)
                    {
                        ChangeActiveFootprint(Requests[record].BrickCacheOrigin, _submittedCacheEdge, -1);
                        ChangeActiveRegionReaders(Requests[record].BrickCacheOrigin, _submittedCacheEdge, -1);
                    }
                _retainedReaderRecords = 0;
                byte owners = _resourceOwners;
                _resourceOwners = 0;
                if ((owners & 8) != 0) _submittedTables.ReleaseSubmission();
                if ((owners & 4) != 0) _submittedExtractor.ReleaseSubmission();
                if ((owners & 2) != 0) _submittedArena.ReleaseSubmission();
                if ((owners & 1) != 0) _submittedMirror.ReleaseSubmission();
                _submittedTables = null;
                _submittedExtractor = null;
                _submittedArena = null;
                _submittedMirror = null;
            }

            internal CountBatchLane()
            {
                OutcomeCallback = ReceiveOutcome;
                FailedSubmissionCallback = ReceiveFailedSubmission;
                SummaryCallback = ReceiveSummary;
            }

            internal bool PreparingSummaries, SummarySubmitted, SummaryFailed;
            private int _summaryRecord, _summaryCursor, _summaryCount;
            private bool _summaryDemand;
            private int3 _summaryOrigin, _summaryExtent;
            private ulong _summaryWorld;
            private GpuVoxelBrickMirror _summaryMirror;
            private int _lastSummaryFrame = -1;
            internal ComputeBuffer SummaryRequests;
            internal ComputeBuffer SummaryMissing;
            internal ComputeBuffer SummaryBlockReferences;
            private NativeArray<int> _blockReferences;
            private readonly int4[] _summaryRegions = new int4[8];
            private readonly uint[] _summaryCountZero = new uint[1];
            private readonly Action<AsyncGPUReadbackRequest> SummaryCallback;

            internal bool AdvanceSummaryPreparation(int frame)
            {
                if (!SystemInfo.supportsAsyncGPUReadback)
                    throw new InvalidOperationException("GPU summary preparation requires asynchronous completion.");
                if (SummarySubmitted || SummaryFailed) return false;
                if (_summaryRecord >= Count) return true;
                if (_lastSummaryFrame == frame) return false;
                _lastSummaryFrame = frame;
                if (s_Storage == null) return false;
                PreparingSummaries = true;
                bool coarse = Requests[0].SourceStep == 8;
                if (coarse) Resources.PrepareHlod();
                else Resources.PrepareSourceResolution();
                int edge = PrefixExtractor.BrickCacheEdge;
                int blocks = edge * edge * edge;
                GpuChunkExtraction request = Requests[_summaryRecord];
                if (!_summaryDemand)
                {
                    // Whole X rows, or multiple complete XY planes, stay contiguous in the
                    // output. Bound both source count and the existing 64-row metadata slice.
                    int y = (_summaryCursor / edge) % edge, z = _summaryCursor / (edge * edge);
                    int rows = Math.Min(edge - y, GpuBlockHlodSummary.MaximumBlocksPerDispatch / edge);
                    int depth = rows == edge && y == 0
                        ? Math.Min(edge - z, Math.Min(64 / rows,
                            GpuBlockHlodSummary.MaximumBlocksPerDispatch / (edge * rows))) : 1;
                    _summaryOrigin = request.BrickCacheOrigin + new int3(0, y, z);
                    _summaryExtent = new int3(edge, rows, depth);
                    _summaryCount = edge * rows * depth;
                    _summaryWorld = RequestSourceRange(_summaryOrigin, _summaryExtent);
                    _summaryDemand = true;
                }
                int3 coreMax = request.ChunkOriginVoxel + new int3(PrefixExtractor.CellsPerAxis * request.SourceStep);
                int regionCount = PrepareSummaryRegions(request.ChunkOriginVoxel, coreMax);
                if (regionCount == 0 || !TryReserveExtractionDispatch(frame, coarse: coarse)) return false;
                if (!PrepareSummaryBlockReferences(regionCount)) return false;
                SummaryRequests ??= new ComputeBuffer(_summaryRegions.Length, 16);
                SummaryMissing ??= new ComputeBuffer(GpuBlockHlodSummary.MaximumBlocksPerDispatch + 1, 4);
                SummaryRequests.SetData(_summaryRegions, 0, 0, regionCount);
                SummaryMissing.SetData(_summaryCountZero, 0, 0, 1);
                _summaryMirror = s_Mirror;
                _summaryMirror.RetainSubmission();
                ChangeActiveFootprint(_summaryOrigin, _summaryExtent, 1);
                ChangeActiveRegionReaders(_summaryOrigin, _summaryExtent, 1);
                SummarySubmitted = true;
                bool issued = false;
                try
                {
                    GpuBlockHlodSummary.DispatchSourceRange(Resources.HlodSummaryShader, _summaryMirror,
                        SummaryRequests, regionCount, _summaryOrigin, _summaryExtent,
                        coarse ? Resources.HlodSummaries : Resources.PreparedCache.DenseEntries,
                        _summaryRecord * blocks + _summaryCursor,
                        SummaryMissing, SolidMaterialClassification.WaterMaterialMask,
                        blockReferences: SummaryBlockReferences, resolveEntries: !coarse);
                    issued = true;
                }
                finally
                {
                    SummaryFailed = !issued;
                    // Bounded missing-source indices are upload requests, not geometry or world
                    // truth. All occupancy/material summaries remain GPU-resident.
                    AsyncGPUReadback.Request(SummaryMissing, SummaryCallback);
                }
                return false;
            }

            private int PrepareSummaryRegions(int3 coreMin, int3 coreMax)
            {
                int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
                int3 minRegion = _summaryOrigin >> shift;
                int3 maxRegion = (_summaryOrigin + _summaryExtent - 1) >> shift;
                int count = 0;
                for (int z = minRegion.z; z <= maxRegion.z; z++)
                for (int y = minRegion.y; y <= maxRegion.y; y++)
                for (int x = minRegion.x; x <= maxRegion.x; x++)
                {
                    int3 region = new(x, y, z);
                    bool resident = s_Storage.IsRegionResident(region);
                    int3 min = math.max(region * VoxelGrid.RegionVoxelEdge, _summaryOrigin * 8);
                    int3 max = math.min((region + 1) * VoxelGrid.RegionVoxelEdge,
                                        (_summaryOrigin + _summaryExtent) * 8);
                    if (!resident && math.all(max > coreMin) && math.all(min < coreMax))
                    {
                        s_CoreNonResidentCoverageChecks++;
                        return 0;
                    }
                    _summaryRegions[count++] = new int4(region, resident ? 1 : 0);
                }
                return count;
            }

            private bool PrepareSummaryBlockReferences(int regionCount)
            {
                // Only copy/upload after obtaining the submission slot, never on waiting polls.
                SummaryBlockReferences ??= new ComputeBuffer(_summaryRegions.Length * 4096, sizeof(int));
                if (!_blockReferences.IsCreated)
                    _blockReferences = new NativeArray<int>(4096, Allocator.Persistent);
                for (int r = 0; r < regionCount; r++)
                {
                    if (_summaryRegions[r].w == 0) continue;
                    int3 region = _summaryRegions[r].xyz;
                    if (!s_Storage.TryAcquireRegion(region, out RegionReadView view)) return false;
                    int minY = math.max(0, _summaryOrigin.y - region.y * 64);
                    int maxY = math.min(64, _summaryOrigin.y + _summaryExtent.y - region.y * 64);
                    int minZ = math.max(0, _summaryOrigin.z - region.z * 64);
                    int maxZ = math.min(64, _summaryOrigin.z + _summaryExtent.z - region.z * 64);
                    int start = minY * 64, count = (maxY - minY) * 64;
                    for (int localZ = minZ; localZ < maxZ; localZ++)
                    {
                        // Copy canonical rows without interpreting block kinds or pool addresses.
                        if (!view.TryCopyBlockReferences(localZ * 4096 + start, _blockReferences, 0, count)
                            || s_Storage.Version != view.Version) return false;
                        int relativeZ = localZ + region.z * 64 - _summaryOrigin.z;
                        int relativeY = minY + region.y * 64 - _summaryOrigin.y;
                        int destination = r * 4096 + (relativeZ * _summaryExtent.y + relativeY) * 64;
                        SummaryBlockReferences.SetData(_blockReferences, 0, destination, count);
                    }
                }
                return true;
            }

            private void ReceiveSummary(AsyncGPUReadbackRequest request)
            {
                SummaryFailed |= request.hasError;
                if (_summaryWorld == s_ResourceWorldEpoch)
                {
                    ChangeActiveFootprint(_summaryOrigin, _summaryExtent, -1);
                    ChangeActiveRegionReaders(_summaryOrigin, _summaryExtent, -1);
                }
                _summaryMirror.ReleaseSubmission();
                _summaryMirror = null;
                SummarySubmitted = false;
                if (Retired) { ReleaseBuffers(); return; }
                if (SummaryFailed) return;
                var missing = request.GetData<uint>();
                uint count = missing[0];
                if (count > _summaryCount) { SummaryFailed = true; return; }
                for (int i = 0; i < count; i++)
                {
                    uint index = missing[i + 1];
                    if (index >= _summaryCount) { SummaryFailed = true; return; }
                    int linear = (int)index;
                    QueueRecoveryBlock(_summaryOrigin + new int3(linear % _summaryExtent.x,
                        linear / _summaryExtent.x % _summaryExtent.y,
                        linear / (_summaryExtent.x * _summaryExtent.y)));
                }
                // Retain demand while recovery services GPU-discovered misses. Releasing it
                // here would let PrepareFrame discard those requests before the retry.
                if (count != 0) return;
                ReleaseSummaryDemand();
                _summaryCursor += _summaryCount;
                int edge = Resources.BrickCacheEdge;
                if (_summaryCursor == edge * edge * edge) { _summaryCursor = 0; _summaryRecord++; }
            }

            private void ReleaseSummaryDemand()
            {
                if (_summaryDemand) ReleaseSourceRange(_summaryOrigin, _summaryExtent, _summaryWorld);
                _summaryDemand = false;
            }

            internal void ResetSummaryPreparation()
            {
                if (SummarySubmitted) throw new InvalidOperationException("Cannot reset an in-flight summary portion.");
                ReleaseSummaryDemand();
                PreparingSummaries = false;
                SummaryFailed = false;
                _summaryRecord = _summaryCursor = _summaryCount = 0;
                _lastSummaryFrame = -1;
            }

            private void ReceiveFailedSubmission(AsyncGPUReadbackRequest request)
            {
                ReleaseSubmissionResources();
                if (Retired) { ReleaseBuffers(); return; }
                OutcomeFailed = true;
                OutcomeReady = true;
            }

            private void ReceiveOutcome(AsyncGPUReadbackRequest request)
            {
                // The readback is ordered after every extraction command that references these
                // resources. Completion owns release even if the world/context has retired.
                ReleaseSubmissionResources();
                if (Retired) { ReleaseBuffers(); return; }
                OutcomeFailed = request.hasError;
                if (!OutcomeFailed) request.GetData<uint>().CopyTo(OutcomeWords);
                OutcomeReady = true;
            }

            internal void ReleaseBuffers()
            {
                ReleaseSubmissionResources();
                Outcomes?.Release(); Outcomes = null;
                Counters?.Release(); Counters = null;
                ResetSummaryPreparation();
                SummaryRequests?.Dispose(); SummaryRequests = null;
                SummaryMissing?.Dispose(); SummaryMissing = null;
                SummaryBlockReferences?.Dispose(); SummaryBlockReferences = null;
                if (_blockReferences.IsCreated) _blockReferences.Dispose();
                Resources?.Dispose(); Resources = null;
                LayoutExtractor = null;
            }
            internal GpuSurfaceExtractor PrefixExtractor;
            internal GpuSurfaceExtractor LayoutExtractor;
            internal GpuTransvoxelTables Tables;
            internal GpuSurfaceExtractor.CountBatchResources Resources;
            internal GraphicsFence CompletionFence;
            internal bool CompletionFenceValid;
            internal bool Submitted;
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

        private static ulong s_RenderGeneration;

        internal static int PrepareChunkHandle(int3 origin, int sourceStep, out ulong generation)
        {
            generation = checked(++s_RenderGeneration);
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

        internal static void ResolveCandidate(int handle, ulong generation, bool approve, int frame)
        {
            if (s_PageArena == null) return;
            // Apply any queued supersession/release before the GPU revalidates this identity.
            s_PageArena.FlushHandleCommands(frame);
            if (approve) s_PageArena.CommitPending(handle, generation, frame);
            else s_PageArena.AbortPending(handle, generation, frame);
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
            internal readonly int3 Extent;

            internal ActiveFootprint(int3 origin, int edge) : this(origin, new int3(edge)) { }

            internal ActiveFootprint(int3 origin, int3 extent)
            {
                Origin = origin;
                Extent = extent;
            }

            public bool Equals(ActiveFootprint other) =>
                math.all(Extent == other.Extent & Origin == other.Origin);

            public override bool Equals(object obj) =>
                obj is ActiveFootprint other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Origin.GetHashCode(), Extent.GetHashCode());
        }

        internal static GpuVoxelBrickMirror Acquire(long requestedBudgetBytes)
        {
            EnsureCollectionPools();
            if (s_Mirror == null)
            {
                long budget = Math.Max(MinimumSharedMirrorBudgetBytes,
                                       Math.Max(1L, requestedBudgetBytes) * 16L);
                var layout = GpuVoxelBrickMirror.SharedLayout(GpuBrickBufferLayout.SlotsForBudget(budget));
                s_Mirror = new GpuVoxelBrickMirror(layout.Slots, layout.DirectoryEntries);
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
            return !s_Mirror.IsClearPending && requiredGeneration <= world.Storage.Version;
        }

        /// <summary>
        /// Replays edits and copies requested bricks once per rendered frame. SetData/scatter is
        /// performed here, never during a worker's chunk admission.
        /// </summary>
        internal static double LastRecoveryMs { get; private set; }
        internal static double LastChangeSyncMs { get; private set; }
        internal static double LastUploadFlushMs { get; private set; }
        internal static double LastBatchAdvanceMs { get; private set; }
        internal static ulong LastRecoveredBlocks { get; private set; }

        internal static void PrepareFrame(IRegionReadSource storage, IVoxelChangeSource changes,
                                          int frame, double budgetMs,
                                          int uploadBudgetBytes = DefaultUploadBudgetBytes)
        {
            if (s_Mirror == null || storage == null || budgetMs <= 0.0) return;
            if (!ReferenceEquals(s_Storage, storage) || !ReferenceEquals(s_ChangeSource, changes))
                AttachWorld(storage, changes);
            if (s_LastPrepareFrame == frame) return;
            s_LastPrepareFrame = frame;
            LastRecoveryMs = LastChangeSyncMs = LastUploadFlushMs = LastBatchAdvanceMs = 0;
            LastRecoveredBlocks = 0;

            if (s_Mirror.IsClearPending) return;
            double deadline = Time.realtimeSinceStartupAsDouble + budgetMs * 0.001;
            double phaseStart = Time.realtimeSinceStartupAsDouble;
            if (s_MirroredVersion != storage.Version
                && Time.realtimeSinceStartupAsDouble < deadline)
                SynchronizeChanges(storage.Version);
            LastChangeSyncMs = (Time.realtimeSinceStartupAsDouble - phaseStart) * 1000.0;
            if (s_Mirror.IsClearPending) return;
            if (Time.realtimeSinceStartupAsDouble < deadline)
            {
                s_RecoveryCalls++;
                phaseStart = Time.realtimeSinceStartupAsDouble;
                ulong publishedBefore = s_RecoveryPublished;
                ProcessRecovery(deadline, Math.Max(0, uploadBudgetBytes));
                LastRecoveryMs = (Time.realtimeSinceStartupAsDouble - phaseStart) * 1000.0;
                LastRecoveredBlocks = s_RecoveryPublished - publishedBefore;
            }
            else s_RecoveryDeadlineSkips++;
            phaseStart = Time.realtimeSinceStartupAsDouble;
            s_Mirror.FlushPendingUploads();
            LastUploadFlushMs = (Time.realtimeSinceStartupAsDouble - phaseStart) * 1000.0;
            phaseStart = Time.realtimeSinceStartupAsDouble;
            AdvanceCountBatches(frame);
            LastBatchAdvanceMs = (Time.realtimeSinceStartupAsDouble - phaseStart) * 1000.0;
        }

        /// <summary>
        /// Appends one immutable chunk descriptor to a cross-chunk lane. A sealed lane remains
        /// submitted until its GPU fence passes. Generated geometry, allocation status, and
        /// immutable request identity remain GPU-owned; the CPU observes queue completion only.
        /// </summary>
        internal static bool TryDispatchCountBatch(GpuSurfaceExtractionContext context,
                                                   uint token,
                                                   GpuSurfaceExtractor extractor,
                                                   GpuTransvoxelTables tables,
                                                   in GpuChunkExtraction request,
                                                   int frame)
        {
            if (context == null || extractor == null || tables == null || s_Mirror == null
                || s_Mirror.IsClearPending || !context.IsCurrentBatchRequest(token))
                return false;
            EnsureCountBatchLanes();

            CountBatchLane lane = null;
            for (int i = 0; i < s_CountBatchLanes.Length; i++)
            {
                CountBatchLane candidate = s_CountBatchLanes[i];
                if (candidate.Submitted || candidate.PreparingSummaries || candidate.Count >= CountBatchCapacity) continue;
                if (candidate.Count > 0 && !extractor.HasSameBatchLayout(candidate.PrefixExtractor))
                    continue;
                // Prefer an already configured lane. Retaining each active ring's layout avoids
                // repeatedly reallocating buffers when fine/coarse requests are interleaved.
                if (candidate.Resources != null && extractor.HasSameBatchLayout(candidate.LayoutExtractor))
                {
                    lane = candidate;
                    break;
                }
                if (lane == null) lane = candidate;
            }
            if (lane == null) return false;

            int record = lane.Count++;
            if (record == 0)
            {
                lane.FirstDispatchFrame = frame;
                lane.PrefixExtractor = extractor;
                lane.Tables = tables;
                extractor.PrepareCountBatchResources(ref lane.Resources, CountBatchCapacity);
                lane.LayoutExtractor = extractor;
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

        internal static void CancelQueuedCountBatches(GpuSurfaceExtractionContext context)
        {
            // Called before context-owned resources are released. Submitted lanes cannot be
            // compacted: their immutable descriptors and GPU offsets are already in flight.
            foreach (CountBatchLane lane in s_CountBatchLanes)
            {
                if (lane == null || lane.Submitted || lane.SummarySubmitted) continue;
                for (int record = lane.Count - 1; record >= 0; record--)
                    if (ReferenceEquals(lane.Contexts[record], context))
                    {
                        if (lane.PreparingSummaries) lane.ResetSummaryPreparation();
                        RemoveQueuedRecord(lane, record);
                    }
            }
        }

        private static void RemoveQueuedRecord(CountBatchLane lane, int record)
        {
            for (int next = record + 1; next < lane.Count; next++)
            {
                lane.Contexts[next - 1] = lane.Contexts[next];
                lane.Tokens[next - 1] = lane.Tokens[next];
                lane.Requests[next - 1] = lane.Requests[next];
            }
            int last = --lane.Count;
            lane.Contexts[last] = null;
            lane.Tokens[last] = 0;
            lane.Requests[last] = default;
            if (lane.Count == 0)
                ResetCountBatchLane(lane);
            else
            {
                // The old prefix may be the context being disposed. Every surviving record
                // was admitted with this same batch layout, so its owner can submit the lane.
                lane.PrefixExtractor = lane.Contexts[0].Extractor;
                lane.Tables = lane.Contexts[0].Tables;
            }
        }

        private static void EnsureCountBatchLanes()
        {
            for (int i = 0; i < s_CountBatchLanes.Length; i++)
            {
                if (s_CountBatchLanes[i] == null) s_CountBatchLanes[i] = new CountBatchLane();
                CountBatchLane lane = s_CountBatchLanes[i];
                lane.Outcomes ??= new ComputeBuffer(CountBatchCapacity, sizeof(uint) * 4,
                    ComputeBufferType.Structured);
                lane.Counters ??= new ComputeBuffer(
                    GpuSurfaceExtractor.BatchHeaderWords
                    + CountBatchCapacity * GpuSurfaceExtractor.BatchRecordWords,
                    sizeof(uint), ComputeBufferType.Structured);
            }
        }

        private static int s_ConsecutiveNearDispatches;
        private const int MaximumNearDispatchStreak = 8;

        private static void AdvanceCountBatches(int frame)
        {
            // Ready geometry normally wins over source preparation. A bounded near streak
            // still gives coarse work service under continuous traversal, without another
            // submission slot or a deeper GPU queue.
            bool coarseFirst = s_ConsecutiveNearDispatches >= MaximumNearDispatchStreak;
            for (int pass = 0; pass < 2; pass++)
            for (int laneIndex = 0; laneIndex < s_CountBatchLanes.Length; laneIndex++)
            {
                CountBatchLane lane = s_CountBatchLanes[laneIndex];
                if (lane == null) continue;
                if (lane.Submitted)
                {
                    if (pass == 0) CompleteSubmittedCountBatch(lane);
                    continue;
                }
                if (lane.Count == 0) continue;
                bool coarse = lane.Requests[0].SourceStep == 8;
                if (coarse != (pass == 0 ? coarseFirst : !coarseFirst)) continue;
                if (frame - lane.FirstDispatchFrame >= CountBatchMaxFillFrames)
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
            lane.ResetSummaryPreparation();
            lane.OutcomeReady = false;
            lane.OutcomeFailed = false;
            lane.Submitted = false;
            lane.CompletionFence = default;
            lane.CompletionFenceValid = false;
            lane.Count = 0;
            lane.FirstDispatchFrame = -1;
            lane.PrefixExtractor = null;
            lane.Tables = null;
        }

        private static void SealCountBatch(CountBatchLane lane)
        {
            if (lane == null || lane.Count == 0 || lane.Submitted) return;
            if (lane.PreparingSummaries)
            {
                if (lane.SummarySubmitted) return;
                bool stale = lane.SummaryFailed;
                for (int record = 0; record < lane.Count; record++)
                    stale |= lane.Contexts[record] == null
                        || !lane.Contexts[record].IsCurrentBatchRequest(lane.Tokens[record]);
                if (stale)
                {
                    for (int record = 0; record < lane.Count; record++)
                        lane.Contexts[record]?.FailPagedBatch(lane.Tokens[record]);
                    ResetCountBatchLane(lane);
                    return;
                }
            }
            for (int record = lane.Count - 1; record >= 0; record--)
                if (lane.Contexts[record] == null
                    || !lane.Contexts[record].IsCurrentBatchRequest(lane.Tokens[record]))
                {
                    lane.Contexts[record]?.FailPagedBatch(lane.Tokens[record]);
                    RemoveQueuedRecord(lane, record);
                }
            if (lane.Count == 0 || s_Mirror == null || s_Mirror.IsClearPending) return;
            if (s_PageArena == null)
                throw new InvalidOperationException(
                    "Production GPU extraction requires the GPU-owned page arena.");
            int frame = Time.frameCount;
            if (!lane.AdvanceSummaryPreparation(frame)) return;

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
            if (!TryReserveExtractionDispatch(frame, coarse: lane.Requests[0].SourceStep == 8)) return;

            if (!SystemInfo.supportsAsyncGPUReadback)
                throw new InvalidOperationException("GPU surface publication requires asynchronous render-control feedback.");
            lane.RetainSubmissionResources(s_Mirror, s_PageArena);
            bool outcomeCopied = false;
            try
            {
                s_PageArena.FlushHandleCommands(frame);
                lane.PrefixExtractor.DispatchCountBatch(
                    s_Mirror, lane.Tables, lane.Requests, lane.Count,
                    lane.Counters, lane.Resources, summariesPrepared: lane.PreparingSummaries);
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
                lane.CompletionFence = s_ExtractionFence;
                lane.CompletionFenceValid = true;

                s_PageArena.CopyBatchOutcomes(lane.Counters, lane.Outcomes, lane.Count,
                    GpuSurfaceExtractor.BatchRecordWords);
                outcomeCopied = true;
            }
            finally
            {
                // Even a partially issued chain owns its resources until an ordered callback.
                // Failed submission observes completion only, never incomplete outcome contents.
                lane.OutcomeReady = false;
                lane.OutcomeFailed = !outcomeCopied;
                lane.Submitted = true;
                if (outcomeCopied) AsyncGPUReadback.Request(lane.Outcomes, lane.OutcomeCallback);
                else
                    // Completion-only failure path: transfer one control-status word, never
                    // geometry counts or allocation totals from the counter buffer.
                    AsyncGPUReadback.Request(lane.Counters, sizeof(uint),
                        (GpuSurfaceExtractor.BatchHeaderWords + 10) * sizeof(uint),
                        lane.FailedSubmissionCallback);
                s_CountBatchReadbacks++;
            }
        }

        private static void CompleteSubmittedCountBatch(CountBatchLane lane)
        {
            if (lane == null || !lane.Submitted || !lane.OutcomeReady
                || (!lane.OutcomeFailed && (!lane.CompletionFenceValid || !lane.CompletionFence.passed)))
                return;

            for (int record = 0; record < lane.Count; record++)
            {
                GpuSurfaceExtractionContext context = lane.Contexts[record];
                if (context == null) continue;
                if (lane.OutcomeFailed)
                {
                    context.FailPagedBatch(lane.Tokens[record]);
                    continue;
                }
                GpuPagedBatchOutcome outcome = GpuPagedBatchOutcome.ParseCompact(
                    lane.OutcomeWords, record, lane.Requests[record]);
                if (outcome.IsReadyCandidate)
                {
                    if (!context.CompletePagedBatch(lane.Tokens[record], outcome.Handle))
                    {
                        context.FailPagedBatch(lane.Tokens[record]);
                        ResolveCandidate(outcome.Handle, outcome.Generation, false, Time.frameCount);
                    }
                }
                else
                {
                    if (outcome.Kind == GpuPagedBatchOutcomeKind.Exhausted) AllocationFailures++;
                    context.FailPagedBatch(lane.Tokens[record]);
                    if (!outcome.IsRetryable)
                        Debug.LogError($"GPU surface transaction rejected: {outcome}");
                }
            }
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
                // A world may retire while feedback is in flight. Its cached callback owns
                // buffer disposal until the copy completes; no teardown wait enters the frame.
                if ((lane.Submitted && !lane.OutcomeReady) || lane.SummarySubmitted)
                    lane.Retired = true;
                else
                    lane.ReleaseBuffers();
                s_CountBatchLanes[i] = null;
            }
            AllocationFailures = 0;
            s_CountBatchReadbacks = 0;
            s_CountBatchRecords = 0;
            s_CountBatchArenaWaits = 0;
        }

        internal static ulong RequestCoverage(int3 brickCacheOrigin, int brickCacheEdge,
                                             int3 coreMinVoxel,
                                             int3 coreMaxVoxelExclusive)
        {
            if (brickCacheEdge > 0) ChangeDemandFootprint(brickCacheOrigin, brickCacheEdge, 1);
            return RequestEditWatch(brickCacheOrigin, brickCacheEdge);
        }

        internal static void ReleaseCoverage(int3 brickCacheOrigin, int brickCacheEdge,
                                             int3 coreMinVoxel,
                                             int3 coreMaxVoxelExclusive, ulong worldEpoch)
        {
            if (worldEpoch != s_ResourceWorldEpoch || brickCacheEdge <= 0) return;
            ChangeDemandFootprint(brickCacheOrigin, brickCacheEdge, -1);
            ReleaseEditWatch(brickCacheOrigin, brickCacheEdge, worldEpoch);

            if (s_DemandFootprints.Count == 0) ClearRecoveryQueues();
        }

        // Summary accumulation watches the whole request for edits without keeping every source
        // resident. Source portions separately own demand/active leases through GPU completion.
        internal static ulong RequestEditWatch(int3 origin, int edge)
        {
            if (edge > 0)
            {
                var footprint = new ActiveFootprint(origin, edge);
                s_EditWatchReaders.TryGetValue(footprint, out int readers);
                if (readers == 0) s_EditWatchEpochs[footprint] = s_CoverageEpoch;
                s_EditWatchReaders[footprint] = readers + 1;
            }
            return s_ResourceWorldEpoch;
        }

        internal static void ReleaseEditWatch(int3 origin, int edge, ulong worldEpoch)
        {
            if (worldEpoch != s_ResourceWorldEpoch || edge <= 0) return;
            var footprint = new ActiveFootprint(origin, edge);
            if (!s_EditWatchReaders.TryGetValue(footprint, out int readers)) return;
            if (readers > 1) s_EditWatchReaders[footprint] = readers - 1;
            else
            {
                s_EditWatchReaders.Remove(footprint);
                s_EditWatchEpochs.Remove(footprint);
            }
        }

        internal static ulong RequestSourceRange(int3 origin, int3 extent)
        {
            ValidateSourceRange(extent);
            ChangeDemandFootprint(origin, extent, 1);
            return s_ResourceWorldEpoch;
        }

        internal static void ReleaseSourceRange(int3 origin, int3 extent, ulong worldEpoch)
        {
            if (worldEpoch != s_ResourceWorldEpoch) return;
            ValidateSourceRange(extent);
            ChangeDemandFootprint(origin, extent, -1);
            if (s_DemandFootprints.Count == 0) ClearRecoveryQueues();
        }

        private static void ValidateSourceRange(int3 extent)
        {
            const int maximum = GpuBlockHlodSummary.MaximumBlocksPerDispatch;
            if (math.any(extent < 1 | extent > maximum)
                || (long)extent.x * extent.y * extent.z > maximum)
                throw new ArgumentOutOfRangeException(nameof(extent));
        }

        internal static bool Covers(int3 brickCacheOrigin, int brickCacheEdge,
                                    int3 coreMinVoxel, int3 coreMaxVoxelExclusive,
                                    ulong requiredGeneration, ref int scanCursor,
                                    ref bool roundIncomplete) =>
            Covers(brickCacheOrigin, new int3(brickCacheEdge), coreMinVoxel, coreMaxVoxelExclusive,
                requiredGeneration, ref scanCursor, ref roundIncomplete);

        private static bool Covers(int3 brickCacheOrigin, int3 extent,
            int3 coreMinVoxel, int3 coreMaxVoxelExclusive, ulong requiredGeneration,
            ref int scanCursor, ref bool roundIncomplete)
        {
            s_CoveragePolls++;
            if (s_Storage == null || s_Mirror == null || s_Mirror.IsClearPending || math.any(extent <= 0))
                return false;
            if (requiredGeneration < s_KnownRegionHistoryFromVersion)
            {
                s_HistoryCoverageRejects++;
                return false;
            }

            int regionShift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int blockShift = VoxelReadGrid.BlockEdgeLog2;
            int blockCount = extent.x * extent.y * extent.z;
            int stop = Math.Min(blockCount, scanCursor + CoverageChecksPerPoll);
            for (; scanCursor < stop; scanCursor++)
            {
                int x = scanCursor % extent.x;
                int yz = scanCursor / extent.x;
                int y = yz % extent.y;
                int z = yz / extent.y;
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

        internal static bool TryBeginExtraction(int3 brickCacheOrigin, int brickCacheEdge, out ulong worldEpoch)
        {
            worldEpoch = s_ResourceWorldEpoch;
            if (s_Mirror != null && s_Mirror.IsClearPending) return false;
            // A direct ComputeShader dispatch shares Metal's graphics queue with rendering. Do not
            // admit more complete count/write/copy chains than one count lane can service; deeper
            // queues increase presentation latency without increasing useful parallelism.
            if (s_ActiveExtractionCount >= MaxConcurrentExtractionChains) return false;
            s_ActiveExtractionCount++;
            ChangeActiveFootprint(brickCacheOrigin, brickCacheEdge, 1);
            ChangeActiveRegionReaders(brickCacheOrigin, brickCacheEdge, 1);
            return true;
        }

        internal static bool TryReserveExtractionDispatch(int frame, bool coarse = false)
        {
            // Metal still serializes the large extraction kernels even when their outputs are
            // private. Keep each count or ordered write/copy/publication chain globally bounded to
            // one stage per frame; multiple large stages caused 80-305 ms traversal stalls.
            if (s_LastExtractionDispatchFrame == frame) return false;
            s_LastExtractionDispatchFrame = frame;
            s_ConsecutiveNearDispatches = coarse ? 0
                : Math.Min(MaximumNearDispatchStreak, s_ConsecutiveNearDispatches + 1);
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

        internal static void EndExtraction(int3 brickCacheOrigin, int brickCacheEdge, ulong worldEpoch)
        {
            if (worldEpoch != s_ResourceWorldEpoch) return;
            if (s_ActiveExtractionCount > 0) s_ActiveExtractionCount--;
            ChangeActiveFootprint(brickCacheOrigin, brickCacheEdge, -1);
            ChangeActiveRegionReaders(brickCacheOrigin, brickCacheEdge, -1);
        }

        internal static string RecoveryState => $"regions={s_RecoveryRegions.Count}/{s_QueuedRecoveryRegions.Count}"
            + $" mixed={ResidentMixedBrickCount}/{MirrorSlotCapacity} noSlot={s_Mirror?.RefusedNoSlot ?? 0} directory={s_Mirror?.DirectoryCapacity ?? 0} dirRefused={s_Mirror?.DirectoryRefusals ?? 0} dirProbes={s_Mirror?.DirectoryProbeChecks ?? 0}"
            + $" stale={s_Mirror?.RejectedStale ?? 0} lastFailure={s_LastRecoveryFailure}"
            + $" recovery[active={s_RecoveryActiveSkips} borrow={s_RecoveryBorrowMisses} block={s_RecoveryBlockMisses} published={s_RecoveryPublished} lastRegion={s_LastRecoveryBlockedRegion} calls={s_RecoveryCalls} deadlineSkips={s_RecoveryDeadlineSkips}]";
        private static GpuBrickPublish s_LastRecoveryFailure;
        private static ulong s_RecoveryActiveSkips, s_RecoveryBorrowMisses, s_RecoveryBlockMisses, s_RecoveryPublished;
        private static int3 s_LastRecoveryBlockedRegion;
        private static ulong s_RecoveryCalls, s_RecoveryDeadlineSkips;
        internal static int ReadyRegionCount => s_ReadyBlocksByRegion.Count;
        internal static int ReadyBlockCount => s_ReadyBlocks.Count;
        internal static int PendingBlockCount => s_PendingBlocks.Count;
        internal static int MirrorSlotCapacity => s_Mirror?.SlotCapacity ?? 0;
        internal static int ResidentMixedBrickCount => s_Mirror?.ResidentBricks ?? 0;
        internal static int ActiveRegionCount => s_ActiveRegionReaders.Count;
        internal static int ActiveExtractions => s_ActiveExtractionCount;
        internal static ulong MirroredVersion => s_MirroredVersion;
        internal static uint CoverageEpoch => s_CoverageEpoch;
        internal static uint CoverageEpochFor(int3 origin, int edge) =>
            s_EditWatchEpochs.TryGetValue(new ActiveFootprint(origin, edge), out uint epoch)
                ? epoch : s_CoverageEpoch;

        private static void InvalidateCoverage(int3 minBlock, int3 maxBlockExclusive, bool all = false)
        {
            unchecked { s_CoverageEpoch++; }
            foreach (ActiveFootprint footprint in s_EditWatchReaders.Keys)
                if (all || math.all(footprint.Origin < maxBlockExclusive
                    & footprint.Origin + footprint.Extent > minBlock))
                    s_EditWatchEpochs[footprint] = s_CoverageEpoch;
        }
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
            InvalidateCoverage(default, default, all: true);
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

            bool wholeRegion = math.any(change.MaxVoxelExclusive <= change.MinVoxel);
            int3 min = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 max = wholeRegion
                ? default
                : (change.MaxVoxelExclusive - 1) >> VoxelReadGrid.BlockEdgeLog2;
            int3 regionMin = change.Region << VoxelReadGrid.BlocksPerRegionEdgeLog2;
            InvalidateCoverage(wholeRegion ? regionMin : min,
                wholeRegion ? regionMin + VoxelReadGrid.BlocksPerRegionEdge : max + 1);
            if (!s_ReadyBlocksByRegion.TryGetValue(change.Region, out HashSet<int3> readyInRegion))
                return;
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
                s_Mirror?.InvalidateReadiness(block);
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
                        s_RecoveryActiveSkips++;
                        s_LastRecoveryBlockedRegion = region;
                        blocks.Enqueue(worldBlock);
                        continue;
                    }

                    madeProgress = true;
                    s_PendingBlocks.Remove(worldBlock);
                    int3 localBlock = worldBlock
                        - (region << VoxelReadGrid.BlocksPerRegionEdgeLog2);
                    if (!resident)
                    {
                        s_RecoveryBorrowMisses++;
                        s_LastRecoveryBlockedRegion = region;
                        s_Mirror.Remove(worldBlock);
                        RemoveReadyBlock(worldBlock);
                        RecordConcurrentRecoveryProgress(ref concurrentProgressRecorded);
                        continue;
                    }
                    if (!view.TryGetBlock(localBlock, out VoxelReadBlock block))
                    {
                        s_RecoveryBlockMisses++;
                        s_LastRecoveryBlockedRegion = region;
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
                    if (result is GpuBrickPublish.NoSlot or GpuBrickPublish.DirectoryFull or GpuBrickPublish.PayloadMissing
                        or GpuBrickPublish.Stale)
                    {
                        s_LastRecoveryFailure = result;
                        QueueRecoveryBlock(worldBlock);
                        RequeueRegion(region);
                        return;
                    }

                    s_RecoveryPublished++;
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
            bool reclaimed = result == GpuBrickPublish.DirectoryFull
                ? TryEvictInactiveDirectoryBlock()
                : result == GpuBrickPublish.NoSlot && TryEvictInactiveMixedBlock();
            if (!reclaimed) return result;
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
        private static int s_DirectoryEvictionCursor;

        private static bool TryEvictInactiveDirectoryBlock()
        {
            // Directory capacity includes uniform keys, which never enter the mixed-slot LRU.
            // Scan a bounded slice of the actual directory and retain its cursor across retries.
            for (int checkedEntries = 0; checkedEntries < 64; checkedEntries++)
            {
                int index = s_DirectoryEvictionCursor & s_Mirror.DirectoryMask;
                // An odd stride visits every bucket of the power-of-two table while spreading
                // victims across its hash range; a linear sweep concentrates live-key clusters.
                s_DirectoryEvictionCursor = unchecked(index + (int)0x9E3779B9u) & s_Mirror.DirectoryMask;
                if (!s_Mirror.TryReadDirectoryCoordinate(index, out int3 block)
                    || IsBlockDemanded(block) || IsBlockActive(block)) continue;
                RemoveReadyBlock(block);
                s_MixedReadyBlocks.Remove(block);
                s_Mirror.Remove(block);
                return true;
            }
            return false;
        }

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

        private static void ChangeDemandFootprint(int3 origin, int edge, int delta) =>
            ChangeDemandFootprint(origin, new int3(edge), delta);

        private static void ChangeDemandFootprint(int3 origin, int3 extent, int delta)
        {
            var footprint = new ActiveFootprint(origin, extent);
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
                int3 end = footprint.Origin + footprint.Extent;
                if (math.all(block >= footprint.Origin & block < end)) return true;
            }
            return false;
        }

        private static void AddReadyBlock(int3 block)
        {
            if (s_ReadyBlocks.Contains(block)) return;
            if (s_ReadyBlocks.Count >= TrackedBlockCapacity)
                TryEvictInactiveReadyBlock(Time.frameCount);
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

        internal static ulong ReadyEvictionChecks { get; private set; }
        private static int s_LastReadyEvictionMissFrame = -1;

        private static bool TryEvictInactiveReadyBlock(int frame)
        {
            // Once a bounded slice finds only protected records, retry next frame. Repeating
            // the same failed walk for every incoming brick consumes recovery's entire budget.
            if (s_LastReadyEvictionMissFrame == frame) return false;
            // Coarse footprints legitimately pin more entries than the cleanup target. Walk
            // the queue incrementally: a full scan for every added brick becomes quadratic
            // and prevents those footprints from ever reaching dispatch. The queue retains
            // its cursor, and demanded/active source records remain protected.
            int attempts = Math.Min(64, s_ReadyResidencyOrder.Count);
            while (attempts-- > 0 && s_ReadyResidencyOrder.Count > 0)
            {
                ReadyEvictionChecks++;
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
            s_LastReadyEvictionMissFrame = frame;
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

        private static void ChangeActiveRegionReaders(int3 brickCacheOrigin, int brickCacheEdge, int delta) =>
            ChangeActiveRegionReaders(brickCacheOrigin, new int3(brickCacheEdge), delta);

        private static void ChangeActiveRegionReaders(int3 brickCacheOrigin, int3 extent, int delta)
        {
            if (math.any(extent <= 0) || delta == 0) return;
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int3 first = brickCacheOrigin >> shift;
            int3 last = (brickCacheOrigin + extent - 1) >> shift;
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

        private static void ChangeActiveFootprint(int3 brickCacheOrigin, int brickCacheEdge, int delta) =>
            ChangeActiveFootprint(brickCacheOrigin, new int3(brickCacheEdge), delta);

        private static void ChangeActiveFootprint(int3 brickCacheOrigin, int3 extent, int delta)
        {
            if (math.any(extent <= 0) || delta == 0) return;
            var footprint = new ActiveFootprint(brickCacheOrigin, extent);
            s_ActiveFootprints.TryGetValue(footprint, out int readers);
            readers += delta;
            if (readers > 0) s_ActiveFootprints[footprint] = readers;
            else s_ActiveFootprints.Remove(footprint);
        }

        private static bool IsBlockActive(int3 block)
        {
            foreach (ActiveFootprint footprint in s_ActiveFootprints.Keys)
            {
                int3 end = footprint.Origin + footprint.Extent;
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
            s_LastReadyEvictionMissFrame = -1;
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

        private static ulong s_ResourceWorldEpoch;
        internal static ulong ResourceWorldEpoch => s_ResourceWorldEpoch;

        private static void ResetWorld(bool disposeMirror)
        {
            s_ResourceWorldEpoch = checked(s_ResourceWorldEpoch + 1);
            s_Storage = null;
            s_ChangeSource = null;
            s_ChangeCursor = 0;
            s_MirroredVersion = 0;
            s_KnownRegionHistoryFromVersion = 0;
            s_ActiveExtractionCount = 0;
            s_LastPrepareFrame = -1;
            s_LastExtractionDispatchFrame = -1;
            s_ConsecutiveNearDispatches = 0;
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
            s_DirectoryEvictionCursor = 0;
            s_DemandFootprints.Clear();
            s_EditWatchEpochs.Clear();
            s_EditWatchReaders.Clear();
            s_Changes.Clear();

            if (!disposeMirror || s_Mirror == null) return;
            s_Mirror.Dispose();
            s_Mirror = null;
        }
    }
}
