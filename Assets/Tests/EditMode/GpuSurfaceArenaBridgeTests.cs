using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuSurfaceArenaBridgeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/Shaders/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        private ComputeShader _shader;

        [SetUp]
        public void SetUp()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the GPU cutover path cannot run.");

            _shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(_shader, $"Compute shader missing at {ShaderPath}");

            if (!_shader.HasKernel("CSSampleDensity"))
            {
                string compilerMessages = string.Join("\n",
                    ShaderUtil.GetComputeShaderMessages(_shader).Select(message => message.message));
                Assert.Fail(
                    $"Compute shader loaded but CSSampleDensity is unavailable. "
                  + $"graphicsDevice={SystemInfo.graphicsDeviceType}, graphicsDeviceName='{SystemInfo.graphicsDeviceName}', "
                  + $"supportsCompute={SystemInfo.supportsComputeShaders}. Compiler messages:\n{compilerMessages}");
            }
        }

        [Test]
        public void ComputeMesherStagesDirectlyIntoTheProductionSurfaceArena()
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            using var arena = new SurfaceGeometryArena(32768, 65536, 8);

            Configure(extractor);
            FillHalfSolidCache(extractor);

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1),
                                                 sourceStep: 2, voxelSize: 0.1f,
                                                 transitionFaceMask: 0b111111);

            GpuSurfaceArenaBuild build = GpuSurfaceArenaBridge.Build(
                extractor, mirror, tables, request, arena);

            Assert.AreEqual(GpuSurfaceArenaBuildStatus.Ready, build.Status);
            Assert.IsTrue(build.Lease.IsValid);
            Assert.Greater(build.VertexCount, 0);
            Assert.Greater(build.IndexCount, 0);

            var args = new uint[SurfaceGeometryArena.ArgsWordsPerDraw];
            arena.Args.GetData(args, 0, build.Lease.ArgsWordStart, args.Length);
            Assert.AreEqual((uint)build.IndexCount, args[0],
                "The draw record must be published only for the complete GPU payload.");
            Assert.AreEqual(1u, args[1]);
            Assert.AreEqual(0u, args[2]);
            Assert.AreEqual(0u, args[3]);

            Assert.AreEqual(0UL, extractor.GeometryReadbacks,
                "Production cutover must not pull vertices, indices, or sampled fields back to CPU memory.");
            Assert.AreEqual(2UL, extractor.CounterReadbacks,
                "The bridge permits only the fixed count/write bookkeeping readbacks.");

            arena.Release(build.Lease);
        }

        [Test]
        public void ArenaPressureLeavesPreviouslyPublishedLeaseUntouched()
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            // One aligned vertex range and one aligned index range: the old representation owns all
            // payload capacity, so a replacement must fail without reclaiming it first.
            using var arena = new SurfaceGeometryArena(256, 512, 2);

            Configure(extractor);
            FillHalfSolidCache(extractor);

            Assert.IsTrue(arena.TryAcquire(1, 1, out SurfaceGeometryLease oldLease));
            int usedVertices = arena.UsedVertices;
            int usedIndices = arena.UsedIndices;
            int usedArgs = arena.UsedArgsRecords;

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1),
                                                 sourceStep: 2, voxelSize: 0.1f,
                                                 transitionFaceMask: 0b111111);

            GpuSurfaceArenaBuild build = GpuSurfaceArenaBridge.Build(
                extractor, mirror, tables, request, arena);

            Assert.AreEqual(GpuSurfaceArenaBuildStatus.ArenaFull, build.Status);
            Assert.IsFalse(build.Lease.IsValid);
            Assert.AreEqual(usedVertices, arena.UsedVertices);
            Assert.AreEqual(usedIndices, arena.UsedIndices);
            Assert.AreEqual(usedArgs, arena.UsedArgsRecords,
                "A failed replacement must not release or overwrite the published draw lease.");

            arena.Release(oldLease);
        }

        private static void Configure(GpuSurfaceExtractor extractor)
        {
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < defaultStyles.Length; i++)
                defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, defaultStyles);
        }

        private static void FillHalfSolidCache(GpuSurfaceExtractor extractor)
        {
            extractor.ClearBrickCache();
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
            {
                bool solid = y < 2;
                extractor.SetBrickCacheEntry(new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? (byte)1 : (byte)0, -1));
            }
        }
    }
}
