using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuProductionBrickCacheArchitectureTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;
                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        private static string ReadRenderingFile(params string[] path)
        {
            string fullPath = Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Rendering");
            foreach (string part in path) fullPath = Path.Combine(fullPath, part);
            return File.ReadAllText(fullPath);
        }

        [Test]
        public void ProductionBatchReusesGpuPreparedDenseCacheWithoutCpuReconstruction()
        {
            string source = ReadRenderingFile("Runtime", "GpuVoxel", "GpuSurfaceExtractor.cs");
            string preparation = ReadRenderingFile(
                "Runtime", "GpuVoxel", "GpuBrickCachePreparation.cs");
            string preparationBuffers = ReadRenderingFile(
                "Runtime", "GpuVoxel", "GpuBrickCachePreparationBuffers.cs");
            string density = ReadRenderingFile("Resources", "VoxelBrickDensity.hlsl");
            string mesher = ReadRenderingFile("Resources", "VoxelBrickMesher.compute");
            string countBatch = MethodBody(source, "internal void DispatchCountBatch",
                                           "internal void DispatchBaseWriteBatch");
            string bindBatch = MethodBody(source, "private void BindBatchShared",
                                          "private void RecordChunkUniforms");

            StringAssert.Contains("internal readonly GpuBrickCachePreparation PreparedCache;", source,
                "Each shared batch lane must own one reusable GPU preparation resource.");
            StringAssert.Contains("resources.PreparedCache.Dispatch(mirror, requests, recordCount);",
                                  countBatch,
                "The production batch must resolve persistent mirror entries entirely on the GPU.");
            StringAssert.DoesNotContain("_brickCache.SetData(_brickCacheStaging)", countBatch,
                "Production batching must not reconstruct or upload a CPU dense brick cache.");
            StringAssert.DoesNotContain(".GetData(", countBatch,
                "Production batching must not synchronously read GPU mirror/cache data back.");
            StringAssert.DoesNotContain("new GpuBrickCachePreparation", countBatch,
                "Preparation resources must be reused by the lane rather than allocated per dispatch.");
            StringAssert.Contains("resources.PreparedCache.DenseEntries", bindBatch,
                "Count and write kernels must consume the same GPU-prepared dense slices.");
            StringAssert.Contains("resources.PreparedCache.RequestViews", bindBatch,
                "Batch kernels must receive the resolver's per-record dense-cache views.");
            StringAssert.Contains("OutputBase = -1", preparation,
                "Reusable request buffers must terminate active views and invalidate stale entries.");
            StringAssert.Contains("new ComputeBuffer(capacity + 1", preparationBuffers,
                "Prepared request views must reserve a terminator even at full logical capacity.");
            StringAssert.Contains("request.w < 0", density,
                "Metal-compatible dense lookup must stop at the reserved prepared-view terminator.");
            StringAssert.DoesNotContain("_BatchBrickCacheViews.GetDimensions", density,
                "Metal does not support StructuredBuffer size queries; the prepared-view terminator "
              + "must bound lookup instead.");
            StringAssert.DoesNotContain("PREPARED_BATCH_LOOKUP_MAGIC", density,
                "Prepared lookup must be compile-time kernel policy, not a runtime cache marker.");
            StringAssert.Contains(
                "#pragma kernel CSBatchSampleDensity VOXEL_BATCH_DENSE_LOOKUP", mesher,
                "Production density sampling must compile against prepared dense-cache semantics.");
            StringAssert.Contains(
                "#pragma kernel CSBatchWriteDecorations VOXEL_BATCH_DENSE_LOOKUP", mesher,
                "Every production batch surface category must compile against the same dense view.");
        }

        [Test]
        public void ProductionGpuFramePathYieldsInsteadOfSynchronizing()
        {
            string extractor = ReadRenderingFile("Runtime", "GpuVoxel", "GpuSurfaceExtractor.cs");
            string context = ReadRenderingFile(
                "Runtime", "GpuVoxel", "GpuSurfaceExtractionContext.cs");
            string coordinator = ReadRenderingFile(
                "Runtime", "GpuVoxel", "GpuSurfaceMirrorCoordinator.cs");
            string pageArena = ReadRenderingFile(
                "Runtime", "GpuVoxel", "GpuSurfacePageArena.cs");
            string cache = ReadRenderingFile(
                "Runtime", "SurfaceExtraction", "CpuTransvoxelChunkCache.cs");

            string countBatch = MethodBody(extractor, "internal void DispatchCountBatch",
                                           "internal void DispatchBaseWriteBatch");
            string writeBatch = MethodBody(extractor, "internal void DispatchBaseWriteBatch",
                                           "internal void PrefixCountBatch");
            string copyPublish = MethodBody(extractor, "public void WriteScratchCopyAndPublish",
                                            "public void PublishEmpty");
            string gpuPhase = MethodBody(cache, "if (_build.Phase == 9)",
                                         "if (_build.Phase == 11)");

            AssertNoBlockingGpuSync(countBatch, "production count batch");
            AssertNoBlockingGpuSync(writeBatch, "production write batch");
            AssertNoBlockingGpuSync(copyPublish, "production copy/publication");
            AssertNoBlockingGpuSync(context, "production extraction context");
            AssertNoBlockingGpuSync(coordinator, "production mirror coordinator");
            AssertNoBlockingGpuSync(pageArena, "production page arena");
            AssertNoBlockingGpuSync(gpuPhase, "solid worker GPU phase");

            StringAssert.Contains("_gpuExtraction.TryTakePagedBatch", gpuPhase,
                "The worker must poll the GPU stage instead of synchronizing it.");
            StringAssert.Contains("break;", gpuPhase,
                "An unfinished GPU stage must yield the current frame.");
            StringAssert.DoesNotContain("GpuCpuSnapshotRequired", gpuPhase,
                "Pending GPU work must not become a CPU snapshot merely because it is unfinished.");
            StringAssert.Contains("!s_ExtractionFence.passed", coordinator,
                "The coordinator must inspect fence readiness without waiting on the fence.");
        }

        private static void AssertNoBlockingGpuSync(string source, string path)
        {
            StringAssert.DoesNotContain(".GetData(", source,
                $"{path} must not synchronously read GPU buffers.");
            StringAssert.DoesNotContain("WaitForCompletion(", source,
                $"{path} must not synchronously wait for GPU readback.");
            StringAssert.DoesNotContain("WaitOnAsyncGraphicsFence(", source,
                $"{path} must not wait for a graphics fence on the frame path.");
        }

        private static string MethodBody(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"Missing source marker: {startMarker}");
            Assert.Greater(end, start, $"Missing source marker after {startMarker}: {endMarker}");
            return source.Substring(start, end - start);
        }
    }
}
