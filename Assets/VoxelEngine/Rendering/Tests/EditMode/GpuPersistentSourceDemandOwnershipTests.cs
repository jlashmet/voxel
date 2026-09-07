using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuPersistentSourceDemandOwnershipTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        public void AdmittedRequestWatchesEditsWithoutRetainingWholeSourceFootprint(int step)
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            using var storage = VoxelEngineBootstrap.CreateStorage(1, 1);
            storage.Residency.EnsureRegionResident(int3.zero);
            storage.PublishAllResidentRegions();

            var previousSource = Runtime.VoxelRenderBridge.Source;
            var previousChanges = Runtime.VoxelRenderBridge.Changes;
            var world = new Runtime.VoxelWorldView
            {
                Storage = storage.Reads,
                SurfaceCatalogueView = VoxelEngine.Storage.Runtime.SurfaceCatalogue.CreateBuiltIns(),
                CoatingCatalogueView = VoxelEngine.Storage.Runtime.CoatingCatalogue.CreateBuiltIns(),
            };

            GpuSurfaceExtractionContext context = null;
            GpuSurfacePageArena arena = null;
            ComputeShader arenaShader = null;
            try
            {
                Runtime.VoxelRenderBridge.Source = () => world;
                Runtime.VoxelRenderBridge.Changes = storage.Changes;
                context = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                Assert.That(context, Is.Not.Null);
                arenaShader = UnityEngine.Object.Instantiate(
                    Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                Assert.That(arenaShader, Is.Not.Null);
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 8);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);

                var request = new GpuChunkExtraction(int3.zero, int3.zero, step, 0.1f);
                Assert.That(
                    context.TryBeginStage(default, default, default, default,
                                          request, storage.Reads.Version),
                    Is.EqualTo(GpuStageOutcome.Staged));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero,
                    "Whole-request admission must not pin source residency. GPU source preparation "
                  + "owns only bounded RequestSourceRange slices so overlapping requests can recycle "
                  + "the shared mirror while they wait.");

                uint beforeEdit = GpuSurfaceMirrorCoordinator.CoverageEpochFor(
                    request.BrickCacheOrigin, context.BrickCacheEdge);
                Assert.That(storage.Mutations.SetWholeBlock(int3.zero, 1, false), Is.True);
                storage.PublishAllResidentRegions();
                GpuSurfaceMirrorCoordinator.PrepareFrame(
                    storage.Reads, storage.Changes, Time.frameCount + 1, 1.0);
                Assert.That(
                    GpuSurfaceMirrorCoordinator.CoverageEpochFor(
                        request.BrickCacheOrigin, context.BrickCacheEdge),
                    Is.Not.EqualTo(beforeEdit),
                    "The whole-request edit/version watch must still invalidate an admitted build.");
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero,
                    "Processing the edit must not promote the whole request into a residency lease.");
            }
            finally
            {
                context?.Release();
                if (arena != null)
                    GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                context?.Dispose();
                if (arenaShader != null)
                    UnityEngine.Object.DestroyImmediate(arenaShader);
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }
        }
    }
}
