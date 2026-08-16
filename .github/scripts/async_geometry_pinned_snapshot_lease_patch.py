from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

# -----------------------------------------------------------------------------
# Storage API: opaque generation-stamped mixed-block read lease.
# -----------------------------------------------------------------------------
api_root = Path('Assets/VoxelEngine/Storage/Api')
lease_path = api_root / 'PinnedVoxelReadBlock.cs'
lease_path.write_text(r'''using Unity.Collections;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Opaque release token for one pinned physical mixed-brick version. Consumers may retain the
    /// token but cannot address Storage slots through it; Storage validates the generation when
    /// the lease is released so a stale token can never affect a recycled slot.
    /// </summary>
    public readonly struct VoxelReadPinToken
    {
        internal readonly int Slot;
        public readonly uint Generation;
        public bool IsValid => Slot >= 0 && Generation != 0;

        internal VoxelReadPinToken(int slot, uint generation)
        {
            Slot = slot;
            Generation = generation;
        }
    }

    /// <summary>
    /// Stable read-only view of one logical 8^3 block. Empty/uniform blocks require no physical
    /// lease. Mixed blocks pin one COW BrickPool version and expose Storage-owned native payload
    /// arrays plus the immutable voxel offset used by Burst jobs. Consumers must never dispose or
    /// write the arrays and must release a valid <see cref="Pin"/> through the source that created
    /// it after every dependent job has finished.
    /// </summary>
    public readonly struct PinnedVoxelReadBlock
    {
        public readonly VoxelReadBlockKind Kind;
        public readonly byte UniformMaterial;
        public readonly int MixedOffset;
        public readonly NativeArray<byte> MixedVoxels;
        public readonly NativeArray<ushort> MixedSurfaceSemantics;
        public readonly NativeArray<byte> MixedBoundarySamples;
        public readonly VoxelReadPinToken Pin;

        public bool HasPinnedPayload => Pin.IsValid;

        internal PinnedVoxelReadBlock(VoxelReadBlockKind kind, byte uniformMaterial,
                                      int mixedOffset,
                                      NativeArray<byte> mixedVoxels,
                                      NativeArray<ushort> mixedSurfaceSemantics,
                                      NativeArray<byte> mixedBoundarySamples,
                                      in VoxelReadPinToken pin)
        {
            Kind = kind;
            UniformMaterial = uniformMaterial;
            MixedOffset = mixedOffset;
            MixedVoxels = mixedVoxels;
            MixedSurfaceSemantics = mixedSurfaceSemantics;
            MixedBoundarySamples = mixedBoundarySamples;
            Pin = pin;
        }

        internal static PinnedVoxelReadBlock Empty => new(
            VoxelReadBlockKind.Empty, VoxelGrid.MaterialEmpty, 0,
            default, default, default, default);

        internal static PinnedVoxelReadBlock Uniform(byte material) => new(
            VoxelReadBlockKind.Uniform, material, 0,
            default, default, default, default);
    }
}
''')
(api_root / 'PinnedVoxelReadBlock.cs.meta').write_text(
    'fileFormatVersion: 2\nguid: 44f27f4c60e34750be3176aa3cc7e31d\n')

read_source_path = api_root / 'IRegionReadSource.cs'
s = read_source_path.read_text()
s = once(s,
'''        bool TryAcquireRegion(int3 regionCoord, out RegionReadView view);

        /// <summary>
        /// Copies compact per-block occupancy state''',
'''        bool TryAcquireRegion(int3 regionCoord, out RegionReadView view);

        /// <summary>
        /// Acquires one stable logical read block. Mixed payloads are pinned copy-on-write
        /// versions that may safely outlive later world edits/region eviction and be read by jobs.
        /// Empty and uniform blocks carry no pin. A valid pin must be released after the final
        /// dependent job; the backing arrays are Storage-owned and must never be disposed/written.
        /// </summary>
        bool TryPinWorldBlock(int3 worldBlockCoord, out PinnedVoxelReadBlock block);

        /// <summary>Releases a mixed-block pin previously returned by this source.</summary>
        void ReleasePinnedWorldBlock(in VoxelReadPinToken token);

        /// <summary>
        /// Copies compact per-block occupancy state''', 'pinned read API')
