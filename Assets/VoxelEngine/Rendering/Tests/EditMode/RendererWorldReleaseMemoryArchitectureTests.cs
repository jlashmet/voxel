using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RendererWorldReleaseMemoryArchitectureTests
    {
        [Test]
        public void WorldReleaseDoesNotEagerlyAllocateReplacementRendererArena()
        {
            string source = File.ReadAllText(
                "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs");

            StringAssert.DoesNotContain(
                "private VoxelSurfaceScheduler _scheduler = new();", source,
                "the persistent render feature must not allocate an arena before a world renders");
            StringAssert.Contains(
                "_scheduler ??= new VoxelSurfaceScheduler();", source,
                "renderer state should be created only when a valid world actually renders");

            int release = source.IndexOf(
                "private void ReleaseWorldResources()", StringComparison.Ordinal);
            int nextMethod = source.IndexOf(
                "public void Dispose()", release, StringComparison.Ordinal);
            Assert.GreaterOrEqual(release, 0);
            Assert.Greater(nextMethod, release);

            string releaseBody = source.Substring(release, nextMethod - release);
            StringAssert.Contains("_scheduler.Dispose();", releaseBody,
                "world teardown must synchronously drain jobs and Storage pins");
            StringAssert.Contains("_scheduler = null;", releaseBody,
                "world teardown must end with no live renderer scheduler/arena");
            StringAssert.DoesNotContain("new VoxelSurfaceScheduler()", releaseBody,
                "teardown must not overlap old Metal resource retirement with a replacement arena");
        }
    }
}
