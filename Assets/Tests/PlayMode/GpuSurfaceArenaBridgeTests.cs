using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class GpuSurfaceArenaBridgeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/Shaders/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;
        private const int ProductionBrickCacheEdge = 4;

        private ComputeShader _shader;

        [SetUp]
        public void SetUp()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "This test must run with a real graphics device; -nographics cannot prove the GPU cutover path.");
            Assert.IsTrue(SystemInfo.supportsComputeShaders,
                $"Graphics device {SystemInfo.graphicsDeviceName} does not support compute shaders.");

            _shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(_shader, $"Compute shader missing at {ShaderPath}");

            if (!_shader.HasKernel("CSSampleDensity"))
            {
                string compilerMessages = string.Join("\n",
                    ShaderUtil.GetComputeShaderMessages(_shader).Select(message => message.message));
                Assert.Fail(
                    $"Compute shader loaded but CSSampleDensity is unavailable. "
                  + $"graphicsDevice={SystemInfo.graphicsDeviceType}, graphicsDeviceName='{SystemInfo.graphicsDeviceName}'. "
                  + $"Compiler messages:\n{compilerMessages}");
            }
        }

        [Test]
        public void ProductionFactoryUsesCpuSnapshotBrickCacheEdge()
        {
            const int deliberatelyNonDefaultEdge = 5;
            using GpuSurfaceExtractionContext context = GpuSurfaceExtractionContext.TryCreate(
                CellsPerAxis, Padding,
                mirrorBudgetBytes: GpuBrickBufferLayout.BytesPerMixedBrick * 8L,
                brickCacheEdge: deliberatelyNonDefaultEdge,
                shader: _shader);

            Assert.NotNull(context);
            Assert.AreEqual(deliberatelyNonDefaultEdge, context.BrickCacheEdge,
                "Production must index the dense brick snapshot with the CPU builder's exact edge, "
              + "not a separately-derived GPU stride that merely covers the same world extent.");
        }

        [Test]
        public void CpuSnapshotStagesThroughMirrorDirectlyIntoTheProductionSurfaceArena()
        {
            using GpuSurfaceExtractionContext context = CreateContext();
            using var arena = new SurfaceGeometryArena(32768, 65536, 8);
            using NativeArray<TransvoxelDensityBrick> bricks = CreateHalfSolidSnapshot(context.BrickCacheEdge);

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1),
                                                 sourceStep: 2, voxelSize: 0.1f,
                                                 transitionFaceMask: 0b111111);

            GpuStageOutcome staged = context.TryStage(
                bricks, default, default, default, request, generation: 1);
            Assert.AreEqual(GpuStageOutcome.Staged, staged,
                "The production context must accept the same dense brick snapshot the CPU builder gathers.");

            GpuSurfaceArenaBuild build = GpuSurfaceArenaBridge.Build(context, arena);

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

            Assert.AreEqual(0UL, context.Extractor.GeometryReadbacks,
                "Production cutover must not pull vertices, indices, or sampled fields back to CPU memory.");
            Assert.AreEqual(2UL, context.Extractor.CounterReadbacks,
                "The context permits only the fixed count/write bookkeeping readbacks.");
            Assert.AreEqual(1UL, context.ChunksStaged);
            Assert.AreEqual(1UL, context.ChunksWritten);

            arena.Release(build.Lease);
        }

        [Test]
        public void ArenaPressureLeavesPreviouslyPublishedLeaseUntouched()
        {
            using GpuSurfaceExtractionContext context = CreateContext();
            // One aligned vertex range and one aligned index range: the old representation owns all
            // payload capacity, so a replacement must fail without reclaiming it first.
            using var arena = new SurfaceGeometryArena(256, 512, 2);
            using NativeArray<TransvoxelDensityBrick> bricks = CreateHalfSolidSnapshot(context.BrickCacheEdge);

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1),
                                                 sourceStep: 2, voxelSize: 0.1f,
                                                 transitionFaceMask: 0b111111);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));

            Assert.IsTrue(arena.TryAcquire(1, 1, out SurfaceGeometryLease oldLease));
            int usedVertices = arena.UsedVertices;
            int usedIndices = arena.UsedIndices;
            int usedArgs = arena.UsedArgsRecords;

            GpuSurfaceArenaBuild build = GpuSurfaceArenaBridge.Build(context, arena);

            Assert.AreEqual(GpuSurfaceArenaBuildStatus.ArenaFull, build.Status);
            Assert.IsFalse(build.Lease.IsValid);
            Assert.AreEqual(usedVertices, arena.UsedVertices);
            Assert.AreEqual(usedIndices, arena.UsedIndices);
            Assert.AreEqual(usedArgs, arena.UsedArgsRecords,
                "A failed replacement must not release or overwrite the published draw lease.");
            Assert.AreEqual(0UL, context.ChunksWritten,
                "Arena pressure must refuse before the GPU writes a replacement payload.");

            arena.Release(oldLease);
        }

        private GpuSurfaceExtractionContext CreateContext()
        {
            GpuSurfaceExtractionContext context = GpuSurfaceExtractionContext.TryCreate(
                CellsPerAxis, Padding,
                mirrorBudgetBytes: GpuBrickBufferLayout.BytesPerMixedBrick * 8L,
                brickCacheEdge: ProductionBrickCacheEdge,
                shader: _shader);
            Assert.NotNull(context, "The graphical CI device must be able to create the GPU extraction context.");
            Assert.AreEqual(ProductionBrickCacheEdge, context.BrickCacheEdge,
                "The bridge must consume the same flattened brick-cache dimensions as the CPU snapshot.");

            MaterialPaletteView palette = default;
            context.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, palette);
            return context;
        }

        private static NativeArray<TransvoxelDensityBrick> CreateHalfSolidSnapshot(int edge)
        {
            var bricks = new NativeArray<TransvoxelDensityBrick>(
                edge * edge * edge, Allocator.Temp, NativeArrayOptions.ClearMemory);

            for (int z = 0; z < edge; z++)
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
            {
                bool solid = y < 2;
                bricks[x + edge * (y + edge * z)] = new TransvoxelDensityBrick
                {
                    Kind = solid ? (byte)1 : (byte)0,
                    UniformMaterial = solid ? (byte)1 : (byte)0,
                    MixedOffset = -1,
                };
            }

            return bricks;
        }
    }
}