read_source_path.write_text(s)

# -----------------------------------------------------------------------------
# Storage runtime implementation.
# -----------------------------------------------------------------------------
source_path = Path('Assets/VoxelEngine/Storage/Runtime/RegionReadSource.cs')
s = source_path.read_text()
anchor = '''        public bool TryAcquireRegion(int3 regionCoord, out RegionReadView view)
        {
'''
idx = s.index(anchor)
# Insert methods before TryAcquireRegion so public read capabilities stay together.
methods = r'''        public bool TryPinWorldBlock(int3 worldBlockCoord, out PinnedVoxelReadBlock block)
        {
            int3 regionCoord = worldBlockCoord >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!_table.TryGetRegion(regionCoord, out Region region))
            {
                block = default;
                return false;
            }

            int3 local = worldBlockCoord & VoxelReadGrid.BlocksPerRegionEdgeMask;
            BrickRef brick = region.BrickRefs[Region.BrickIndex(local.x, local.y, local.z)];
            if (brick.IsEmpty)
            {
                block = PinnedVoxelReadBlock.Empty;
                return true;
            }
            if (brick.IsUniform)
            {
                block = PinnedVoxelReadBlock.Uniform(brick.UniformMaterial);
                return true;
            }

            BrickPool.PinToken physicalPin = _pool.Pin(brick.PoolIndex);
            var apiPin = new VoxelReadPinToken(physicalPin.BrickIndex,
                                               physicalPin.Generation);
            block = new PinnedVoxelReadBlock(
                VoxelReadBlockKind.Mixed,
                VoxelGrid.MaterialEmpty,
                brick.PoolIndex * VoxelReadGrid.VoxelsPerBlock,
                _pool.Voxels,
                _pool.SurfaceSemantics,
                _pool.BoundarySamples,
                in apiPin);
            return true;
        }

        public void ReleasePinnedWorldBlock(in VoxelReadPinToken token)
        {
            if (!token.IsValid) return;
            var physicalPin = new BrickPool.PinToken(token.Slot, token.Generation);
            _pool.Unpin(in physicalPin);
        }

'''
s = s[:idx] + methods + s[idx:]
source_path.write_text(s)

# -----------------------------------------------------------------------------
# Build workspace: preallocated pin-token storage. Keep the old zero-length native mixed lists as
# job-safety fallback for uniform-only chunks until the final cleanup removes them entirely.
# -----------------------------------------------------------------------------
workspace_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/TransvoxelBuildWorkspace.cs')
w = workspace_path.read_text()
w = once(w,
'''using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;''',
'''using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;''', 'workspace storage api import')
w = once(w,
'''        internal readonly NativeList<byte> DensityMixedBoundarySamples;

        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''',
'''        internal readonly NativeList<byte> DensityMixedBoundarySamples;
        internal readonly NativeList<VoxelReadPinToken> PinnedReadBlocks;

        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''', 'workspace pin list field')
w = once(w,
'''            DensityMixedBoundarySamples = new NativeList<byte>(64 * 1024,
                                                               Allocator.Persistent);

            CompactedTopologyVertices''',
'''            DensityMixedBoundarySamples = new NativeList<byte>(64 * 1024,
                                                               Allocator.Persistent);
            PinnedReadBlocks = new NativeList<VoxelReadPinToken>(
                brickCacheCount > 0 ? brickCacheCount : 1, Allocator.Persistent);

            CompactedTopologyVertices''', 'workspace pin list allocation')
