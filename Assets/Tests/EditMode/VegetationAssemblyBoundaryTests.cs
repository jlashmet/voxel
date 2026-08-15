using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VegetationAssemblyBoundaryTests
    {
        [Test]
        public void RenderingAndWorldGenUseVegetationApiOnly()
        {
            string root = FindRepoRoot();
            AssertApiOnly(Path.Combine(root, "Assets", "VoxelEngine", "Rendering", "VoxelEngine.Rendering.asmdef"));
            AssertApiOnly(Path.Combine(root, "Packages", "com.mountingforce.worldgen", "Runtime", "Voxel", "MountingForce.WorldGen.Voxel.asmdef"));

            string rendering = Path.Combine(root, "Assets", "VoxelEngine", "Rendering");
            foreach (string path in Directory.EnumerateFiles(rendering, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                Assert.That(source.Contains("VoxelEngine.Vegetation.Runtime"), Is.False, Path.GetFileName(path));
                Assert.That(source.Contains("VoxelEngine.Core.Vegetation"), Is.False, Path.GetFileName(path));
            }
        }

        private static void AssertApiOnly(string asmdefPath)
        {
            string text = File.ReadAllText(asmdefPath);
            Assert.That(text.Contains("\"VoxelEngine.Vegetation.Api\""), Is.True, asmdefPath);
            Assert.That(text.Contains("\"VoxelEngine.Vegetation.Runtime\""), Is.False, asmdefPath);
            Assert.That(text.Contains("\"VoxelEngine.Vegetation\""), Is.False, asmdefPath);
        }

        private static string FindRepoRoot()
        {
            string directory = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(directory))
            {
                if (Directory.Exists(Path.Combine(directory, "Assets"))) return directory;
                directory = Directory.GetParent(directory)?.FullName;
            }
            throw new InvalidOperationException("Could not locate repository root.");
        }
    }
}
