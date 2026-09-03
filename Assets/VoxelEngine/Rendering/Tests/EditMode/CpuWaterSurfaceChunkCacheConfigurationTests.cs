using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class CpuWaterSurfaceChunkCacheConfigurationTests
    {
        [Test]
        public void ScheduledWaterMeshUsesAuthoritativeMaterialMask()
        {
            string source = File.ReadAllText(RenderingPath(
                "Runtime", "SurfaceExtraction", "CpuWaterSurfaceChunkCache.cs"));
            string stepBuild = MethodBody(source, "private bool StepBuild(",
                "private bool SnapshotWaterBrick(");

            StringAssert.Contains(
                "WaterMaterialMask = AuthoritativeWaterMaterialMask",
                stepBuild,
                "The water cache must pass its authoritative liquid classification to the Burst mesh job; "
              + "otherwise the job's default zero mask emits empty meshes for discovered water bricks.");
            StringAssert.Contains(
                "private const uint AuthoritativeWaterMaterialMask",
                source,
                "Water material policy belongs in the cache/composition boundary, not the reusable mesh job.");
        }

        private static string RenderingPath(params string[] parts)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate project root containing Assets/.");

            string path = Path.Combine(directory.FullName, "Assets", "VoxelEngine", "Rendering");
            foreach (string part in parts) path = Path.Combine(path, part);
            return path;
        }

        private static string MethodBody(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing source marker: {startMarker}");
            Assert.That(end, Is.GreaterThan(start),
                $"Missing source marker after {startMarker}: {endMarker}");
            return source.Substring(start, end - start);
        }
    }
}