w = once(w,
'''            if (DensityMixedBoundarySamples.IsCreated) DensityMixedBoundarySamples.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();''',
'''            if (DensityMixedBoundarySamples.IsCreated) DensityMixedBoundarySamples.Dispose();
            if (PinnedReadBlocks.IsCreated) PinnedReadBlocks.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();''', 'workspace pin list dispose')
workspace_path.write_text(w)

# -----------------------------------------------------------------------------
# Solid cache: pin only mixed bricks, read Storage pool arrays directly, and drain releases under
# the same worker deadline after the last dependent job completes.
# -----------------------------------------------------------------------------
cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
c = cache_path.read_text()
c = once(c,
'''        private NativeList<byte> _densityMixedBoundarySamples;
        private NativeArray<byte> _mipSampleOccupancy;''',
'''        private NativeList<byte> _densityMixedBoundarySamples;
        private NativeList<VoxelReadPinToken> _pinnedReadBlocks;
        private IRegionReadSource _pinnedReadSource;
        private NativeArray<byte> _pinnedMixedVoxels;
        private NativeArray<ushort> _pinnedMixedSurfaceSemantics;
        private NativeArray<byte> _pinnedMixedBoundarySamples;
        private int _pinnedReleaseCursor;
        private bool _discardBuildAfterPinRelease;
        private NativeArray<byte> _mipSampleOccupancy;''', 'cache pinned snapshot fields')

c = once(c,
'''            _densityMixedBoundarySamples = _workspace.DensityMixedBoundarySamples;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;''',
'''            _densityMixedBoundarySamples = _workspace.DensityMixedBoundarySamples;
            _pinnedReadBlocks = _workspace.PinnedReadBlocks;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;''', 'cache pin list alias')

# Stale sliced snapshots must drain pins incrementally before resetting/reusing the workspace.
c = once(c,
'''            if (_build.Active && !_build.SnapshotTaken
                && _desiredVersions.TryGetValue(_build.Coordinate, out ulong slicedDesired)
                && slicedDesired > _build.SourceVersion)
            {
                StaleBuildCount++;
                ResetCompletedBuild();
            }

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            do''',
'''            if (_build.Active && !_build.SnapshotTaken
                && _desiredVersions.TryGetValue(_build.Coordinate, out ulong slicedDesired)
                && slicedDesired > _build.SourceVersion)
                _discardBuildAfterPinRelease = true;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            if (_discardBuildAfterPinRelease)
            {
                if (!StepReleasePinnedSnapshotBlocks(deadline)) return;
                StaleBuildCount++;
                _discardBuildAfterPinRelease = false;
                ResetCompletedBuild();
            }

            do''', 'bounded stale pin release')

# Empty result: drain halo pins under deadline before moving on.
c = once(c,
'''                    if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
                    {
                        _build.Phase = 3;
                        _build.Cursor = 0;
                        continue;
                    }''',
'''                    if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
                    {
                        if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                        _build.Phase = 3;
                        _build.Cursor = 0;
                        continue;
                    }''', 'empty snapshot pin release')

# Job-complete paths move through phase 6 so unpin work is budgeted rather than executed in one
# completion spike. BeginCompletedResultAppend can safely initialize cursors before releases drain.
c = once(c,
'''                    BeginCompletedResultAppend(includeTopology: true);
                    _build.Phase = 5;
                    continue;''',
'''                    BeginCompletedResultAppend(includeTopology: true);
                    _build.Phase = 6;
                    continue;''', 'continuous pin release phase')
c = once(c,
'''                    BeginCompletedResultAppend(includeTopology: false);
                    _build.Phase = 5;
                    continue;
                }

                if (_build.Phase == 5)''',
'''                    BeginCompletedResultAppend(includeTopology: false);
                    _build.Phase = 6;
                    continue;
                }

                if (_build.Phase == 6)
                {
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                    _build.Phase = 5;
                    continue;
                }

                if (_build.Phase == 5)''', 'faceted pin release phase')

