using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using GameCastlePlan = Game.Structures.Api.CastlePlan;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Builds the expensive procedural castle against a private voxel store on a worker thread,
    /// then publishes only the touched 8^3 logical blocks back to the live world in bounded
    /// batches. The live RegionTable/BrickPool are never read or written by the worker.
    ///
    /// The legacy ICastleBuildSession.IsComplete flag is also used by ShowcaseWorld's scheduler
    /// to decide whether it may immediately call Step again in the same frame. YieldRequested is
    /// therefore deliberately reported as complete between slices; Step() remains the authority
    /// for terminal completion. This keeps the old world scheduler to one small castle slice per
    /// player-loop iteration without making the live storage concurrent.
    /// </summary>
    internal sealed class AsyncCastleBuildSession : ICastleBuildSession
    {
        private const int BlocksPerPublishSlice = 32;
        private const int PrivateMixedBrickCapacity = 1 << 17;

        private readonly IRegionSnapshotSource _liveSnapshots;
        private readonly IRegionMutationStore _liveMutations;
        private readonly IMaterialAuthoringCatalogue _materials;
        private readonly GameCastlePlan _plan;
        private readonly uint _terrainSeed;
        private readonly int3[] _regions;
        private readonly RegionSemanticSnapshot[] _sourceSnapshots;
        private readonly IVoxelStorageRuntime _privateStorage;

        private int _captureCursor;
        private Task<BuildResult> _worker;
        private BuildResult _result;
        private int _publishCursor;
        private bool _yieldRequested;
        private bool _terminalComplete;

        public AsyncCastleBuildSession(
            IRegionReadSource liveReads,
            IRegionMutationStore liveMutations,
            IMaterialAuthoringCatalogue materials,
            in CastlePlan plan,
            uint terrainSeed)
        {
            _liveSnapshots = liveReads as IRegionSnapshotSource
                ?? throw new ArgumentException(
                    "Showcase castle authoring requires a snapshot-capable read source.",
                    nameof(liveReads));
            _liveMutations = liveMutations
                ?? throw new ArgumentNullException(nameof(liveMutations));
            _materials = materials;
            _plan = plan.Value;
            _terrainSeed = terrainSeed;
            _regions = CastleRegions(in plan);
            _sourceSnapshots = new RegionSemanticSnapshot[_regions.Length];

            // Allocate the isolated native store on the Unity/main thread. Worker execution below
            // only mutates this private lifetime; Unity objects and the live allocator never cross
            // the thread boundary.
            _privateStorage = VoxelEngineBootstrap.CreateStorage(
                math.max(16, _regions.Length * 2),
                PrivateMixedBrickCapacity,
                1024);
        }

        /// <summary>
        /// True either after terminal completion or while the scheduler must yield this frame.
        /// See class comment: terminal completion is the bool returned by Step().
        /// </summary>
        public bool IsComplete => _terminalComplete || _yieldRequested || IsWorkerRunning;

        public int StageNumber
        {
            get
            {
                if (_terminalComplete) return 9;
                if (_worker == null) return 1;
                if (!_worker.IsCompleted) return 2;
                return 8;
            }
        }

        public long TotalVoxelsWritten => _result?.TotalVoxelsWritten ?? 0L;

        private bool IsWorkerRunning => _worker != null && !_worker.IsCompleted;

        public bool Step()
        {
            if (_terminalComplete) return true;
            _yieldRequested = false;

            // Snapshot exactly one already-generated castle region per frame. This is the only
            // live read needed by the worker and keeps snapshot encoding from becoming a single
            // multi-region startup hitch.
            if (_captureCursor < _regions.Length)
            {
                int3 region = _regions[_captureCursor];
                RegionSnapshotCaptureResult captured = _liveSnapshots.CaptureSemanticSnapshot(
                    region,
                    RegionSemanticSnapshotLimits.DefaultMaxSnapshotBytes,
                    out RegionSemanticSnapshot snapshot);
                if (captured != RegionSnapshotCaptureResult.Ok)
                    throw new InvalidOperationException(
                        $"Could not snapshot castle region {region}: {captured}.");

                _sourceSnapshots[_captureCursor++] = snapshot;
                _yieldRequested = true;
                return false;
            }

            if (_worker == null)
            {
                _worker = Task.Run(BuildOnPrivateStore);
                _yieldRequested = true;
                return false;
            }

            if (!_worker.IsCompleted)
            {
                _yieldRequested = true;
                return false;
            }

            if (_result == null)
            {
                // Propagate worker exceptions on the main thread with their original stack.
                _result = _worker.GetAwaiter().GetResult();
                _privateStorage.Dispose();
            }

            int end = math.min(_result.Blocks.Count,
                               _publishCursor + BlocksPerPublishSlice);
            for (; _publishCursor < end; _publishCursor++)
                PublishBlock(_result.Blocks[_publishCursor]);

            if (_publishCursor < _result.Blocks.Count)
            {
                _yieldRequested = true;
                return false;
            }

            _terminalComplete = true;
            return true;
        }

        private BuildResult BuildOnPrivateStore()
        {
            for (int i = 0; i < _sourceSnapshots.Length; i++)
            {
                RegionSemanticSnapshot snapshot = _sourceSnapshots[i];
                if (!_privateStorage.SnapshotMutations.TryApplySemanticSnapshot(
                        snapshot.RegionCoord,
                        snapshot.Bytes,
                        snapshot.SemanticHash,
                        true))
                    throw new InvalidOperationException(
                        $"Private castle store rejected source snapshot {snapshot.RegionCoord}.");
            }

            var tracking = new TrackingMutationStore(_privateStorage.Mutations);
            IStructureAuthoringSession authoring =
                VoxelEngine.Composition.StructuresComposition.CreateAuthoringSession(
                    _privateStorage.Reads,
                    tracking,
                    _materials);
            var build = new CastleAuthoringBuild(authoring, in _plan, _terrainSeed);
            while (!build.Step()) { }

            var blocks = new List<BlockImage>(tracking.Touched.Count);
            foreach (KeyValuePair<int3, bool> touched in tracking.Touched)
                blocks.Add(CaptureBlock(_privateStorage.Reads, touched.Key, touched.Value));

            // Stable order keeps publication deterministic and makes frame-budget regressions
            // reproducible rather than dependent on Dictionary iteration order.
            blocks.Sort(static (a, b) =>
            {
                int c = a.WorldBlock.z.CompareTo(b.WorldBlock.z);
                if (c != 0) return c;
                c = a.WorldBlock.y.CompareTo(b.WorldBlock.y);
                return c != 0 ? c : a.WorldBlock.x.CompareTo(b.WorldBlock.x);
            });
            return new BuildResult(build.TotalVoxelsWritten, blocks);
        }

        private void PublishBlock(BlockImage image)
        {
            if (image.Kind == VoxelReadBlockKind.Empty)
            {
                _liveMutations.SetWholeBlock(
                    image.WorldBlock, VoxelGrid.MaterialEmpty, image.MarkHardSurface);
                return;
            }

            if (image.Kind == VoxelReadBlockKind.Uniform)
            {
                _liveMutations.SetWholeBlock(
                    image.WorldBlock, image.UniformMaterial, image.MarkHardSurface);
                return;
            }

            if (!_liveMutations.TryBeginCellBlock(
                    image.WorldBlock, image.MarkHardSurface, out VoxelBlockMutation mutation))
                throw new InvalidOperationException(
                    $"Could not publish castle block {image.WorldBlock}.");

            bool changed = mutation.MetadataChanged;
            for (int i = 0; i < VoxelReadGrid.VoxelsPerBlock; i++)
            {
                VoxelCell cell = image.Cells[i];
                changed |= mutation.SetCell(i, in cell);
            }
            _liveMutations.CompletePartialBlock(ref mutation, changed);
        }

        private static BlockImage CaptureBlock(
            IRegionReadSource reads, int3 worldBlock, bool markHardSurface)
        {
            if (!reads.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock block))
                throw new InvalidOperationException(
                    $"Private castle block {worldBlock} was not readable after authoring.");

            try
            {
                if (block.Kind == VoxelReadBlockKind.Empty)
                    return BlockImage.Empty(worldBlock, markHardSurface);
                if (block.Kind == VoxelReadBlockKind.Uniform)
                    return BlockImage.Uniform(
                        worldBlock, block.UniformMaterial, markHardSurface);

                var cells = new VoxelCell[VoxelReadGrid.VoxelsPerBlock];
                for (int i = 0; i < cells.Length; i++)
                {
                    int offset = block.MixedOffset + i;
                    byte material = block.MixedVoxels[offset];
                    cells[i] = new VoxelCell
                    {
                        BaseMaterialId = material,
                        Surface = material == VoxelGrid.MaterialEmpty
                            ? default
                            : VoxelSurfaceSemantics.FromStorage(
                                block.MixedSurfaceSemantics[offset]),
                        Boundary = new VoxelBoundarySample
                        {
                            Packed = block.MixedBoundarySamples[offset]
                        }
                    };
                }
                return BlockImage.Mixed(worldBlock, cells, markHardSurface);
            }
            finally
            {
                if (block.HasPinnedPayload)
                    reads.ReleasePinnedWorldBlock(in block.Pin);
            }
        }

        private static int3[] CastleRegions(in CastlePlan plan)
        {
            int cx = plan.Centre.x;
            int cz = plan.Centre.z;
            int reach = math.max(plan.PlateauRadius + plan.CliffDrop + 8,
                                 VoxelGrid.RegionVoxelEdge);
            int shift = VoxelDimensions.RegionVoxelEdgeLog2;
            int minRx = (cx - reach) >> shift;
            int maxRx = (cx + reach) >> shift;
            int minRz = (cz - reach) >> shift;
            int maxRz = (cz + reach) >> shift;
            var regions = new int3[(maxRx - minRx + 1) * (maxRz - minRz + 1)];
            int cursor = 0;
            for (int rz = minRz; rz <= maxRz; rz++)
            for (int rx = minRx; rx <= maxRx; rx++)
                regions[cursor++] = new int3(rx, 0, rz);
            return regions;
        }

        private sealed class TrackingMutationStore : IRegionMutationStore
        {
            private readonly IRegionMutationStore _inner;
            public readonly Dictionary<int3, bool> Touched = new();

            public TrackingMutationStore(IRegionMutationStore inner) =>
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));

            public bool IsRegionResident(int3 regionCoord) =>
                _inner.IsRegionResident(regionCoord);

            public bool SetWholeBlock(int3 worldBlock, byte material, bool markHardSurface)
            {
                bool changed = _inner.SetWholeBlock(worldBlock, material, markHardSurface);
                if (changed) Touch(worldBlock, markHardSurface);
                return changed;
            }

            public bool SetWholeCellBlock(
                int3 worldBlock, in VoxelCell cell, bool markHardSurface)
            {
                bool changed = _inner.SetWholeCellBlock(
                    worldBlock, in cell, markHardSurface);
                if (changed) Touch(worldBlock, markHardSurface);
                return changed;
            }

            public bool TryBeginPartialBlock(
                int3 worldBlock, byte targetMaterial, bool markHardSurface,
                out VoxelBlockMutation mutation)
            {
                bool ok = _inner.TryBeginPartialBlock(
                    worldBlock, targetMaterial, markHardSurface, out mutation);
                if (ok) Touch(worldBlock, markHardSurface);
                return ok;
            }

            public bool TryBeginCellBlock(
                int3 worldBlock, bool markHardSurface, out VoxelBlockMutation mutation)
            {
                bool ok = _inner.TryBeginCellBlock(
                    worldBlock, markHardSurface, out mutation);
                if (ok) Touch(worldBlock, markHardSurface);
                return ok;
            }

            public bool CompletePartialBlock(
                ref VoxelBlockMutation mutation, bool payloadChanged) =>
                _inner.CompletePartialBlock(ref mutation, payloadChanged);

            private void Touch(int3 worldBlock, bool markHardSurface)
            {
                if (Touched.TryGetValue(worldBlock, out bool previous))
                    Touched[worldBlock] = previous || markHardSurface;
                else
                    Touched.Add(worldBlock, markHardSurface);
            }
        }

        private sealed class BuildResult
        {
            public readonly long TotalVoxelsWritten;
            public readonly List<BlockImage> Blocks;

            public BuildResult(long totalVoxelsWritten, List<BlockImage> blocks)
            {
                TotalVoxelsWritten = totalVoxelsWritten;
                Blocks = blocks;
            }
        }

        private sealed class BlockImage
        {
            public readonly int3 WorldBlock;
            public readonly VoxelReadBlockKind Kind;
            public readonly byte UniformMaterial;
            public readonly VoxelCell[] Cells;
            public readonly bool MarkHardSurface;

            private BlockImage(
                int3 worldBlock, VoxelReadBlockKind kind, byte uniformMaterial,
                VoxelCell[] cells, bool markHardSurface)
            {
                WorldBlock = worldBlock;
                Kind = kind;
                UniformMaterial = uniformMaterial;
                Cells = cells;
                MarkHardSurface = markHardSurface;
            }

            public static BlockImage Empty(int3 block, bool hard) =>
                new(block, VoxelReadBlockKind.Empty, 0, null, hard);
            public static BlockImage Uniform(int3 block, byte material, bool hard) =>
                new(block, VoxelReadBlockKind.Uniform, material, null, hard);
            public static BlockImage Mixed(int3 block, VoxelCell[] cells, bool hard) =>
                new(block, VoxelReadBlockKind.Mixed, 0, cells, hard);
        }
    }
}
