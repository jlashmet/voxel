using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuPersistentSourceDemandOwnershipTests
    {
        [TestCase(1, 2, 2)]
        [TestCase(2, 2, 2)]
        [TestCase(4, 1, 1)]
        [TestCase(8, 1, 0)]
        public void SourceAdmissionBoundsImmutableOwnersAndWaitingDemand(
            int step, int expectedOwners, int expectedDemandFootprints)
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

            GpuSurfacePageArena arena = null;
            ComputeShader arenaShader = null;
            var contexts = new GpuSurfaceExtractionContext[3];
            try
            {
                Runtime.VoxelRenderBridge.Source = () => world;
                Runtime.VoxelRenderBridge.Changes = storage.Changes;
                arenaShader = UnityEngine.Object.Instantiate(
                    Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                Assert.That(arenaShader, Is.Not.Null);
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 16);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);

                for (int i = 0; i < contexts.Length; i++)
                {
                    contexts[i] = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                    Assert.That(contexts[i], Is.Not.Null);
                    int3 chunkOrigin = new(i * 256 * step, 0, 0);
                    int3 cacheOrigin = new(i * 32, 0, 0);
                    var request = new GpuChunkExtraction(
                        chunkOrigin, cacheOrigin, step, 0.1f);
                    Assert.That(
                        contexts[i].TryBeginStage(default, default, default, default,
                                                  request, storage.Reads.Version),
                        Is.EqualTo(GpuStageOutcome.Staged));
                }

                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(step));
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(expectedOwners),
                    "Only the bounded number of immutable requests may own source admission.");
                Assert.That(GpuSurfaceMirrorCoordinator.ActiveExtractions, Is.EqualTo(expectedOwners),
                    "A request waiting on source admission must not consume a GPU extraction chain.");
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount,
                    Is.EqualTo(expectedDemandFootprints),
                    "Waiting requests must not add whole-source demand. Exact steps retain only "
                  + "their bounded immutable owners; step 8 owns copied HLOD slices instead.");

                contexts[0].Release();
                contexts[2].TryTakePagedBatch(out _, out _);
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(expectedOwners),
                    "Releasing an owner must let the already-waiting request acquire the freed slot.");
                Assert.That(GpuSurfaceMirrorCoordinator.ActiveExtractions, Is.EqualTo(expectedOwners));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount,
                    Is.EqualTo(expectedDemandFootprints));
            }
            finally
            {
                for (int i = 0; i < contexts.Length; i++) contexts[i]?.Release();
                for (int i = 0; i < contexts.Length; i++) contexts[i]?.Dispose();
                if (arena != null)
                    GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                if (arenaShader != null)
                    UnityEngine.Object.DestroyImmediate(arenaShader);
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }

            Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.Zero);
            Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero);
            Assert.That(GpuSurfaceMirrorCoordinator.ActiveExtractions, Is.Zero);
        }

        [Test]
        public void WaitingDifferentLodTakesAdmissionAfterCurrentLodReleases()
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

            GpuSurfaceExtractionContext step4 = null, step1 = null;
            GpuSurfacePageArena arena = null;
            ComputeShader arenaShader = null;
            try
            {
                Runtime.VoxelRenderBridge.Source = () => world;
                Runtime.VoxelRenderBridge.Changes = storage.Changes;
                step4 = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                step1 = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                Assert.That(step4, Is.Not.Null);
                Assert.That(step1, Is.Not.Null);
                arenaShader = UnityEngine.Object.Instantiate(
                    Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                Assert.That(arenaShader, Is.Not.Null);
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 16);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);

                var coarseExact = new GpuChunkExtraction(int3.zero, int3.zero, 4, 0.1f);
                var fine = new GpuChunkExtraction(new int3(256, 0, 0), new int3(40, 0, 0), 1, 0.1f);
                step4.TryBeginStage(default, default, default, default,
                                    coarseExact, storage.Reads.Version);
                step1.TryBeginStage(default, default, default, default,
                                    fine, storage.Reads.Version);

                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(4));
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(1));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1));

                step4.Release();
                step1.TryTakePagedBatch(out _, out _);
                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(1),
                    "A waiting different LOD must receive the bounded admission turn after release.");
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(1));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1));
            }
            finally
            {
                step4?.Release();
                step1?.Release();
                step4?.Dispose();
                step1?.Dispose();
                if (arena != null)
                    GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                if (arenaShader != null)
                    UnityEngine.Object.DestroyImmediate(arenaShader);
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }
        }
    }
}