# Exact snapshot initialization establishes one source owner and stable pool-array aliases.
c = once(c,
'''            if (!_build.SnapshotInitialised)
            {
                _densityMixedVoxels.Clear();
                _densityMixedSurfaceSemantics.Clear();
                _densityMixedBoundarySamples.Clear();
                _buildSurfaceCatalogue = _surfaceCatalogue;''',
'''            if (!_build.SnapshotInitialised)
            {
                if (_pinnedReadBlocks.Length != 0)
                    throw new InvalidOperationException(
                        "Cannot begin a new exact snapshot while previous Storage pins remain.");
                _densityMixedVoxels.Clear();
                _densityMixedSurfaceSemantics.Clear();
                _densityMixedBoundarySamples.Clear();
                _pinnedReadSource = source;
                _pinnedReleaseCursor = 0;
                _pinnedMixedVoxels = default;
                _pinnedMixedSurfaceSemantics = default;
                _pinnedMixedBoundarySamples = default;
                _buildSurfaceCatalogue = _surfaceCatalogue;''', 'exact snapshot pin initialization')

# Jobs read pinned Storage pool arrays. Uniform-only builds use the created zero-length fallback
# arrays to satisfy Unity job container validation without pinning anything.
c = once(c,
'''                    MixedVoxels = _densityMixedVoxels.AsArray(),
                    MixedSurfaceSemantics = _densityMixedSurfaceSemantics.AsArray(),
                    MixedBoundarySamples = _densityMixedBoundarySamples.AsArray(),''',
'''                    MixedVoxels = PinnedMixedVoxelsOrFallback(),
                    MixedSurfaceSemantics = PinnedMixedSurfaceSemanticsOrFallback(),
                    MixedBoundarySamples = PinnedMixedBoundarySamplesOrFallback(),''', 'density job pinned arrays')
# SnapshotFacetedMaskJob has the same old triple; replace the remaining occurrence.
c = once(c,
'''                MixedVoxels = _densityMixedVoxels.AsArray(),
                MixedSurfaceSemantics = _densityMixedSurfaceSemantics.AsArray(),
                MixedBoundarySamples = _densityMixedBoundarySamples.AsArray(),''',
'''                MixedVoxels = PinnedMixedVoxelsOrFallback(),
                MixedSurfaceSemantics = PinnedMixedSurfaceSemanticsOrFallback(),
                MixedBoundarySamples = PinnedMixedBoundarySamplesOrFallback(),''', 'faceted snapshot pinned arrays')

# Classification reads the same immutable pool payload offsets recorded in the snapshot.
c = once(c,
'''            int endVoxel = brick.MixedOffset + VoxelReadGrid.VoxelsPerBlock;
            for (int voxel = brick.MixedOffset; voxel < endVoxel; voxel++)
            {
                byte material = _densityMixedVoxels[voxel];''',
'''            NativeArray<byte> mixedVoxels = PinnedMixedVoxelsOrFallback();
            NativeArray<ushort> mixedSurfaceSemantics = PinnedMixedSurfaceSemanticsOrFallback();
            NativeArray<byte> mixedBoundarySamples = PinnedMixedBoundarySamplesOrFallback();
            int endVoxel = brick.MixedOffset + VoxelReadGrid.VoxelsPerBlock;
            for (int voxel = brick.MixedOffset; voxel < endVoxel; voxel++)
            {
                byte material = mixedVoxels[voxel];''', 'classification pinned materials')
c = c.replace('_densityMixedSurfaceSemantics[voxel]', 'mixedSurfaceSemantics[voxel]', 1)
c = c.replace('_densityMixedBoundarySamples[voxel]', 'mixedBoundarySamples[voxel]', 1)

