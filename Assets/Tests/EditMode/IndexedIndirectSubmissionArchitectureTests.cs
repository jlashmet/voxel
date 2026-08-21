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

        [Test]
        public void IndexedIndirectSubmissionStoresArenaGlobalIndicesWithZeroBaseVertex()
        {
            string renderPass = ReadRenderingSource(
                Path.Combine("RenderFeature", "VoxelRenderPass.cs"));
            string arena = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "SurfaceGeometryArena.cs"));
            string mesher = ReadRenderingResource("VoxelBrickMesher.compute");
            string shader = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Rendering", "Runtime", "Shaders",
                "SmoothSurface.shader"));

            StringAssert.Contains("GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Index", arena,
                "The shared contiguous index payload must be a native index buffer while remaining compute-writable.");
            StringAssert.Contains("startIndex = metadata.IndexStart", renderPass,
                "The indirect command must start at the chunk's index range.");
            StringAssert.Contains("baseVertexIndex = 0u", renderPass,
                "Procedural SV_VertexID on Unity 6000.5 Metal does not expose baseVertexIndex, so the command base must stay zero.");
            StringAssert.Contains("uint vertexBase = (uint)lease.VertexStart", arena,
                "CPU-authored local indices must be rebased to arena-global vertex ids before upload.");
            StringAssert.Contains("source[sourceStart + i] += vertexBase", arena);
            StringAssert.Contains("VertexSlot(vertexBase + i2)", mesher,
                "GPU planar indices must store the same arena-global vertex ids used by vertex writes.");
            StringAssert.Contains("VertexSlot(vertexBase + _RegularCellIndices", mesher,
                "GPU shared-vertex indices must store arena-global vertex ids.");
            StringAssert.Contains("VertexSlot(vertexBase + a)", mesher,
                "GPU transition indices must store arena-global vertex ids.");
            StringAssert.Contains("InitIndirectDrawArgs(0)", shader,
                "Use Unity's current indirect setup without requiring the Metal-incompatible SV_DrawID semantic.");
            StringAssert.Contains("GetIndirectVertexID(vertexID)", shader,
                "Stored indices are already arena-global, so the indexed vertex id is consumed directly.");
            StringAssert.DoesNotContain("GetIndirectVertexID_Base", shader,
                "The _Base helper adds startIndex and was proven to corrupt the indexed arena representation.");
            StringAssert.DoesNotContain("SV_DrawID", shader,
                "SV_DrawID made Hidden/VoxelEngine/SmoothSurface unsupported on the Metal validation player.");
            StringAssert.DoesNotContain("_SurfaceIndices", shader,
                "Hardware indexing owns the index fetch in the optimized path; a second structured index lookup would address twice.");
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
    }
}
