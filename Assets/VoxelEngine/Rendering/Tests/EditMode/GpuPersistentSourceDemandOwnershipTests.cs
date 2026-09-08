using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuPersistentSourceDemandOwnershipTests
    {
        [TestCase(1, 10, 8, 8)]
        [TestCase(2, 18, 6, 6)]
        [TestCase(4, 34, 1, 1)]
        [TestCase(8, 66, 1, 0)]
        public void SourceAdmissionBoundsImmutableOwnersAndWaitingDemand(
            int step, int cacheEdge, int expectedOwners, int expectedDemandFootprints)
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
            var contexts = new GpuSurfaceExtractionContext[expectedOwners + 1];
            try
            {
                Runtime.VoxelRenderBridge.Source = () => world;
                Runtime.VoxelRenderBridge.Changes = storage.Changes;
                arenaShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                Assert.That(arenaShader, Is.Not.Null);
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 32);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);

                for (int i = 0; i < contexts.Length; i++)
                {
                    contexts[i] = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024, cacheEdge);
                    Assert.That(contexts[i], Is.Not.Null);
                    int3 cacheOrigin = new(i * (cacheEdge + 4), 0, 0);
                    int3 chunkOrigin = cacheOrigin * 8;
                    var request = new GpuChunkExtraction(
                        chunkOrigin, cacheOrigin, step, 0.1f);
                    Assert.That(
                        contexts[i].TryBeginStage(default, default, default, default,
                                                  request, storage.Reads.Version),
                        Is.EqualTo(GpuStageOutcome.Staged));
                }

                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(step));
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(expectedOwners),
                    "Immutable source owners must be bounded by mirror capacity and chain count.");
                Assert.That(GpuSurfaceMirrorCoordinator.ActiveExtractions, Is.EqualTo(expectedOwners),
                    "A request waiting on source admission must not consume a GPU extraction chain.");
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount,
                    Is.EqualTo(expectedDemandFootprints),
                    "Waiting requests must not add whole-source demand. Exact steps retain only "
                  + "their capacity-bounded immutable owners; step 8 owns copied HLOD slices instead.");
                int expectedReservation = step == 8
                    ? 0 : expectedOwners * cacheEdge * cacheEdge * cacheEdge;
                Assert.That(GpuSurfaceSourceAdmission.ReservedMixedSlots,
                    Is.EqualTo(expectedReservation));
                Assert.That(GpuSurfaceSourceAdmission.ReservedMixedSlots,
                    Is.LessThanOrEqualTo(contexts[0].Mirror.SlotCapacity));
                Assert.That(GpuSurfaceSourceAdmission.WaitingCount, Is.EqualTo(1));
            }
            finally
            {
                for (int i = 0; i < contexts.Length; i++) contexts[i]?.Release();
                for (int i = 0; i < contexts.Length; i++) contexts[i]?.Dispose();
                if (arena != null)
                    GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                if (arenaShader != null) Object.DestroyImmediate(arenaShader);
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }

            Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.Zero);
            Assert.That(GpuSurfaceSourceAdmission.ReservedMixedSlots, Is.Zero);
            Assert.That(GpuSurfaceSourceAdmission.WaitingCount, Is.Zero);
            Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero);
            Assert.That(GpuSurfaceMirrorCoordinator.ActiveExtractions, Is.Zero);
        }

        [Test]
        public void ReleaseDropsCoordinatorCoverageWithoutAdmissionOwnership()
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
            try
            {
                Runtime.VoxelRenderBridge.Source = () => world;
                Runtime.VoxelRenderBridge.Changes = storage.Changes;
                context = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024, 10);
                Assert.That(context, Is.Not.Null);
                GpuSurfaceMirrorCoordinator.PrepareFrame(
                    storage.Reads, storage.Changes, Time.frameCount, 1.0);

                var request = new GpuChunkExtraction(int3.zero, int3.zero, 1, 0.1f);
                int extent = GpuSolidChunkCache.CellsPerAxis * request.SourceStep;
                ulong coverageWorldEpoch = GpuSurfaceMirrorCoordinator.RequestCoverage(
                    request.BrickCacheOrigin, 10, request.ChunkOriginVoxel,
                    request.ChunkOriginVoxel + new int3(extent));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1));
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.Zero,
                    "This regression starts from coordinator-owned coverage, not admission ownership.");

                GpuSurfaceSourceAdmission.Release(context, request, 10, coverageWorldEpoch);

                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero,
                    "Releasing context coverage must not leak demand when admission did not create it.");
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.Zero);
                Assert.That(GpuSurfaceSourceAdmission.ReservedMixedSlots, Is.Zero);
            }
            finally
            {
                context?.Dispose();
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }
        }

        [UnityTest]
        public IEnumerator WaitingDifferentLodTakesAdmissionAfterCurrentLodReleases()
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
                step4 = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024, 34);
                step1 = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024, 10);
                Assert.That(step4, Is.Not.Null);
                Assert.That(step1, Is.Not.Null);
                arenaShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                Assert.That(arenaShader, Is.Not.Null);
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 16);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);

                var coarseExact = new GpuChunkExtraction(int3.zero, int3.zero, 4, 0.1f);
                var fine = new GpuChunkExtraction(new int3(320, 0, 0), new int3(40, 0, 0), 1, 0.1f);
                step4.TryBeginStage(default, default, default, default,
                                    coarseExact, storage.Reads.Version);
                step1.TryBeginStage(default, default, default, default,
                                    fine, storage.Reads.Version);

                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(4));
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(1));
                Assert.That(GpuSurfaceSourceAdmission.ReservedMixedSlots, Is.EqualTo(34 * 34 * 34));
                Assert.That(GpuSurfaceSourceAdmission.WaitingCount, Is.EqualTo(1));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1));

                step4.Release();
                yield return null;
                step1.TryTakePagedBatch(out _, out _);
                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(1),
                    "A waiting different LOD must receive the bounded admission turn after release.");
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(1));
                Assert.That(GpuSurfaceSourceAdmission.ReservedMixedSlots, Is.EqualTo(10 * 10 * 10));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1));
            }
            finally
            {
                step4?.Release(); step1?.Release();
                step4?.Dispose(); step1?.Dispose();
                if (arena != null) GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                if (arenaShader != null) Object.DestroyImmediate(arenaShader);
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }
        }
    }
}