# Replace mixed payload copying with one pin + offset.
start = c.index('        private TransvoxelDensityBrick SnapshotBlock(')
end = c.index('        private bool StepCells(', start)
snapshot_method = r'''        private TransvoxelDensityBrick SnapshotBlock(IRegionReadSource source,
                                                      ref RegionSampleCursor cursor,
                                                      int3 worldBlock)
        {
            if (!TryAcquireWorldBlock(source, ref cursor, worldBlock, out RegionReadView region)
                || !region.TryGetWorldBlock(worldBlock, out VoxelReadBlock block)
                || block.Kind == VoxelReadBlockKind.Empty)
                return default;

            if (block.Kind == VoxelReadBlockKind.Uniform)
            {
                return new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = block.UniformMaterial,
                    MixedOffset = 0
                };
            }

            if (!source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned)
                || pinned.Kind != VoxelReadBlockKind.Mixed || !pinned.HasPinnedPayload)
                throw new InvalidOperationException(
                    $"Failed to pin mixed Storage read block {worldBlock}.");

            if (!_pinnedMixedVoxels.IsCreated)
            {
                _pinnedMixedVoxels = pinned.MixedVoxels;
                _pinnedMixedSurfaceSemantics = pinned.MixedSurfaceSemantics;
                _pinnedMixedBoundarySamples = pinned.MixedBoundarySamples;
            }
            else if (_pinnedMixedVoxels.Length != pinned.MixedVoxels.Length
                     || _pinnedMixedSurfaceSemantics.Length != pinned.MixedSurfaceSemantics.Length
                     || _pinnedMixedBoundarySamples.Length != pinned.MixedBoundarySamples.Length)
            {
                // A build must never splice payloads from different physical Storage pools.
                source.ReleasePinnedWorldBlock(in pinned.Pin);
                throw new InvalidOperationException(
                    "Pinned read blocks came from incompatible Storage backing arrays.");
            }

            _pinnedReadBlocks.Add(pinned.Pin);
            return new TransvoxelDensityBrick
            {
                Kind = 2,
                UniformMaterial = 0,
                MixedOffset = pinned.MixedOffset
            };
        }

        private NativeArray<byte> PinnedMixedVoxelsOrFallback() =>
            _pinnedMixedVoxels.IsCreated
                ? _pinnedMixedVoxels : _densityMixedVoxels.AsArray();

        private NativeArray<ushort> PinnedMixedSurfaceSemanticsOrFallback() =>
            _pinnedMixedSurfaceSemantics.IsCreated
                ? _pinnedMixedSurfaceSemantics : _densityMixedSurfaceSemantics.AsArray();

        private NativeArray<byte> PinnedMixedBoundarySamplesOrFallback() =>
            _pinnedMixedBoundarySamples.IsCreated
                ? _pinnedMixedBoundarySamples : _densityMixedBoundarySamples.AsArray();

        private const int PinnedReleasesPerDeadlineCheck = 64;

        /// <summary>
        /// Releases immutable Storage payload versions incrementally. Job completion is not
        /// allowed to turn into a large frame-thread unpin loop; slow release merely delays the
        /// next build while the previous published geometry remains valid.
        /// </summary>
        private bool StepReleasePinnedSnapshotBlocks(double deadlineSeconds)
        {
            if (_pinnedReadBlocks.Length == 0)
            {
                ClearPinnedSnapshotState();
                return true;
            }
            if (_pinnedReadSource == null)
                throw new InvalidOperationException("Pinned snapshot lost its Storage source.");

            while (_pinnedReleaseCursor < _pinnedReadBlocks.Length)
            {
                int end = math.min(_pinnedReadBlocks.Length,
                                   _pinnedReleaseCursor + PinnedReleasesPerDeadlineCheck);
                for (; _pinnedReleaseCursor < end; _pinnedReleaseCursor++)
                {
                    VoxelReadPinToken token = _pinnedReadBlocks[_pinnedReleaseCursor];
                    _pinnedReadSource.ReleasePinnedWorldBlock(in token);
                }
                if (_pinnedReleaseCursor < _pinnedReadBlocks.Length
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    return false;
            }

            _pinnedReadBlocks.Clear();
            ClearPinnedSnapshotState();
            return true;
        }

        private void ReleasePinnedSnapshotBlocksImmediate()
        {
            if (_pinnedReadSource != null)
            {
                for (int i = _pinnedReleaseCursor; i < _pinnedReadBlocks.Length; i++)
                {
                    VoxelReadPinToken token = _pinnedReadBlocks[i];
                    _pinnedReadSource.ReleasePinnedWorldBlock(in token);
                }
            }
            _pinnedReadBlocks.Clear();
            ClearPinnedSnapshotState();
        }

        private void ClearPinnedSnapshotState()
        {
            _pinnedReadSource = null;
            _pinnedMixedVoxels = default;
            _pinnedMixedSurfaceSemantics = default;
            _pinnedMixedBoundarySamples = default;
            _pinnedReleaseCursor = 0;
        }

'''
c = c[:start] + snapshot_method + c[end:]

