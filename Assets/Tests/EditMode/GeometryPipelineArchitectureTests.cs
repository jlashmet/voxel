using System;
using System.IO;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

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

        [Test]
        public void MultipleCamerasCannotMultiplyGeometryFrameBudgets()
        {
            string scheduler = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));
            int guard = scheduler.IndexOf("if (_lastAdvancedFrame == frame)",
                                          StringComparison.Ordinal);
            int advancement = scheduler.IndexOf("_lastAdvancedFrame = frame;", guard,
                                                StringComparison.Ordinal);
            Assert.GreaterOrEqual(guard, 0);
            Assert.Greater(advancement, guard);
            string sameFramePath = scheduler.Substring(guard, advancement - guard);
            StringAssert.Contains("CollectVisibility(camera, voxelSize, frame);", sameFramePath);
            StringAssert.Contains("return;", sameFramePath);

            int visibilityStart = scheduler.IndexOf("private void CollectVisibility",
                                                    StringComparison.Ordinal);
            int visibilityEnd = scheduler.IndexOf("private void EnqueueSurfaceDiscovery",
                                                  visibilityStart, StringComparison.Ordinal);
            Assert.GreaterOrEqual(visibilityStart, 0);
            Assert.Greater(visibilityEnd, visibilityStart);
            string visibility = scheduler.Substring(visibilityStart,
                                                    visibilityEnd - visibilityStart);
            StringAssert.DoesNotContain("ReadSince(", visibility);
            StringAssert.DoesNotContain("ProcessSurfaceDiscovery(", visibility);
            StringAssert.DoesNotContain("TryPublishPending(", visibility);
            StringAssert.DoesNotContain(".Prepare(storage,", visibility);
        }

        [Test]
        public void SolidBuildOutputStaysNativeThroughArenaUpload()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            StringAssert.Contains("private NativeList<SmoothSurfaceVertex> _vertices;", cache);
            StringAssert.Contains("private NativeList<uint> _indices;", cache);
            StringAssert.DoesNotContain("private readonly List<SmoothSurfaceVertex> _vertices", cache);
            StringAssert.DoesNotContain("private readonly List<uint> _indices", cache);
            StringAssert.Contains("NativeArray<SmoothSurfaceVertex> source", arena);
            StringAssert.Contains("NativeArray<uint> source", arena);
        }


        [Test]
        public void CoarseExactSamplingUsesFewerBuildWorkspaces()
        {
            Assert.AreEqual(8, VoxelSurfaceScheduler.WorkerCountForSourceStep(1));
            Assert.AreEqual(8, VoxelSurfaceScheduler.WorkerCountForSourceStep(2));
            Assert.AreEqual(4, VoxelSurfaceScheduler.WorkerCountForSourceStep(4));
            Assert.AreEqual(2, VoxelSurfaceScheduler.WorkerCountForSourceStep(8));
            Assert.Less(VoxelSurfaceScheduler.WorkerCountForSourceStep(8),
                        VoxelSurfaceScheduler.WorkerCountForSourceStep(1),
                "The exact step-8 ring must not duplicate its 66^3 snapshot cache eight times.");
        }

    }
}