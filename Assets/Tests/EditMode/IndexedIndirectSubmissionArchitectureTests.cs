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

        private static string ReadRenderingResource(string relativePath) => File.ReadAllText(
            Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Rendering", "Resources", relativePath));

        private static string ReadSmoothShader() => File.ReadAllText(Path.Combine(
            RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", "Shaders",
            "SmoothSurface.shader"));

        [Test]
        public void IndexedIndirectSubmissionAppliesArenaOffsetsExactlyOnce()
        {
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            string shader = ReadSmoothShader();

            StringAssert.Contains("GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Index", arena,
                "The shared contiguous index payload must be a native index buffer while remaining compute-writable.");
            StringAssert.Contains("startIndex = metadata.IndexStart", renderPass,
                "The indirect command must start at the chunk's index range.");
            StringAssert.Contains("baseVertexIndex = metadata.VertexStart", renderPass,
                "Chunk-local index values must receive the arena vertex base exactly once in the indirect command.");
            StringAssert.Contains("Graphics.RenderPrimitivesIndexedIndirect", renderPass);
            StringAssert.Contains("InitIndirectDrawArgs(0)", shader,
                "Use Unity's indirect setup without requiring the Metal-incompatible SV_DrawID semantic.");
            StringAssert.Contains("GetIndirectVertexID_Base(vertexID)", shader,
                "The vertex buffer is one shared arena, so the shader must consume the base/absolute indexed vertex ID rather than rebase it relative to a command.");
            StringAssert.DoesNotContain("GetIndirectVertexID(vertexID)", shader,
                "The command-relative helper was used by the visually corrupt implementation with non-zero startIndex/baseVertexIndex.");
            StringAssert.DoesNotContain("SV_DrawID", shader,
                "SV_DrawID made Hidden/VoxelEngine/SmoothSurface unsupported on the Metal validation player.");
            StringAssert.DoesNotContain("_SurfaceIndices", shader,
                "Hardware indexing owns the index fetch in the optimized path; a second structured index lookup would apply addressing twice.");
        }

        [Test]
        public void IndexedArenaRemainsGpuWritableWhileServingAsIndexBuffer()
        {
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            string mesher = ReadRenderingResource("VoxelBrickMesher.compute");

            StringAssert.Contains("GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Index", arena,
                "The same allocation must remain both a native index buffer and a raw compute buffer.");
            StringAssert.Contains("RWByteAddressBuffer _Indices", mesher,
                "Production GPU extraction writes the shared index arena from compute.");
            StringAssert.DoesNotContain("GraphicsBuffer.UsageFlags.LockBufferForWrite", arena,
                "LockBufferForWrite makes a GraphicsBuffer GPU-read-only and is invalid for the RWByteAddressBuffer index arena.");
        }

        [Test]
        public void IndexedMultiCommandVertexPathUsesHardwareVertexIdWithoutCommandZeroFixup()
        {
            string shader = ReadSmoothShader();

            StringAssert.Contains("_SurfaceVertices[vertexID]", shader,
                "Hardware indexing already applies startIndex/baseVertex before SV_VertexID reaches this shader; the vertex arena lookup should consume that ID directly.");
            StringAssert.DoesNotContain("InitIndirectDrawArgs(0)", shader,
                "The surface shader does not consume command or instance args, so command-zero fixup must not participate in multi-command vertex addressing.");
            StringAssert.DoesNotContain("GetIndirectVertexID", shader,
                "Candidate 1 remained visually corrupt while routing indexed SV_VertexID through UnityIndirect helpers initialized from command zero.");
            StringAssert.DoesNotContain("SV_DrawID", shader,
                "SV_DrawID is not Metal-supported by this shader path.");
        }
    }
}