# Reset only occurs once release has completed; reset the discard marker too.
c = once(c,
'''        private void ResetCompletedBuild()
        {
            _pendingUpload = false;
            _build = default;''',
'''        private void ResetCompletedBuild()
        {
            if (_pinnedReadBlocks.Length != 0)
                throw new InvalidOperationException(
                    "Build reset attempted before pinned Storage payloads were released.");
            _pendingUpload = false;
            _discardBuildAfterPinRelease = false;
            _build = default;''', 'reset pin invariant')

# Out-of-window/residency removal defers until bounded pin release happens in Prepare.
c = once(c,
'''            if (_build.Active && _build.Coordinate.Equals(chunk)
                && !ScheduledJobsComplete())
                return false;

            _known.Remove(chunk);''',
'''            if (_build.Active && _build.Coordinate.Equals(chunk)
                && !ScheduledJobsComplete())
                return false;
            if (_build.Active && _build.Coordinate.Equals(chunk)
                && _pinnedReadBlocks.Length > 0)
            {
                // All handles were observed complete above, so this releases only Unity job
                // safety state. Physical brick pins are drained later under the build deadline.
                CompleteJobs();
                _discardBuildAfterPinRelease = true;
                return false;
            }

            _known.Remove(chunk);''', 'residency defers pinned release')

# Teardown may synchronize/unpin immediately by contract.
c = once(c,
'''        public void Dispose()
        {
            CompleteJobs();
            foreach (Entry entry in _entries.Values) entry.Dispose();''',
'''        public void Dispose()
        {
            CompleteJobs();
            ReleasePinnedSnapshotBlocksImmediate();
            foreach (Entry entry in _entries.Values) entry.Dispose();''', 'teardown pin release')
cache_path.write_text(c)

# -----------------------------------------------------------------------------
# Jobs explicitly opt out of Unity whole-container alias restrictions. COW + generation pins are
# the synchronization contract: jobs read retired immutable slots while gameplay may write clones
# in other slots of the same physical NativeArray.
# -----------------------------------------------------------------------------
density_job_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/TransvoxelDensityJob.cs')
dj = density_job_path.read_text()
dj = once(dj,
'''using Unity.Collections;
using Unity.Jobs;''',
'''using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;''', 'density unsafe import')
dj = dj.replace('[ReadOnly] public NativeArray<byte> MixedVoxels;',
'''[NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedVoxels;''', 1)
dj = dj.replace('[ReadOnly] public NativeArray<ushort> MixedSurfaceSemantics;',
'''[NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<ushort> MixedSurfaceSemantics;''', 1)
dj = dj.replace('[ReadOnly] public NativeArray<byte> MixedBoundarySamples;',
'''[NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedBoundarySamples;''', 1)
dj = dj.replace(
'''    /// The main thread snapshots only the bricks surrounding the chunk and packs mixed-brick voxel
    /// payloads into a compact array. The job therefore performs no RegionTable hashing, no region
    /// lifetime access, and no BrickPool reads while gameplay can edit/evict the authoritative
    /// world. It is a pure read-only calculation over immutable snapshot data.''',
'''    /// The main thread snapshots only compact block kind/offset metadata. Mixed payloads remain in
    /// Storage-owned BrickPool arrays under generation-stamped COW pins, so gameplay edits publish
    /// clones while this job reads the immutable retired version. The job performs no RegionTable
    /// hashing or region-lifetime access and never copies 8^3 mixed payloads into renderer memory.''', 1)
