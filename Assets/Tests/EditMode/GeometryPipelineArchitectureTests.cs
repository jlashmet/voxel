using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GeometryPipelineArchitectureTests
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

        private static string ReadRenderingSource(string relativePath) => File.ReadAllText(
            Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", relativePath));

        [Test]
        public void TransitionMeshingIsScheduledAndNeverRunInline()
        {
            string source = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            int start = source.IndexOf("private bool StepTransitionFaces", StringComparison.Ordinal);
            int end = source.IndexOf("private void InitialiseTopologyTables", start,
                                     StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);
            string transition = source.Substring(start, end - start);
            StringAssert.Contains("_transitionJobHandle = job.Schedule();", transition);
            StringAssert.Contains("if (!_transitionJobHandle.IsCompleted) return false;", transition);
            StringAssert.DoesNotContain(".Run();", transition);
        }

        [Test]
        public void SolidPublicationIsQueuedAndGloballyBudgeted()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("public bool TryPublishPending", cache);
            StringAssert.Contains("entry.AdvanceUpload(_vertices, _indices, byteBudget", cache);
            StringAssert.DoesNotContain("entry.Upload(_vertices, _indices)", cache);
            StringAssert.Contains("SolidUploadBudgetBytes", scheduler);
            StringAssert.Contains("_lastFrameSolidUploadedBytes += uploadedBytes", scheduler);
        }

        [Test]
        public void KnownFramePathJobCompletionsAreReadinessGated()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            StringAssert.Contains("if (!_transitionJobHandle.IsCompleted) return false;", cache);
            StringAssert.Contains("&& !ScheduledJobsComplete())", cache);
            int discovery = scheduler.IndexOf(
                "if (!_surfaceDiscoveryJobHandle.IsCompleted)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(discovery, 0);
            StringAssert.Contains("return;", scheduler.Substring(discovery, 140));
        }
    }
}