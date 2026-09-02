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

        private static string ReadExtractor() => File.ReadAllText(Path.Combine(
            RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", "GpuVoxel",
            "GpuSurfaceExtractor.cs"));

        [Test]
        public void ProductionBatchReusesGpuPreparedDenseCacheWithoutCpuReconstruction()
        {
            string source = ReadExtractor();
            string countBatch = MethodBody(source, "internal void DispatchCountBatch",
                                           "internal void DispatchBaseWriteBatch");
            string bindBatch = MethodBody(source, "private void BindBatchShared",
                                          "private void BindShared");

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