density_job_path.write_text(dj)

faceted_job_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/SnapshotFacetedMaskJob.cs')
fj = faceted_job_path.read_text()
fj = fj.replace('[ReadOnly] public NativeArray<byte> MixedVoxels;',
'''[NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedVoxels;''', 1)
fj = fj.replace('[ReadOnly] public NativeArray<ushort> MixedSurfaceSemantics;',
'''[NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<ushort> MixedSurfaceSemantics;''', 1)
fj = fj.replace('[ReadOnly] public NativeArray<byte> MixedBoundarySamples;',
'''[NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedBoundarySamples;''', 1)
fj = fj.replace(
'    /// Builds all six exact planar face masks in one pass over a compact immutable brick snapshot.',
'    /// Builds all six exact planar face masks from compact block metadata plus COW-pinned immutable Storage payloads.', 1)
faceted_job_path.write_text(fj)

brick_job_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/TransvoxelDensityJob.cs')
# Update struct comment now that MixedOffset addresses the Storage pool instead of a packed list.
bj = brick_job_path.read_text().replace(
'        // 0 = empty, 1 = uniform, 2 = mixed payload in MixedVoxels.',
'        // 0 = empty, 1 = uniform, 2 = COW-pinned mixed payload at MixedOffset.', 1)
brick_job_path.write_text(bj)

