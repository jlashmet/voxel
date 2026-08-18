using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using GameCastlePlan = Game.Structures.Api.CastlePlan;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Builds the expensive procedural castle against a private voxel store on a worker thread,
    /// then publishes only the touched 8^3 logical blocks back to the live world in bounded
    /// batches. The live RegionTable/BrickPool are never read or written by the worker.
    ///
    /// Castle-owned regions are deterministic terrain until landmark publication. Reconstructing
    /// that terrain from the authoritative terrain sampler on the worker avoids serialising nine
    /// complete 512^3 semantic region snapshots on the player-loop thread. That snapshot path was
    /// measured at more than 75 ms for a single castle slice on the Metal validation machine.
    ///
    /// ShowcaseWorld's legacy castle loop tests IsComplete after every Step and would otherwise
    /// poll a waiting worker until the entire frame budget expires. IsComplete therefore also
    /// reports true after this session has already been stepped in the current Unity frame. On
    /// the next frame it becomes false again automatically until terminal completion. That keeps
    /// the existing scheduler to one bounded castle slice per player-loop iteration without
    /// conflating worker completion with live-world completion across frames.
    /// </summary>
    internal sealed class AsyncCastleBuildSession : ICastleBuildSession, IDisposable
    {
        private const int MaxBlocksPerPublishSlice = 32;
        private const double PublishBudgetMs = 0.35;
        private const int MinimumPrivateMixedBrickCapacity = 8 * 1024;
        private const int PrivateMixedBrickSafetyReserve = 4 * 1024;
        private const int PrivateCapacityGrowthFloor = 8 * 1024;
        private const int MaxPrivateBuildAttempts = 5;
        private const int AuthoredMixedBrickFragmentationMultiplier = 5;
        private const int TerrainDeepDepth = 40;

        private readonly IRegionMutationStore _liveMutations;
        private readonly IMaterialAuthoringCatalogue _materials;
        private readonly GameCastlePlan _plan;
        private readonly uint _terrainSeed;
        private readonly int3[] _regions;

        private Task<BuildResult> _worker;
        private BuildResult _result;
        private int _publishCursor;
        private int _lastStepFrame = -1;
        private bool _terminalComplete;
        private volatile bool _cancelRequested;
        private bool _disposed;

        public AsyncCastleBuildSession(
            IRegionReadSource liveReads,
            IRegionMutationStore liveMutations,
            IMaterialAuthoringCatalogue materials,
            in CastlePlan plan,
            uint terrainSeed)
        {
            if (liveReads == null) throw new ArgumentNullException(nameof(liveReads));
            _liveMutations = liveMutations
                ?? throw new ArgumentNullException(nameof(liveMutations));
            _materials = materials;
            _plan = plan.Value;
            _terrainSeed = terrainSeed;
            _regions = CastleRegions(in plan);

            // No private BrickPool and no multi-megabyte semantic snapshot are allocated here.
            // The first Step only queues worker work; private storage sizing, terrain generation,
            // castle authoring and result staging all happen off the player-loop thread.
        }

        /// <summary>
        /// Terminally true after publication, and transiently true after this session already
        /// consumed its one allowed slice in the current frame so ShowcaseWorld cannot busy-poll.
        /// </summary>
        public bool IsComplete => _terminalComplete || _lastStepFrame == Time.frameCount;

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

        public bool Step()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncCastleBuildSession));
            if (_terminalComplete) return true;
            _lastStepFrame = Time.frameCount;

            if (_worker == null)
            {
                _worker = Task.Run(BuildOnPrivateStore);
                return false;
            }

            if (!_worker.IsCompleted)
                return false;

            if (_result == null)
            {
                // Propagate worker exceptions on the main thread with their original stack.
                _result = _worker.GetAwaiter().GetResult();
                if (_result == null)
                    throw new OperationCanceledException("Castle build was cancelled before publication.");
            }

            // Publication is a frame-path mutation producer: every copied block can invalidate
            // several surface chunks and enqueue replacement geometry. A fixed 32-block burst
            // could consume multiple milliseconds on its own and then leave the renderer chasing
            // a large dirty wave. Keep the old hard cap as a backstop, but stop on a tight wall-
            // clock budget as well. Always publish at least one block so progress cannot stall on
            // machines where the first mutation itself crosses the deadline.
            double publishDeadline = Time.realtimeSinceStartupAsDouble + PublishBudgetMs * 0.001;
            int published = 0;
            while (_publishCursor < _result.Blocks.Count
                   && published < MaxBlocksPerPublishSlice)
            {
                PublishBlock(_result.Blocks[_publishCursor]);
                _publishCursor++;
                published++;
                if (Time.realtimeSinceStartupAsDouble >= publishDeadline)
                    break;
            }

            if (_publishCursor < _result.Blocks.Count)
                return false;

            // Native staging is needed only until the final live block is copied. Retain the small
            // BuildResult shell so TotalVoxelsWritten remains available to ShowcaseWorld when this
            // terminal Step returns true.
            _result.DisposePayloads();
            _terminalComplete = true;
            return true;
        }

        private BuildResult BuildOnPrivateStore()
        {
            if (_cancelRequested) return null;

            int terrainMixedBricks = CountTerrainMixedBricks();
            int capacity = EstimatePrivateMixedBrickCapacity(in _plan, terrainMixedBricks);
            Exception lastCapacityFailure = null;

            // BrickPool is deliberately fixed-capacity, but this isolated build can recover from
            // an underestimate without penalising the player-loop or permanently reserving the old
            // 131k-slot pool. A failed private attempt is disposed before a 25% (minimum 8k) retry,
            // so only one pool exists at a time. The initial terrain term is exact; the authored
            // term is intentionally conservative because thin styled walls fragment writes across
            // far more mixed blocks than a simple voxels/512 estimate implies.
            for (int attempt = 0; attempt < MaxPrivateBuildAttempts; attempt++)
            {
                if (_cancelRequested) return null;
                try
                {
                    return BuildOnPrivateStoreAttempt(capacity);
                }
                catch (Exception ex) when (IsBrickPoolExhaustion(ex))
                {
                    lastCapacityFailure = ex;
                    if (attempt + 1 >= MaxPrivateBuildAttempts) break;
                    int growth = math.max(PrivateCapacityGrowthFloor, capacity >> 2);
                    capacity = checked(capacity + growth);
                }
            }

            throw new InvalidOperationException(
                $"Castle private storage still exhausted after {MaxPrivateBuildAttempts} "
              + $"plan-sized attempts; final capacity={capacity}, terrainMixed={terrainMixedBricks}.",
                lastCapacityFailure);
        }

        private BuildResult BuildOnPrivateStoreAttempt(int privateMixedCapacity)
        {
            IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                math.max(16, _regions.Length * 2),
                privateMixedCapacity,
                1024);

            try
            {
                PopulateDeterministicTerrain(storage);
                if (_cancelRequested) return null;

                var tracking = new TrackingMutationStore(storage.Mutations);
                IStructureAuthoringSession authoring =
                    VoxelEngine.Composition.StructuresComposition.CreateAuthoringSession(
                        storage.Reads,
                        tracking,
                        _materials);
                var build = new CastleAuthoringBuild(authoring, in _plan, _terrainSeed);
                while (!build.Step())
                {
                    if (_cancelRequested) return null;
                }
                if (_cancelRequested) return null;

                // First classify the touched blocks. The result holds only compact descriptors;
                // mixed payloads are copied into three contiguous native staging arrays below.
                // No VoxelCell[512] objects enter the managed heap.
                var blocks = new List<BlockImage>(tracking.Touched.Count);
                int mixedCount = 0;
                foreach (KeyValuePair<int3, bool> touched in tracking.Touched)
                {
                    if (_cancelRequested) return null;
                    if (!storage.Reads.TryPinWorldBlock(touched.Key, out PinnedVoxelReadBlock block))
                        throw new InvalidOperationException(
                            $"Private castle block {touched.Key} was not readable after authoring.");

                    try
                    {
                        int payloadOffset = block.Kind == VoxelReadBlockKind.Mixed
                            ? mixedCount++ * VoxelReadGrid.VoxelsPerBlock
                            : -1;
                        blocks.Add(new BlockImage(
                            touched.Key,
                            block.Kind,
                            block.UniformMaterial,
                            payloadOffset,
                            touched.Value));
                    }
                    finally
                    {
                        if (block.HasPinnedPayload)
                            storage.Reads.ReleasePinnedWorldBlock(in block.Pin);
                    }
                }

                // Publish bottom-up so an in-progress landmark reads as a coherent structure
                // growing from its foundation instead of disconnected north/south slices. Within
                // each level, work from the castle centre outward. This remains fully deterministic
                // while making bounded publication visually much less broken.
                int centreBlockX = _plan.Centre.x >> VoxelReadGrid.BlockEdgeLog2;
                int centreBlockZ = _plan.Centre.z >> VoxelReadGrid.BlockEdgeLog2;
                blocks.Sort((a, b) =>
                {
                    int c = a.WorldBlock.y.CompareTo(b.WorldBlock.y);
                    if (c != 0) return c;
                    int adx = a.WorldBlock.x - centreBlockX;
                    int adz = a.WorldBlock.z - centreBlockZ;
                    int bdx = b.WorldBlock.x - centreBlockX;
                    int bdz = b.WorldBlock.z - centreBlockZ;
                    long aDistance = (long)adx * adx + (long)adz * adz;
                    long bDistance = (long)bdx * bdx + (long)bdz * bdz;
                    c = aDistance.CompareTo(bDistance);
                    if (c != 0) return c;
                    c = a.WorldBlock.z.CompareTo(b.WorldBlock.z);
                    return c != 0 ? c : a.WorldBlock.x.CompareTo(b.WorldBlock.x);
                });

                int payloadVoxels = checked(mixedCount * VoxelReadGrid.VoxelsPerBlock);
                NativeArray<byte> mixedMaterials = payloadVoxels > 0
                    ? new NativeArray<byte>(payloadVoxels, Allocator.Persistent,
                                            NativeArrayOptions.UninitializedMemory)
                    : default;
                NativeArray<ushort> mixedSurfaceSemantics = payloadVoxels > 0
                    ? new NativeArray<ushort>(payloadVoxels, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory)
                    : default;
                NativeArray<byte> mixedBoundarySamples = payloadVoxels > 0
                    ? new NativeArray<byte>(payloadVoxels, Allocator.Persistent,
                                            NativeArrayOptions.UninitializedMemory)
                    : default;

                try
                {
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        if (_cancelRequested)
                        {
                            DisposeStaging(
                                ref mixedMaterials,
                                ref mixedSurfaceSemantics,
                                ref mixedBoundarySamples);
                            return null;
                        }

                        BlockImage image = blocks[i];
                        if (image.Kind != VoxelReadBlockKind.Mixed) continue;
                        if (!storage.Reads.TryPinWorldBlock(
                                image.WorldBlock, out PinnedVoxelReadBlock block))
                            throw new InvalidOperationException(
                                $"Private castle block {image.WorldBlock} disappeared during staging.");

                        try
                        {
                            if (block.Kind != VoxelReadBlockKind.Mixed || !block.HasPinnedPayload)
                                throw new InvalidOperationException(
                                    $"Private castle block {image.WorldBlock} changed kind during staging.");

                            NativeArray<byte>.Copy(
                                block.MixedVoxels,
                                block.MixedOffset,
                                mixedMaterials,
                                image.PayloadOffset,
                                VoxelReadGrid.VoxelsPerBlock);
                            NativeArray<ushort>.Copy(
                                block.MixedSurfaceSemantics,
                                block.MixedOffset,
                                mixedSurfaceSemantics,
                                image.PayloadOffset,
                                VoxelReadGrid.VoxelsPerBlock);
                            NativeArray<byte>.Copy(
                                block.MixedBoundarySamples,
                                block.MixedOffset,
                                mixedBoundarySamples,
                                image.PayloadOffset,
                                VoxelReadGrid.VoxelsPerBlock);
                        }
                        finally
                        {
                            if (block.HasPinnedPayload)
                                storage.Reads.ReleasePinnedWorldBlock(in block.Pin);
                        }
                    }

                    return new BuildResult(
                        build.TotalVoxelsWritten,
                        blocks,
                        mixedMaterials,
                        mixedSurfaceSemantics,
                        mixedBoundarySamples);
                }
                catch
                {
                    DisposeStaging(
                        ref mixedMaterials,
                        ref mixedSurfaceSemantics,
                        ref mixedBoundarySamples);
                    throw;
                }
            }
            finally
            {
                storage.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancelRequested = true;

            Task<BuildResult> worker = _worker;
            if (worker != null)
            {
                // World teardown is a lifecycle boundary rather than the player-loop frame path.
                // Join so the worker cannot outlive the live material catalogue it borrowed. The
                // worker's finally block owns private native storage on every exit path.
                try
                {
                    BuildResult pending = worker.GetAwaiter().GetResult();
                    pending?.DisposePayloads();
                }
                catch
                {
                    // A cancelled world's result is intentionally discarded. Runtime failures are
                    // surfaced by Step while the world is active; teardown must still release the
                    // live world even if the background task happened to fault concurrently.
                }
                _worker = null;
            }

            _result?.DisposePayloads();
            _result = null;
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

            bool copied = mutation.CopyStoragePayload(
                _result.MixedMaterials,
                _result.MixedSurfaceSemantics,
                _result.MixedBoundarySamples,
                image.PayloadOffset);
            if (!copied)
            {
                _liveMutations.CompletePartialBlock(ref mutation, false);
                throw new InvalidOperationException(
                    $"Could not bulk-publish castle block {image.WorldBlock}.");
            }

            _liveMutations.CompletePartialBlock(ref mutation, true);
        }

        private int CountTerrainMixedBricks()
        {
            long total = 0;
            var heights = new int[VoxelReadGrid.BlockEdge * VoxelReadGrid.BlockEdge];

            for (int r = 0; r < _regions.Length; r++)
            {
                if (_cancelRequested) return 0;
                int3 originVoxel = _regions[r] * VoxelGrid.RegionVoxelEdge;
                for (int bz = 0; bz < VoxelReadGrid.BlocksPerRegionEdge; bz++)
                for (int bx = 0; bx < VoxelReadGrid.BlocksPerRegionEdge; bx++)
                {
                    SampleTerrainBlockHeights(
                        originVoxel, bx, bz, heights, out int minHeight, out int maxHeight);
                    for (int by = 0; by < VoxelReadGrid.BlocksPerRegionEdge; by++)
                    {
                        int blockBaseY = originVoxel.y + by * VoxelReadGrid.BlockEdge;
                        if (blockBaseY > maxHeight) break;
                        int blockTopY = blockBaseY + VoxelReadGrid.BlockEdge - 1;
                        if (blockTopY <= minHeight) continue;
                        total++;
                    }
                }
            }

            if (total > int.MaxValue)
                throw new InvalidOperationException(
                    $"Castle terrain requires too many mixed bricks: {total}.");
            return (int)total;
        }

        private void PopulateDeterministicTerrain(IVoxelStorageRuntime storage)
        {
            var heights = new int[VoxelReadGrid.BlockEdge * VoxelReadGrid.BlockEdge];
            var blockMaterials = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            var blockSurfaces = new NativeArray<ushort>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            var blockBoundaries = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Persistent,
                NativeArrayOptions.ClearMemory);

            try
            {
                for (int r = 0; r < _regions.Length; r++)
                {
                    if (_cancelRequested) return;
                    int3 regionCoord = _regions[r];
                    int3 originVoxel = regionCoord * VoxelGrid.RegionVoxelEdge;
                    int3 originBlock = regionCoord * VoxelReadGrid.BlocksPerRegionEdge;
                    RegionGenerationWriteView generation = storage.Generation.AcquireRegion(regionCoord);

                    for (int bz = 0; bz < VoxelReadGrid.BlocksPerRegionEdge; bz++)
                    for (int bx = 0; bx < VoxelReadGrid.BlocksPerRegionEdge; bx++)
                    {
                        SampleTerrainBlockHeights(
                            originVoxel, bx, bz, heights, out int minHeight, out int maxHeight);

                        for (int by = 0; by < VoxelReadGrid.BlocksPerRegionEdge; by++)
                        {
                            if (_cancelRequested) return;
                            int blockBaseY = originVoxel.y + by * VoxelReadGrid.BlockEdge;
                            if (blockBaseY > maxHeight) break; // remaining blocks are already empty

                            int blockTopY = blockBaseY + VoxelReadGrid.BlockEdge - 1;
                            if (blockTopY <= minHeight)
                            {
                                byte uniform = blockTopY < minHeight - TerrainDeepDepth
                                    ? ShowcaseWorld.MatBedrock
                                    : ShowcaseWorld.MatStone;
                                generation.SetUniformBlock(bx, by, bz, uniform);
                                continue;
                            }

                            FillTerrainMixedPayload(blockBaseY, heights, blockMaterials);
                            int3 worldBlock = originBlock + new int3(bx, by, bz);
                            if (!storage.Mutations.TryBeginCellBlock(
                                    worldBlock, false, out VoxelBlockMutation mutation))
                                throw new InvalidOperationException(
                                    $"Could not create private terrain block {worldBlock}.");

                            bool copied = mutation.CopyStoragePayload(
                                blockMaterials, blockSurfaces, blockBoundaries, 0);
                            if (!copied)
                            {
                                storage.Mutations.CompletePartialBlock(ref mutation, false);
                                throw new InvalidOperationException(
                                    $"Could not populate private terrain block {worldBlock}.");
                            }
                            storage.Mutations.CompletePartialBlock(ref mutation, true);
                        }
                    }
                }
            }
            finally
            {
                blockMaterials.Dispose();
                blockSurfaces.Dispose();
                blockBoundaries.Dispose();
            }
        }

        private void SampleTerrainBlockHeights(
            int3 originVoxel,
            int blockX,
            int blockZ,
            int[] heights,
            out int minHeight,
            out int maxHeight)
        {
            minHeight = int.MaxValue;
            maxHeight = int.MinValue;
            int baseX = originVoxel.x + blockX * VoxelReadGrid.BlockEdge;
            int baseZ = originVoxel.z + blockZ * VoxelReadGrid.BlockEdge;

            for (int vz = 0; vz < VoxelReadGrid.BlockEdge; vz++)
            for (int vx = 0; vx < VoxelReadGrid.BlockEdge; vx++)
            {
                int height = TerrainSampler.HeightAt(baseX + vx, baseZ + vz, _terrainSeed);
                heights[vx + vz * VoxelReadGrid.BlockEdge] = height;
                if (height < minHeight) minHeight = height;
                if (height > maxHeight) maxHeight = height;
            }
        }

        private static void FillTerrainMixedPayload(
            int blockBaseY,
            int[] heights,
            NativeArray<byte> materials)
        {
            int edge = VoxelReadGrid.BlockEdge;
            int edgeSquared = edge * edge;
            for (int vz = 0; vz < edge; vz++)
            for (int vy = 0; vy < edge; vy++)
            for (int vx = 0; vx < edge; vx++)
            {
                int surface = heights[vx + vz * edge];
                int worldY = blockBaseY + vy;
                int voxel = vx + vy * edge + vz * edgeSquared;
                materials[voxel] = TerrainMaterialAt(worldY, surface);
            }
        }

        private static byte TerrainMaterialAt(int y, int surface)
        {
            if (y > surface) return VoxelGrid.MaterialEmpty;
            if (y == surface) return ShowcaseWorld.SurfaceMaterialAt(surface);
            if (y > surface - TerrainDeepDepth) return ShowcaseWorld.MatStone;
            return ShowcaseWorld.MatBedrock;
        }

        internal static int EstimatePrivateMixedBrickCapacity(
            in GameCastlePlan plan, int terrainMixedBrickCount)
        {
            // Terrain mixed demand is counted exactly from the deterministic height field before
            // allocating the pool. CastlePlanner.EstimateWrites is a content-policy work estimate,
            // not a mixed-brick bound: thin walls, stairs and authored semantic surfaces can keep
            // mostly-uniform blocks mixed. Reserve five block-equivalents per 512 estimated writes;
            // a bounded worker-only retry remains the backstop for unusually fragmented plans.
            long authoredBlocks =
                (CastlePlanner.EstimateWrites(in plan) + VoxelReadGrid.VoxelsPerBlock - 1)
                / VoxelReadGrid.VoxelsPerBlock;
            long estimate = (long)terrainMixedBrickCount
                          + authoredBlocks * AuthoredMixedBrickFragmentationMultiplier
                          + PrivateMixedBrickSafetyReserve;
            if (estimate < MinimumPrivateMixedBrickCapacity)
                estimate = MinimumPrivateMixedBrickCapacity;
            if (estimate > int.MaxValue)
                throw new InvalidOperationException(
                    $"Castle plan requires an unsupported private mixed-brick capacity: {estimate}.");
            return (int)estimate;
        }

        private static bool IsBrickPoolExhaustion(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is InvalidOperationException
                    && current.Message != null
                    && current.Message.StartsWith(
                        "BrickPool exhausted at capacity ", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void DisposeStaging(
            ref NativeArray<byte> materials,
            ref NativeArray<ushort> surfaceSemantics,
            ref NativeArray<byte> boundarySamples)
        {
            if (materials.IsCreated) materials.Dispose();
            if (surfaceSemantics.IsCreated) surfaceSemantics.Dispose();
            if (boundarySamples.IsCreated) boundarySamples.Dispose();
            materials = default;
            surfaceSemantics = default;
            boundarySamples = default;
        }

        private static int3[] CastleRegions(in CastlePlan plan)
        {
            int cx = plan.Centre.x;
            int cz = plan.Centre.z;
            int reach = math.max(plan.PlateauRadius + plan.CliffDrop + 8,
                                 VoxelGrid.RegionVoxelEdge);
            int shift = VoxelGrid.RegionVoxelEdgeLog2;
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
            public NativeArray<byte> MixedMaterials;
            public NativeArray<ushort> MixedSurfaceSemantics;
            public NativeArray<byte> MixedBoundarySamples;
            private bool _payloadsDisposed;

            public BuildResult(
                long totalVoxelsWritten,
                List<BlockImage> blocks,
                NativeArray<byte> mixedMaterials,
                NativeArray<ushort> mixedSurfaceSemantics,
                NativeArray<byte> mixedBoundarySamples)
            {
                TotalVoxelsWritten = totalVoxelsWritten;
                Blocks = blocks;
                MixedMaterials = mixedMaterials;
                MixedSurfaceSemantics = mixedSurfaceSemantics;
                MixedBoundarySamples = mixedBoundarySamples;
            }

            public void DisposePayloads()
            {
                if (_payloadsDisposed) return;
                _payloadsDisposed = true;
                if (MixedMaterials.IsCreated) MixedMaterials.Dispose();
                if (MixedSurfaceSemantics.IsCreated) MixedSurfaceSemantics.Dispose();
                if (MixedBoundarySamples.IsCreated) MixedBoundarySamples.Dispose();
                MixedMaterials = default;
                MixedSurfaceSemantics = default;
                MixedBoundarySamples = default;
            }
        }

        private readonly struct BlockImage
        {
            public readonly int3 WorldBlock;
            public readonly VoxelReadBlockKind Kind;
            public readonly byte UniformMaterial;
            public readonly int PayloadOffset;
            public readonly bool MarkHardSurface;

            public BlockImage(
                int3 worldBlock,
                VoxelReadBlockKind kind,
                byte uniformMaterial,
                int payloadOffset,
                bool markHardSurface)
            {
                WorldBlock = worldBlock;
                Kind = kind;
                UniformMaterial = uniformMaterial;
                PayloadOffset = payloadOffset;
                MarkHardSurface = markHardSurface;
            }
        }
    }
}