using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class IndexedIndirectSubmissionArchitectureTests
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
        public void IndexedIndirectSubmissionAppliesArenaOffsetsExactlyOnce()
        {
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            string shader = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", "Shaders",
                "SmoothSurface.shader"));

            StringAssert.Contains("GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Index", arena,
                "The shared contiguous index payload must be a native index buffer while remaining compute-writable.");
            StringAssert.Contains("startIndex = metadata.IndexStart", renderPass,
                "The indirect command must start at the chunk's index range.");
            StringAssert.Contains("baseVertexIndex = metadata.VertexStart", renderPass,
                "Chunk-local index values must receive the arena vertex base exactly once in the indirect command.");
            StringAssert.Contains("Graphics.RenderPrimitivesIndexedIndirect", renderPass);
            StringAssert.Contains("InitIndirectDrawArgs(0)", shader,
                "Use Unity's supported multi-command indirect shader contract.");
            StringAssert.Contains("GetIndirectVertexID(vertexID)", shader);
            StringAssert.DoesNotContain("SV_DrawID", shader,
                "SV_DrawID made Hidden/VoxelEngine/SmoothSurface unsupported on the Metal validation player.");
            StringAssert.DoesNotContain("_SurfaceIndices", shader,
                "Hardware indexing owns the index fetch in the optimized path; a second structured index lookup would apply addressing twice.");
        }
    }
}