# -----------------------------------------------------------------------------
# Behavioral Storage test + architecture regression guard.
# -----------------------------------------------------------------------------
test_path = Path('Assets/Tests/EditMode/StorageRenderingReadContractTests.cs')
t = test_path.read_text()
if 'PinnedMixedBlockRemainsImmutableAcrossAuthoritativeEdit' not in t:
    insert = r'''

        [Test]
        public void PinnedMixedBlockRemainsImmutableAcrossAuthoritativeEdit()
        {
            var table = new RegionTable(2, Allocator.Persistent);
            var pool = new BrickPool(8, Allocator.Persistent);
            try
            {
                int3 voxel = new int3(3, 4, 5);
                Assert.True(VoxelAccess.SetVoxel(ref table, ref pool, voxel, 6));
                var source = new RegionReadSource(in table, in pool);
                int3 worldBlock = voxel >> VoxelReadGrid.BlockEdgeLog2;
                Assert.True(source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned));
                Assert.AreEqual(VoxelReadBlockKind.Mixed, pinned.Kind);
                Assert.True(pinned.HasPinnedPayload);

                int3 inner = voxel & VoxelReadGrid.BlockEdgeMask;
                int voxelIndex = inner.x | (inner.y << 3) | (inner.z << 6);
                Assert.AreEqual(6, pinned.MixedVoxels[pinned.MixedOffset + voxelIndex]);

                Assert.True(VoxelAccess.SetVoxel(ref table, ref pool, voxel, 9));
                Assert.AreEqual(6, pinned.MixedVoxels[pinned.MixedOffset + voxelIndex],
                    "Pinned Storage payload changed after authoritative COW edit.");
                Assert.True(source.TryRead(voxel, out VoxelCell current));
                Assert.AreEqual(9, current.BaseMaterialId);

                VoxelReadPinToken token = pinned.Pin;
                source.ReleasePinnedWorldBlock(in token);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        [Test]
        public void UniformPinnedReadRequiresNoPhysicalLease()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(1, Allocator.Persistent);
            try
            {
                Region region = table.LoadRegion(int3.zero);
                region.BrickRefs[0] = BrickRef.Uniform(4);
                table.CommitRegion(in region);
                var source = new RegionReadSource(in table, in pool);
                Assert.True(source.TryPinWorldBlock(int3.zero, out PinnedVoxelReadBlock pinned));
                Assert.AreEqual(VoxelReadBlockKind.Uniform, pinned.Kind);
                Assert.AreEqual(4, pinned.UniformMaterial);
                Assert.False(pinned.HasPinnedPayload);
            }
            finally
            {
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }
'''
    marker = '\n        [Test]\n        public void WorldBlockCoordinatesRemainCorrectAcrossNegativeRegions()'
    pos = t.find(marker)
    if pos < 0:
        raise SystemExit('StorageRenderingReadContractTests insertion marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'ExactGeometrySnapshotsBorrowPinnedCowPayloads' not in a:
    insert = r'''

        [Test]
        public void ExactGeometrySnapshotsBorrowPinnedCowPayloads()
        {
            string api = File.ReadAllText(Path.Combine(
                Application.dataPath, "VoxelEngine", "Storage", "Api", "IRegionReadSource.cs"));
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string density = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "TransvoxelDensityJob.cs"));
            string faceted = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "SnapshotFacetedMaskJob.cs"));
            StringAssert.Contains("TryPinWorldBlock", api);
            StringAssert.Contains("ReleasePinnedWorldBlock", api);
            StringAssert.Contains("source.TryPinWorldBlock", cache);
            StringAssert.Contains("StepReleasePinnedSnapshotBlocks", cache);
            StringAssert.Contains("PinnedReleasesPerDeadlineCheck", cache);
            StringAssert.DoesNotContain("TryCopyWorldBlock(\n                    worldBlock", cache);
            StringAssert.DoesNotContain("ResizeUninitialized(nextLength)", cache);
            StringAssert.Contains("NativeDisableContainerSafetyRestriction", density);
            StringAssert.Contains("NativeDisableContainerSafetyRestriction", faceted);
        }
'''
    marker = '\n    }\n}'
    pos = a.rfind(marker)
    if pos < 0:
        raise SystemExit('architecture test closing marker missing')
    a = a[:pos] + insert + a[pos:]
arch_path.write_text(a)

# -----------------------------------------------------------------------------
# Progress doc.
# -----------------------------------------------------------------------------
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('  - [ ] Expose bounded Storage snapshot leases to rendering and retire them after jobs complete.\n',
              '  - [x] Expose bounded Storage snapshot leases to rendering and retire them after jobs complete.\n', 1)
d = d.replace('  - [x] Expose bounded Storage snapshot leases to rendering and retire them after jobs complete.\n',
'''  - [x] Expose bounded Storage snapshot leases to rendering and retire them after jobs complete.
  - [x] Read mixed exact-snapshot payloads directly from pinned COW Storage arrays instead of copying 8^3 payloads into renderer lists.
  - [ ] Move compact block-kind/ref snapshot traversal itself off the frame thread with versioned job-safe region metadata.
''', 1)
doc_path.write_text(d)

# Final guards.
cache = cache_path.read_text()
assert 'source.TryPinWorldBlock' in cache
assert 'ResizeUninitialized(nextLength)' not in cache
assert 'TryCopyWorldBlock(\n                    worldBlock' not in cache
assert 'PinnedReleasesPerDeadlineCheck' in cache
assert 'StepReleasePinnedSnapshotBlocks(deadline)' in cache
assert 'ReleasePinnedSnapshotBlocksImmediate();' in cache
assert 'NativeDisableContainerSafetyRestriction' in density_job_path.read_text()
assert 'NativeDisableContainerSafetyRestriction' in faceted_job_path.read_text()
assert 'TryPinWorldBlock' in read_source_path.read_text()
assert 'public bool TryPinWorldBlock' in source_path.read_text()
