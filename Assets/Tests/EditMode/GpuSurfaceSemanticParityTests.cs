using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>Real-kernel coverage for semantics that previously forced CPU extraction.</summary>
    public sealed class GpuSurfaceSemanticParityTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int Cells = 8;
        private const int Capacity = 65536;
        private ComputeShader _shader;

        [SetUp]
        public void SetUp()
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("Compute shaders unavailable.");
            _shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(_shader);
        }

        [TestCase(SurfaceStyles.Planar)]
        [TestCase(SurfaceStyles.Sharp)]
        [TestCase(SurfaceStyles.Cubic)]
        public void ExactReconstructionEmitsEveryOccupiedBoundaryFace(ushort style)
        {
            using var mirror = new GpuVoxelBrickMirror(8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, Cells, 2);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, null);
            PublishRepeatedHalfBrick(mirror, extractor, style, coating: 0,
                                     out NativeArray<byte> voxels,
                                     out NativeArray<ushort> semantics,
                                     out NativeArray<byte> boundaries);
            var vertices = new ComputeBuffer(Capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(Capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                var request = new GpuChunkExtraction(int3.zero, new int3(-1), 1, 1f);
                GpuExtractionCounts counts = extractor.Count(mirror, tables, request);
                Assert.IsFalse(counts.Unsupported, "A supported reconstruction must stay on GPU.");
                // The repeated brick is solid for y=0..3. Its x/z neighbours repeat as solid;
                // only the 8x8 top and bottom planes are exposed: 128 independent exact quads.
                Assert.AreEqual(128 * 4, counts.VertexCount);
                Assert.AreEqual(128 * 6, counts.IndexCount);

                GpuExtractionResult result = extractor.WriteRange(
                    mirror, tables, request, vertices, indices, 0, counts.VertexCount,
                    0, counts.IndexCount);
                Assert.IsFalse(result.Overflowed);
                Assert.AreEqual(counts.VertexCount, result.VertexCount);
                Assert.AreEqual(counts.IndexCount, result.IndexCount);

                var readback = new GpuSurfaceExtractor.ReadbackVertex[result.VertexCount];
                vertices.GetData(readback);
                foreach (GpuSurfaceExtractor.ReadbackVertex vertex in readback)
                {
                    Assert.AreEqual(1u, vertex.Material & 0xFFu);
                    Assert.AreEqual(style, (vertex.Material >> 16) & 0xFFu);
                    Assert.AreEqual(0f, vertex.Normal.x, 1e-6f);
                    Assert.AreEqual(1f, Mathf.Abs(vertex.Normal.y), 1e-6f);
                    Assert.AreEqual(0f, vertex.Normal.z, 1e-6f);
                }
            }
            finally
            {
                voxels.Dispose(); semantics.Dispose(); boundaries.Dispose();
                vertices.Release(); indices.Release();
            }
        }

        [Test]
        public void MossClumpsAddTheDeterministicCpuGeometryWithoutFallback()
        {
            GpuExtractionResult plain = ExtractSmooth(coating: 0);
            GpuExtractionResult moss = ExtractSmooth(coating: Coatings.Moss);
            int clumps = ExpectedTopMossClumps();
            Assert.Greater(clumps, 0, "Fixture must select at least one deterministic clump.");
            Assert.AreEqual(clumps * 12, moss.VertexCount - plain.VertexCount);
            Assert.AreEqual(clumps * 48, moss.IndexCount - plain.IndexCount);
        }

        [Test]
        public void ReservedScratchWritePublishesArgsWithoutASecondReadback()
        {
            using var mirror = new GpuVoxelBrickMirror(8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, Cells, 2);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, null);
            PublishRepeatedHalfBrick(mirror, extractor, SurfaceStyles.Smooth, 0,
                                     out NativeArray<byte> voxels,
                                     out NativeArray<ushort> semantics,
                                     out NativeArray<byte> boundaries);
            var request = new GpuChunkExtraction(int3.zero, new int3(-1), 1, 1f);
            GpuExtractionCounts counts = extractor.Count(mirror, tables, request);
            const int vertexStart = 128;
            const int indexStart = 256;
            const int argsStart = 4;
            var vertices = new ComputeBuffer(vertexStart + counts.VertexCount,
                                             GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(indexStart + counts.IndexCount, sizeof(uint),
                                            ComputeBufferType.Structured);
            var args = new ComputeBuffer(8, sizeof(uint), ComputeBufferType.IndirectArguments);
            try
            {
                ulong readbacksAfterCount = extractor.CounterReadbacks;
                extractor.WriteRangeToScratch(
                    mirror, tables, request, counts.VertexCount, counts.IndexCount);
                extractor.CopyCompletedWriteRange(vertices, indices, args, argsStart,
                    vertexStart, counts.VertexCount, indexStart, counts.IndexCount);
                var argsReadback = new uint[8];
                args.GetData(argsReadback); // verification-only synchronization
                Assert.AreEqual(readbacksAfterCount, extractor.CounterReadbacks,
                    "Reserved production writes must not request per-chunk verification counters.");
                Assert.AreEqual((uint)counts.IndexCount, argsReadback[argsStart]);
                Assert.AreEqual(1u, argsReadback[argsStart + 1]);
                Assert.AreEqual(0u, argsReadback[argsStart + 2]);
                Assert.AreEqual(0u, argsReadback[argsStart + 3]);
            }
            finally
            {
                voxels.Dispose(); semantics.Dispose(); boundaries.Dispose();
                vertices.Release(); indices.Release(); args.Release();
            }
        }

        private GpuExtractionResult ExtractSmooth(byte coating)
        {
            using var mirror = new GpuVoxelBrickMirror(8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, Cells, 2);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(),
                                    CoatingCatalogueView.CreateBuiltIns(), null);
            PublishRepeatedHalfBrick(mirror, extractor, SurfaceStyles.Smooth, coating,
                                     out NativeArray<byte> voxels,
                                     out NativeArray<ushort> semantics,
                                     out NativeArray<byte> boundaries);
            var vertices = new ComputeBuffer(Capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(Capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, new int3(-1), 1, 1f,
                    vertices, indices, Capacity, Capacity);
                Assert.IsFalse(result.Overflowed);
                return result;
            }
            finally
            {
                voxels.Dispose(); semantics.Dispose(); boundaries.Dispose();
                vertices.Release(); indices.Release();
            }
        }

        private static void PublishRepeatedHalfBrick(
            GpuVoxelBrickMirror mirror, GpuSurfaceExtractor extractor, ushort style, byte coating,
            out NativeArray<byte> voxels, out NativeArray<ushort> semantics,
            out NativeArray<byte> boundaries, Allocator allocator = Allocator.Temp)
        {
            voxels = new NativeArray<byte>(512, allocator);
            semantics = new NativeArray<ushort>(512, allocator);
            boundaries = new NativeArray<byte>(512, allocator);
            ushort packed = new VoxelSurfaceSemantics
            {
                StyleId = style,
                CoatingId = coating,
            }.PackedStorage;
            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                voxels[i] = (byte)(y < 4 ? 1 : 0);
                semantics[i] = packed;
            }
            Assert.AreEqual(GpuBrickPublish.Uploaded,
                mirror.Publish(VoxelBrickDelta.MixedAt(int3.zero, 1, 0),
                               voxels, semantics, boundaries, 0, true));
            Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));
            extractor.ClearBrickCache();
            uint entry = GpuSurfaceExtractor.PackBrickCacheEntry(
                VoxelBrickContent.Mixed, 0, slot);
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
                extractor.SetBrickCacheEntry(new int3(x, y, z), entry);
        }

        private static int ExpectedTopMossClumps()
        {
            int count = 0;
            for (int z = 0; z < Cells; z++)
            for (int x = 0; x < Cells; x++)
                if ((DecorationHash(new int3(x, 3, z), Coatings.Moss + 3 * 17) & 0xFFu) < 210u)
                    count++;
            return count;
        }

        private static uint DecorationHash(int3 voxel, int coating)
        {
            uint h = (uint)voxel.x * 0x9E3779B9u ^ (uint)voxel.y * 0x85EBCA6Bu
                   ^ (uint)voxel.z * 0xC2B2AE35u ^ (uint)coating * 0x27D4EB2Fu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15;
            return h;
        }
    }
}
