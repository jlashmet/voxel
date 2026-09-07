using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSourceAdmissionFairnessTests
    {
        [UnityTest]
        public IEnumerator WaitingCoarseLodForcesActiveFineModeToDrainBeforeRefill()
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
            var fineA = default(GpuSurfaceExtractionContext);
            var fineB = default(GpuSurfaceExtractionContext);
            var fineRefill = default(GpuSurfaceExtractionContext);
            var coarse = default(GpuSurfaceExtractionContext);
            try
            {
                Runtime.VoxelRenderBridge.Source = () => world;
                Runtime.VoxelRenderBridge.Changes = storage.Changes;
                arenaShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                Assert.That(arenaShader, Is.Not.Null);
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 16);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);

                fineA = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                fineB = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                fineRefill = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                coarse = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                Assert.That(fineA, Is.Not.Null);
                Assert.That(fineB, Is.Not.Null);
                Assert.That(fineRefill, Is.Not.Null);
                Assert.That(coarse, Is.Not.Null);

                Begin(fineA, 1, 0, storage.Reads.Version);
                Begin(fineB, 1, 32, storage.Reads.Version);
                Begin(coarse, 4, 96, storage.Reads.Version);
                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(1));
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(2));
                Assert.That(GpuSurfaceSourceAdmission.WaitingCount, Is.EqualTo(1));

                fineA.Release();
                yield return null;
                Begin(fineRefill, 1, 64, storage.Reads.Version);
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(1),
                    "A new fine request must wait once another LOD has requested a turn.");
                Assert.That(GpuSurfaceSourceAdmission.WaitingCount, Is.EqualTo(2));
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1),
                    "Only the surviving admitted fine request may keep immutable source demand.");

                fineB.Release();
                yield return null;
                coarse.TryTakePagedBatch(out _, out _);
                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(4));
                Assert.That(GpuSurfaceSourceAdmission.ActiveCount, Is.EqualTo(1),
                    "The already-waiting coarse request must receive the handoff before fine refill.");
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.EqualTo(1));
            }
            finally
            {
                fineA?.Release();
                fineB?.Release();
                fineRefill?.Release();
                coarse?.Release();
                fineA?.Dispose();
                fineB?.Dispose();
                fineRefill?.Dispose();
                coarse?.Dispose();
                if (arena != null) GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                if (arenaShader != null) Object.DestroyImmediate(arenaShader);
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }
        }

        [UnityTest]
        public IEnumerator CancelledWaiterDoesNotHoldAFalsePreferredTurn()
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
            GpuSurfaceExtractionContext fine = null, coarseCancelled = null, nextFine = null;
            try
            {
                Runtime.VoxelRenderBridge.Source = () => world;
                Runtime.VoxelRenderBridge.Changes = storage.Changes;
                arenaShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
                arena = new GpuSurfacePageArena(arenaShader, 65536, 65536, 16);
                GpuSurfaceMirrorCoordinator.ConfigurePageArena(arena);
                fine = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                coarseCancelled = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                nextFine = GpuSurfaceExtractionContext.TryCreate(8, 2, 1024);
                Begin(fine, 4, 0, storage.Reads.Version);
                Begin(coarseCancelled, 8, 64, storage.Reads.Version);
                Assert.That(GpuSurfaceSourceAdmission.WaitingCount, Is.EqualTo(1));
                coarseCancelled.Release();
                Assert.That(coarseCancelled.HasActiveRequest, Is.False);
                fine.Release();
                yield return null;
                Begin(nextFine, 1, 96, storage.Reads.Version);
                Assert.That(GpuSurfaceSourceAdmission.ActiveStep, Is.EqualTo(1),
                    "A cancelled waiter must not reserve a phantom preferred-mode interval.");
                Assert.That(GpuSurfaceSourceAdmission.WaitingCount, Is.Zero);
            }
            finally
            {
                fine?.Release(); coarseCancelled?.Release(); nextFine?.Release();
                fine?.Dispose(); coarseCancelled?.Dispose(); nextFine?.Dispose();
                if (arena != null) GpuSurfaceMirrorCoordinator.DetachPageArena(arena, Time.frameCount);
                arena?.Dispose();
                if (arenaShader != null) Object.DestroyImmediate(arenaShader);
                Runtime.VoxelRenderBridge.Source = previousSource;
                Runtime.VoxelRenderBridge.Changes = previousChanges;
            }
        }

        private static void Begin(GpuSurfaceExtractionContext context, int step, int cacheX, ulong generation)
        {
            var request = new GpuChunkExtraction(
                new int3(cacheX * 8, 0, 0), new int3(cacheX, 0, 0), step, 0.1f);
            Assert.That(context.TryBeginStage(default, default, default, default, request, generation),
                Is.EqualTo(GpuStageOutcome.Staged));
        }
    }
}
