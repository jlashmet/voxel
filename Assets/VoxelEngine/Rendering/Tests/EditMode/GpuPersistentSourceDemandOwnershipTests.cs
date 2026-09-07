using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuPersistentSourceDemandOwnershipTests
    {
        private static readonly BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;

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
                Assert.That(
                    GpuSurfaceMirrorCoordinator.PrepareFromBridge(storage.Reads.Version),
                    Is.True);

                var request = new GpuChunkExtraction(int3.zero, int3.zero, step, 0.1f);
                MethodInfo admit = typeof(GpuSurfaceExtractionContext).GetMethod(
                    "BeginPersistentStage", InstancePrivate);
                Assert.That(admit, Is.Not.Null);
                Assert.That(
                    (bool)admit.Invoke(context, new object[] { request, storage.Reads.Version }),
                    Is.True);

                bool watching = (bool)typeof(GpuSurfaceExtractionContext).GetField(
                    "_coverageRequested", InstancePrivate).GetValue(context);
                Assert.That(watching, Is.True,
                    "An admitted request must retain its whole-request edit/version watch.");
                Assert.That(GpuSurfaceMirrorCoordinator.DemandFootprintCount, Is.Zero,
                    "Whole-request admission must not pin source residency. GPU source preparation "
                  + "owns only bounded RequestSourceRange slices so overlapping requests can recycle "
                  + "the shared mirror while they wait.");
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
