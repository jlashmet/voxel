using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class GpuSurfaceArenaBridgeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const string PageArenaShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/GpuSurfacePageArena.compute";
        private const string DrawCompactShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/GpuSurfaceDrawCompact.compute";
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
        public void BaseRingProductionDimensionsStageDirectlyIntoTheArena()
        {
            const int baseCellsPerAxis = CpuTransvoxelChunkCache.CellsPerAxis;
            const int basePadding = 1;
            const int baseBrickCacheEdge = 10; // 64 / 8 core bricks + one snapshot brick per side.

            using GpuSurfaceExtractionContext context = GpuSurfaceExtractionContext.TryCreate(
                baseCellsPerAxis, basePadding,
                mirrorBudgetBytes: GpuBrickBufferLayout.BytesPerMixedBrick * 8L,
                brickCacheEdge: baseBrickCacheEdge,
                shader: _shader);
            Assert.NotNull(context);
            Assert.AreEqual(baseBrickCacheEdge, context.BrickCacheEdge);

            MaterialPaletteView palette = default;
            context.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, palette);

            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(baseBrickCacheEdge);
            using var arena = new SurfaceGeometryArena(131072, 262144, 4);

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1),
                                                 sourceStep: 1, voxelSize: 0.1f,
                                                 transitionFaceMask: 0);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));

            GpuSurfaceArenaBuild build = GpuSurfaceArenaBridge.Build(context, arena);

            Assert.AreEqual(GpuSurfaceArenaBuildStatus.Ready, build.Status,
                "The cutover path must work at the base ring's real 64-cell production dimensions, "
              + "not only the small parity fixture.");
            Assert.Greater(build.VertexCount, 0);
            Assert.Greater(build.IndexCount, 0);
            Assert.AreEqual(0UL, context.Extractor.GeometryReadbacks);
            Assert.AreEqual(2UL, context.Extractor.CounterReadbacks);

            var args = new uint[SurfaceGeometryArena.ArgsWordsPerDraw];
            arena.Args.GetData(args, 0, build.Lease.ArgsWordStart, args.Length);
            Assert.AreEqual((uint)build.IndexCount, args[0]);

            arena.Release(build.Lease);
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

        [Test]
        public void ReleasedContextRejectsStaleBatchCompletionAndRetiresItsUnconsumedLease()
        {
            using var arena = new SurfaceGeometryArena(32768, 65536, 8);
            using GpuSurfaceExtractionContext context = GpuSurfaceExtractionContext.TryCreate(
                CellsPerAxis, Padding,
                mirrorBudgetBytes: GpuBrickBufferLayout.BytesPerMixedBrick * 8L,
                brickCacheEdge: ProductionBrickCacheEdge,
                surfaceArena: arena,
                shader: _shader);
            Assert.NotNull(context);
            context.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, default);
            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            var request = new GpuChunkExtraction(
                int3.zero, new int3(-1), sourceStep: 1, voxelSize: 0.1f);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));
            GpuExtractionCounts counts = context.StagedCounts;
            Assert.IsTrue(arena.TryAcquire(counts.VertexCount, counts.IndexCount,
                                           out SurfaceGeometryLease lease));

            Assert.IsTrue(context.CompleteBatchedCount(
                0, counts, failed: false, lease: lease));
            context.Release();
            Assert.IsFalse(context.CompleteBatchedCount(
                0, counts, failed: false, lease: lease),
                "A completion from the released generation must not reinstall its arena range.");
            arena.RetireExpiredLeases(3);
            Assert.AreEqual(0, arena.UsedVertices);
            Assert.AreEqual(0, arena.UsedIndices);
            Assert.AreEqual(0, arena.UsedArgsRecords);
        }

        [Test]
        public void BatchedEmptyCountCompletesWithoutArenaLeaseOrRedispatchToken()
        {
            using GpuSurfaceExtractionContext context = CreateContext();
            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            var request = new GpuChunkExtraction(
                int3.zero, new int3(-1), sourceStep: 1, voxelSize: 0.1f);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));

            Assert.IsTrue(context.CompleteBatchedCount(
                0, default, failed: false), "The live generation must accept its empty record.");
            Assert.AreEqual(GpuSurfaceExtractor.GpuCounterPoll.Ready,
                context.TryCompleteStage(out GpuExtractionCounts counts));
            Assert.IsTrue(counts.IsEmpty,
                "Empty must remain empty so the cache takes its lease-free air-publication path.");
            Assert.IsFalse(context.TryTakeCountBatchLease(out _));
            Assert.AreEqual(1UL, context.ChunksEmpty);
        }

        [Test]
        public void TrueDispatchBatchCountsAndWritesOrdinaryAndTransitionChunks()
        {
            using GpuSurfaceExtractionContext context = CreateContext();
            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            var request = new GpuChunkExtraction(
                int3.zero, int3.zero, sourceStep: 2, voxelSize: 0.1f);
            var transitionRequest = new GpuChunkExtraction(
                int3.zero, int3.zero, sourceStep: 2, voxelSize: 0.1f,
                transitionFaceMask: 0b000011);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));
            GpuExtractionCounts expected = context.Extractor.Count(
                context.Mirror, context.Tables, request);
            GpuExtractionCounts transitionExpected = context.Extractor.Count(
                context.Mirror, context.Tables, transitionRequest);
            var requests = new[] { request, transitionRequest };
            using GpuSurfaceExtractor.CountBatchResources resources =
                context.Extractor.CreateCountBatchResources(requests.Length);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords
                    + requests.Length * GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            context.Extractor.DispatchCountBatch(
                context.Mirror, context.Tables, requests, requests.Length,
                counters, resources);
            context.Extractor.PrefixCountBatch(
                counters, requests.Length,
                SurfaceGeometryArena.VertexAlignment,
                SurfaceGeometryArena.IndexAlignment);
            var words = new uint[counters.count];
            counters.GetData(words);

            int first = GpuSurfaceExtractor.BatchHeaderWords;
            int second = first + GpuSurfaceExtractor.BatchRecordWords;
            Assert.AreEqual((uint)expected.VertexCount, words[first + 2]);
            Assert.AreEqual((uint)expected.IndexCount, words[first + 3]);
            Assert.AreEqual((uint)transitionExpected.VertexCount, words[second + 2]);
            Assert.AreEqual((uint)transitionExpected.IndexCount, words[second + 3]);
            Assert.AreEqual(2u, words[2]);

            using var vertices = new ComputeBuffer(
                (int)words[0], GpuSurfaceExtractor.ReadbackVertex.Stride,
                ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(
                (int)words[1], sizeof(uint), ComputeBufferType.Structured);
            context.Extractor.DispatchBaseWriteBatch(
                context.Mirror, context.Tables, requests.Length,
                counters, resources, vertices, indices);
            counters.GetData(words);
            Assert.AreEqual((uint)expected.VertexCount, words[first + 8]);
            Assert.AreEqual((uint)expected.IndexCount, words[first + 9]);
            Assert.AreEqual((uint)transitionExpected.VertexCount, words[second + 8]);
            Assert.AreEqual((uint)transitionExpected.IndexCount, words[second + 9],
                $"ordinary={expected.VertexCount}/{expected.IndexCount} "
              + $"transition={transitionExpected.VertexCount}/{transitionExpected.IndexCount} "
              + $"written={words[second + 8]}/{words[second + 9]}");
        }

        [Test]
        public void TrueDispatchBatchWritesTwoSmoothChunksIntoGpuPrefixRanges()
        {
            using GpuSurfaceExtractionContext context = CreateContext();
            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            var request = new GpuChunkExtraction(
                int3.zero, int3.zero, sourceStep: 1, voxelSize: 0.1f);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));
            GpuExtractionCounts expected = context.Extractor.Count(
                context.Mirror, context.Tables, request);
            var requests = new[] { request, request };
            using GpuSurfaceExtractor.CountBatchResources resources =
                context.Extractor.CreateCountBatchResources(requests.Length);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords
                    + requests.Length * GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            context.Extractor.DispatchCountBatch(
                context.Mirror, context.Tables, requests, requests.Length,
                counters, resources);
            context.Extractor.PrefixCountBatch(
                counters, requests.Length,
                SurfaceGeometryArena.VertexAlignment,
                SurfaceGeometryArena.IndexAlignment);
            var words = new uint[counters.count];
            counters.GetData(words);
            using var vertices = new ComputeBuffer(
                (int)words[0], GpuSurfaceExtractor.ReadbackVertex.Stride,
                ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(
                (int)words[1], sizeof(uint), ComputeBufferType.Structured);
            context.Extractor.DispatchBaseWriteBatch(
                context.Mirror, context.Tables, requests.Length,
                counters, resources, vertices, indices);
            counters.GetData(words);

            int first = GpuSurfaceExtractor.BatchHeaderWords;
            int second = first + GpuSurfaceExtractor.BatchRecordWords;
            Assert.AreEqual((uint)expected.VertexCount, words[first + 8]);
            Assert.AreEqual((uint)expected.IndexCount, words[first + 9]);
            Assert.AreEqual((uint)expected.VertexCount, words[second + 8]);
            Assert.AreEqual((uint)expected.IndexCount, words[second + 9]);
        }

        [Test]
        public void TrueDispatchBatchCountsAndWritesRetainedProfilesWithoutCpuFallback()
        {
            using GpuSurfaceExtractionContext context = CreateContext();
            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            var plain = new GpuChunkExtraction(
                int3.zero, int3.zero, sourceStep: 1, voxelSize: 1f);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, plain, generation: 1));

            var profile = new ProfileBlock
            {
                Centre = new int3(4, 4, 4),
                InnerRadiusQ4 = 16,
                OuterRadiusQ4 = 48,
                FrontQ4 = 32,
                BackQ4 = 96,
                BackingDepthVoxel = 4,
                StartDirection = new int2(4096, 0),
                EndDirection = new int2(0, 4096),
                Axis = 2,
                Material = 1,
                SurfaceStyle = SurfaceStyles.Rounded,
            };
            var profiled = new GpuChunkExtraction(
                int3.zero, int3.zero, sourceStep: 1, voxelSize: 1f,
                profileBlocks: new[] { profile });
            var requests = new[] { plain, profiled };
            using GpuSurfaceExtractor.CountBatchResources resources =
                context.Extractor.CreateCountBatchResources(requests.Length);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords
                    + requests.Length * GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            context.Extractor.DispatchCountBatch(
                context.Mirror, context.Tables, requests, requests.Length,
                counters, resources);
            context.Extractor.PrefixCountBatch(
                counters, requests.Length,
                SurfaceGeometryArena.VertexAlignment,
                SurfaceGeometryArena.IndexAlignment);
            var words = new uint[counters.count];
            counters.GetData(words);
            int first = GpuSurfaceExtractor.BatchHeaderWords;
            int second = first + GpuSurfaceExtractor.BatchRecordWords;
            // Quarter-circle radius 3 resolves to nine segments. Six quads per segment plus
            // two quads on each radial end are wholly owned by this chunk.
            const uint profileQuads = 9u * 6u + 4u;
            Assert.AreEqual(words[first + 2] + profileQuads * 4u, words[second + 2]);
            Assert.AreEqual(words[first + 3] + profileQuads * 6u, words[second + 3]);

            using var vertices = new ComputeBuffer(
                (int)words[0], GpuSurfaceExtractor.ReadbackVertex.Stride,
                ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(
                (int)words[1], sizeof(uint), ComputeBufferType.Structured);
            context.Extractor.DispatchBaseWriteBatch(
                context.Mirror, context.Tables, requests.Length,
                counters, resources, vertices, indices);
            counters.GetData(words);
            Assert.AreEqual(words[second + 2], words[second + 8]);
            Assert.AreEqual(words[second + 3], words[second + 9]);

            var readback = new GpuSurfaceExtractor.ReadbackVertex[(int)words[0]];
            vertices.GetData(readback);
            int profileVertices = readback.Count(vertex =>
                (vertex.Material & 0xFFu) == 1u
                && ((vertex.Material >> 16) & 0xFFu) == SurfaceStyles.Rounded);
            Assert.AreEqual((int)(profileQuads * 4u), profileVertices,
                "Every retained-profile vertex must carry its authored style on the GPU path.");
        }

        [Test]
        public void PublishedBatchGeometrySkipsPerChunkWriteChain()
        {
            using var arena = new SurfaceGeometryArena(32768, 65536, 8);
            using GpuSurfaceExtractionContext context = GpuSurfaceExtractionContext.TryCreate(
                CellsPerAxis, Padding,
                mirrorBudgetBytes: GpuBrickBufferLayout.BytesPerMixedBrick * 8L,
                brickCacheEdge: ProductionBrickCacheEdge,
                surfaceArena: arena, shader: _shader);
            Assert.NotNull(context);
            context.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, default);
            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            var request = new GpuChunkExtraction(
                int3.zero, int3.zero, sourceStep: 1, voxelSize: 0.1f);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));
            GpuExtractionCounts expected = context.StagedCounts;
            Assert.IsTrue(arena.TryAcquire(expected.VertexCount, expected.IndexCount,
                                           out SurfaceGeometryLease lease));
            Assert.IsTrue(context.CompleteBatchedCount(
                0, expected, failed: false, lease: lease, geometryPublished: true));
            Assert.AreEqual(GpuSurfaceExtractor.GpuCounterPoll.Ready,
                context.TryCompleteStage(out _));
            Assert.IsTrue(context.TryTakeCountBatchLease(out SurfaceGeometryLease stagedLease));
            context.BeginWriteRange(
                arena.Vertices, arena.Indices, arena.Args, stagedLease.ArgsWordStart,
                stagedLease.VertexStart, stagedLease.VertexCapacity,
                stagedLease.IndexStart, stagedLease.IndexCapacity);
            Assert.AreEqual(GpuSurfaceExtractor.GpuCounterPoll.Ready,
                context.TryCompleteWriteRange(out int indexCount));
            Assert.AreEqual(expected.IndexCount, indexCount);
            arena.Release(in stagedLease);
        }

        [Test]
        public void GpuPageArenaAllocatesWritesAndPublishesWithoutProductionReadback()
        {
            ComputeShader arenaShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                PageArenaShaderPath);
            Assert.NotNull(arenaShader);
            using var pageArena = new GpuSurfacePageArena(
                arenaShader, vertexCapacity: 32768, indexCapacity: 65536,
                handleCapacity: 16);
            using GpuSurfaceExtractionContext context = CreateContext();
            using NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            const ulong generation = 0x0000000200000003UL;
            var request = new GpuChunkExtraction(
                int3.zero, int3.zero, sourceStep: 1, voxelSize: 0.1f,
                handle: 0, generation: generation);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation));
            var requests = new[] { request };
            using GpuSurfaceExtractor.CountBatchResources resources =
                context.Extractor.CreateCountBatchResources(1);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            pageArena.QueueGeneration(0, generation);
            pageArena.FlushHandleCommands(frame: 10);
            context.Extractor.DispatchCountBatch(
                context.Mirror, context.Tables, requests, 1, counters, resources);
            context.Extractor.PrefixCountBatch(
                counters, 1, SurfaceGeometryArena.VertexAlignment,
                SurfaceGeometryArena.IndexAlignment);
            pageArena.AllocateBatch(resources.Chunks, counters, 1,
                                    GpuSurfaceExtractor.BatchRecordWords, frame: 10);
            context.Extractor.DispatchBaseWriteBatch(
                context.Mirror, context.Tables, 1, counters, resources,
                pageArena.Vertices, pageArena.Indices,
                pageArena: pageArena, frame: 10);

            var live = new uint[8];
            pageArena.LiveChunkGeometry.GetData(live, 0, 0, live.Length);
            Assert.AreEqual(unchecked((uint)generation), live[0]);
            Assert.AreEqual((uint)(generation >> 32), live[1]);
            Assert.AreEqual((uint)context.StagedCounts.VertexCount, live[3]);
            Assert.AreEqual((uint)context.StagedCounts.IndexCount, live[4]);
            Assert.AreEqual(1u, live[7]);
            Assert.AreEqual(0UL, context.Extractor.GeometryReadbacks);

            ComputeShader drawShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                DrawCompactShaderPath);
            Assert.NotNull(drawShader);
            using var draws = new GpuSurfaceDrawDispatcher(drawShader, pageArena);
            draws.Prepare(new[] { 0 }, frame: 11);
            var args = new uint[GpuSurfaceDrawDispatcher.BucketCount * 4];
            draws.ActiveIndirectArgs.GetData(args);
            Assert.AreEqual(1, args.Where((_, i) => i % 4 == 1).Sum(value => (int)value),
                "One live handle must become exactly one indirect instance without CPU metadata.");
        }

        [Test]
        public void GpuPageArenaRejectsStaleGenerationAndKeepsLiveGeometry()
        {
            ComputeShader arenaShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                PageArenaShaderPath);
            using var arena = new GpuSurfacePageArena(
                arenaShader, GpuSurfacePageArena.VertexPageSize * 2,
                GpuSurfacePageArena.IndexPageSize * 2, handleCapacity: 2);
            using var descriptors = new ComputeBuffer(
                1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride,
                ComputeBufferType.Structured);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            arena.QueueGeneration(0, 2);
            arena.FlushHandleCommands(frame: 1);
            SetAllocationRequest(descriptors, counters, handle: 0, generation: 2,
                                 vertexCount: 32, indexCount: 48);
            arena.AllocateBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 1);
            arena.PublishBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 1);

            arena.QueueGeneration(0, 3);
            arena.FlushHandleCommands(frame: 2);
            SetAllocationRequest(descriptors, counters, handle: 0, generation: 2,
                                 vertexCount: 64, indexCount: 96);
            arena.AllocateBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 2);
            arena.PublishBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 2);

            var live = new uint[8];
            arena.LiveChunkGeometry.GetData(live);
            Assert.AreEqual(2u, live[0]);
            Assert.AreEqual(32u, live[3]);
            Assert.AreEqual(48u, live[4]);
            Assert.AreEqual(1u, live[7]);
        }

        [Test]
        public void GpuPageArenaExhaustionRetainsOldLiveGeometry()
        {
            ComputeShader arenaShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                PageArenaShaderPath);
            using var arena = new GpuSurfacePageArena(
                arenaShader, GpuSurfacePageArena.VertexPageSize,
                GpuSurfacePageArena.IndexPageSize, handleCapacity: 1);
            using var descriptors = new ComputeBuffer(
                1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride,
                ComputeBufferType.Structured);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            PublishAllocation(arena, descriptors, counters, 0, 1, 32, 48, frame: 1);
            arena.QueueGeneration(0, 2);
            arena.FlushHandleCommands(frame: 2);
            SetAllocationRequest(descriptors, counters, 0, 2, 64, 96);
            arena.AllocateBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 2);
            arena.PublishBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 2);

            var live = new uint[8];
            arena.LiveChunkGeometry.GetData(live);
            Assert.AreEqual(1u, live[0]);
            Assert.AreEqual(32u, live[3]);
            Assert.AreEqual(48u, live[4]);
            Assert.AreEqual(1u, live[7]);
        }

        [Test]
        public void GpuPageArenaDoesNotReuseRetiredPagesBeforeSafeEpoch()
        {
            ComputeShader arenaShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                PageArenaShaderPath);
            using var arena = new GpuSurfacePageArena(
                arenaShader, GpuSurfacePageArena.VertexPageSize,
                GpuSurfacePageArena.IndexPageSize, handleCapacity: 2);
            using var descriptors = new ComputeBuffer(
                1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride,
                ComputeBufferType.Structured);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            PublishAllocation(arena, descriptors, counters, 0, 1, 32, 48, frame: 1);
            arena.QueueRelease(0, 1);
            arena.FlushHandleCommands(frame: 10);
            arena.QueueGeneration(1, 1);
            arena.FlushHandleCommands(frame: 10);
            SetAllocationRequest(descriptors, counters, 1, 1, 16, 24);
            arena.AllocateBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 13);
            arena.PublishBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 13);
            var live = new uint[16];
            arena.LiveChunkGeometry.GetData(live);
            Assert.AreEqual(0u, live[8 + 7], "A retired page was reused before its safe epoch.");

            SetAllocationRequest(descriptors, counters, 1, 1, 16, 24);
            arena.AllocateBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 14);
            arena.PublishBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame: 14);
            arena.LiveChunkGeometry.GetData(live);
            Assert.AreEqual(1u, live[8 + 7], "The page was not reclaimed at its safe epoch.");
        }

        [Test]
        public void ReleasedHandleCannotBeReusedBeforeItsGpuCommandIsSubmitted()
        {
            ComputeShader arenaShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                PageArenaShaderPath);
            using var arena = new GpuSurfacePageArena(
                arenaShader, GpuSurfacePageArena.VertexPageSize,
                GpuSurfacePageArena.IndexPageSize, handleCapacity: 1);
            Assert.IsTrue(arena.TryAcquireHandle(out int handle));
            arena.QueueRelease(handle, generation: 1);
            Assert.IsFalse(arena.TryAcquireHandle(out _));
            arena.FlushHandleCommands(frame: 1);
            Assert.IsTrue(arena.TryAcquireHandle(out int reused));
            Assert.AreEqual(handle, reused);
        }

        [UnityTest]
        public IEnumerator ProductionScratchPublicationUsesGraphicsQueueOrderingWithoutReadback()
        {
            using var arena = new SurfaceGeometryArena(32768, 65536, 8);
            using GpuSurfaceExtractionContext context = GpuSurfaceExtractionContext.TryCreate(
                CellsPerAxis, Padding,
                mirrorBudgetBytes: GpuBrickBufferLayout.BytesPerMixedBrick * 8L,
                brickCacheEdge: ProductionBrickCacheEdge,
                surfaceArena: arena,
                shader: _shader);
            Assert.NotNull(context);
            context.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, default);
            NativeArray<TransvoxelDensityBrick> bricks =
                CreateHalfSolidSnapshot(context.BrickCacheEdge);
            var request = new GpuChunkExtraction(
                int3.zero, new int3(-1), sourceStep: 1, voxelSize: 0.1f);
            Assert.AreEqual(GpuStageOutcome.Staged,
                context.TryStage(bricks, default, default, default, request, generation: 1));
            // TryStage consumes the legacy snapshot synchronously. Allocator.Temp storage must not
            // survive the first yielded frame in a UnityTest.
            bricks.Dispose();
            GpuExtractionCounts counts = context.StagedCounts;
            Assert.IsTrue(arena.TryAcquire(counts.VertexCount, counts.IndexCount,
                                           out SurfaceGeometryLease lease));

            context.BeginWriteRange(
                arena.Vertices, arena.Indices, arena.Args, lease.ArgsWordStart,
                lease.VertexStart, lease.VertexCapacity,
                lease.IndexStart, lease.IndexCapacity);
            GpuSurfaceExtractor.GpuCounterPoll poll = GpuSurfaceExtractor.GpuCounterPoll.Pending;
            int indexCount = 0;
            for (int frame = 0; frame < 120 && poll == GpuSurfaceExtractor.GpuCounterPoll.Pending;
                 frame++)
            {
                poll = context.TryCompleteWriteRange(out indexCount);
                if (poll == GpuSurfaceExtractor.GpuCounterPoll.Pending) yield return null;
            }

            Assert.AreEqual(GpuSurfaceExtractor.GpuCounterPoll.Ready, poll,
                "Graphics-queue publication did not become CPU-visible within 120 frames.");
            Assert.AreEqual(counts.IndexCount, indexCount);
            Assert.AreEqual(0UL, context.Extractor.GeometryReadbacks,
                "Production publication must not read back geometry or use it as a completion token.");
            var args = new uint[SurfaceGeometryArena.ArgsWordsPerDraw];
            arena.Args.GetData(args, 0, lease.ArgsWordStart, args.Length);
            Assert.AreEqual((uint)counts.IndexCount, args[0]);
            arena.Release(in lease);
        }

        [UnityTest]
        public IEnumerator TwoChunksCountAndPrefixInOneCommandBatch()
        {
            using GpuSurfaceExtractionContext first = CreateContext();
            using GpuSurfaceExtractionContext second = CreateContext();
            NativeArray<TransvoxelDensityBrick> firstBricks =
                CreateHalfSolidSnapshot(first.BrickCacheEdge);
            NativeArray<TransvoxelDensityBrick> secondBricks =
                CreateHalfSolidSnapshot(second.BrickCacheEdge);
            var firstRequest = new GpuChunkExtraction(
                int3.zero, new int3(-1), sourceStep: 1, voxelSize: 0.1f);
            var secondRequest = new GpuChunkExtraction(
                new int3(CellsPerAxis, 0, 0), new int3(-1),
                sourceStep: 1, voxelSize: 0.1f);
            Assert.AreEqual(GpuStageOutcome.Staged,
                first.TryStage(firstBricks, default, default, default, firstRequest, 1));
            Assert.AreEqual(GpuStageOutcome.Staged,
                second.TryStage(secondBricks, default, default, default, secondRequest, 1));
            GpuExtractionCounts firstExpected = first.StagedCounts;
            GpuExtractionCounts secondExpected = second.StagedCounts;
            firstBricks.Dispose();
            secondBricks.Dispose();

            const int recordCount = 2;
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords
                    + recordCount * GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);
            using var commands = new CommandBuffer { name = "GPU surface two-chunk test batch" };
            first.Extractor.RecordCountToBatch(
                commands, first.Mirror, first.Tables, firstRequest, counters, 0);
            second.Extractor.RecordCountToBatch(
                commands, second.Mirror, second.Tables, secondRequest, counters, 1);
            first.Extractor.RecordPrefixCountBatch(
                commands, counters, recordCount,
                SurfaceGeometryArena.VertexAlignment,
                SurfaceGeometryArena.IndexAlignment);
            Graphics.ExecuteCommandBuffer(commands);

            AsyncGPUReadbackRequest readback = AsyncGPUReadback.Request(counters);
            for (int frame = 0; frame < 120 && !readback.done && !readback.hasError; frame++)
                yield return null;
            Assert.IsFalse(readback.hasError, "The cross-chunk count batch readback failed.");
            Assert.IsTrue(readback.done, "The cross-chunk count batch did not finish in 120 frames.");
            NativeArray<uint> words = readback.GetData<uint>();
            int firstWord = GpuSurfaceExtractor.BatchHeaderWords;
            int secondWord = firstWord + GpuSurfaceExtractor.BatchRecordWords;
            Assert.AreEqual((uint)firstExpected.VertexCount, words[firstWord + 2]);
            Assert.AreEqual((uint)firstExpected.IndexCount, words[firstWord + 3]);
            Assert.AreEqual((uint)secondExpected.VertexCount, words[secondWord + 2]);
            Assert.AreEqual((uint)secondExpected.IndexCount, words[secondWord + 3]);
            Assert.AreEqual(words[firstWord + 6] + words[secondWord + 6], words[0]);
            Assert.AreEqual(words[firstWord + 7] + words[secondWord + 7], words[1]);
            Assert.AreEqual(2u, words[2]);
        }

        private static void PublishAllocation(
            GpuSurfacePageArena arena, ComputeBuffer descriptors, ComputeBuffer counters,
            int handle, ulong generation, uint vertexCount, uint indexCount, int frame)
        {
            arena.QueueGeneration(handle, generation);
            arena.FlushHandleCommands(frame);
            SetAllocationRequest(descriptors, counters, handle, generation,
                                 vertexCount, indexCount);
            arena.AllocateBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame);
            arena.PublishBatch(descriptors, counters, 1,
                GpuSurfaceExtractor.BatchRecordWords, frame);
        }

        private static void SetAllocationRequest(
            ComputeBuffer descriptors, ComputeBuffer counters,
            int handle, ulong generation, uint vertexCount, uint indexCount)
        {
            descriptors.SetData(new[]
            {
                new GpuSurfaceExtractor.BatchChunkDescriptor
                {
                    Handle = unchecked((uint)handle),
                    GenerationLow = unchecked((uint)generation),
                    GenerationHigh = unchecked((uint)(generation >> 32)),
                    SourceStep = 1,
                    VoxelSize = 0.1f,
                }
            });
            var words = new uint[
                GpuSurfaceExtractor.BatchHeaderWords + GpuSurfaceExtractor.BatchRecordWords];
            int record = GpuSurfaceExtractor.BatchHeaderWords;
            words[record + 2] = vertexCount;
            words[record + 3] = indexCount;
            counters.SetData(words);
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
