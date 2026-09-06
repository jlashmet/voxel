using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
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

                var indexReadback = new uint[result.IndexCount];
                indices.GetData(indexReadback);
                AssertCanonicalHalfBrickFaces(readback, indexReadback);
            }
            finally
            {
                voxels.Dispose(); semantics.Dispose(); boundaries.Dispose();
                vertices.Release(); indices.Release();
            }
        }

        // Independent analytic oracle: occupied y=[0,4), repeated in neighbouring bricks,
        // exposes exactly two 8x8 planes. This checks geometry without executing a CPU mesher
        // or depending on GPU append order / choice of quad diagonal.
        private static void AssertCanonicalHalfBrickFaces(
            GpuSurfaceExtractor.ReadbackVertex[] vertices, uint[] indices)
        {
            var faceAreas = new float[2, Cells, Cells];
            var firstTriangleMasks = new int[2, Cells, Cells];
            var triangles = new HashSet<(int, int, int)>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                var corners = new Vector3[3];
                var keys = new int[3];
                int plane = -1;
                for (int corner = 0; corner < 3; corner++)
                {
                    uint index = indices[i + corner];
                    Assert.Less(index, (uint)vertices.Length, "Index points outside written geometry.");
                    var vertex = vertices[index];
                    Vector3 p = vertex.Position;
                    Assert.That(p.y, Is.EqualTo(0f).Or.EqualTo(4f), "Face moved off its occupied boundary.");
                    int cornerPlane = p.y == 0f ? 0 : 1;
                    if (plane < 0) plane = cornerPlane;
                    Assert.AreEqual(plane, cornerPlane, "Triangle bridges separate boundary planes.");
                    Assert.That(p.x, Is.InRange(0f, (float)Cells));
                    Assert.That(p.z, Is.InRange(0f, (float)Cells));
                    Assert.AreEqual(Mathf.Round(p.x), p.x, "Exact face corner must lie on the voxel lattice.");
                    Assert.AreEqual(Mathf.Round(p.z), p.z, "Exact face corner must lie on the voxel lattice.");
                    Assert.AreEqual(plane == 0 ? Vector3.down : Vector3.up, vertex.Normal);
                    corners[corner] = p;
                    keys[corner] = (int)p.x + (Cells + 1) * ((int)p.z + (Cells + 1) * plane);
                }

                Vector3 cross = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
                Vector3 outward = plane == 0 ? Vector3.down : Vector3.up;
                Assert.AreEqual(1f, Vector3.Dot(cross, outward), 1e-6f,
                    "Each triangle must have area 0.5 and outward winding.");
                float minX = Mathf.Min(corners[0].x, Mathf.Min(corners[1].x, corners[2].x));
                float minZ = Mathf.Min(corners[0].z, Mathf.Min(corners[1].z, corners[2].z));
                float maxX = Mathf.Max(corners[0].x, Mathf.Max(corners[1].x, corners[2].x));
                float maxZ = Mathf.Max(corners[0].z, Mathf.Max(corners[1].z, corners[2].z));
                Assert.AreEqual(1f, maxX - minX, "Triangle spans more than one voxel face.");
                Assert.AreEqual(1f, maxZ - minZ, "Triangle spans more than one voxel face.");
                System.Array.Sort(keys);
                Assert.IsTrue(triangles.Add((keys[0], keys[1], keys[2])), "Duplicate boundary triangle.");
                int cornerMask = 0;
                foreach (Vector3 p in corners)
                    cornerMask |= 1 << ((int)(p.x - minX) + 2 * (int)(p.z - minZ));
                int previousMask = firstTriangleMasks[plane, (int)minX, (int)minZ];
                if (previousMask == 0)
                    firstTriangleMasks[plane, (int)minX, (int)minZ] = cornerMask;
                else
                {
                    int sharedCorners = previousMask & cornerMask;
                    Assert.That(sharedCorners, Is.EqualTo(0b1001).Or.EqualTo(0b0110),
                        "The two face triangles must share a diagonal, not overlap across an edge.");
                }
                faceAreas[plane, (int)minX, (int)minZ] += 0.5f;
            }

            for (int plane = 0; plane < 2; plane++)
            for (int z = 0; z < Cells; z++)
            for (int x = 0; x < Cells; x++)
                Assert.AreEqual(1f, faceAreas[plane, x, z],
                    $"Boundary plane {plane}, face ({x},{z}) has missing or overlapping geometry.");
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

        [Test]
        public void SharedCountBufferCarriesTwoDescriptorsInOneTransfer()
        {
            using var mirror = new GpuVoxelBrickMirror(8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, Cells, 2);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, null);
            PublishRepeatedHalfBrick(mirror, extractor, SurfaceStyles.Smooth, 0,
                                     out NativeArray<byte> voxels,
                                     out NativeArray<ushort> semantics,
                                     out NativeArray<byte> boundaries);
            var first = new GpuChunkExtraction(int3.zero, new int3(-1), 1, 1f);
            var second = new GpuChunkExtraction(new int3(8, 0, 0), new int3(-1), 1, 1f);
            GpuExtractionCounts expectedFirst = extractor.Count(mirror, tables, first);
            GpuExtractionCounts expectedSecond = extractor.Count(mirror, tables, second);
            GpuExtractionCounts[] expected = { expectedFirst, expectedSecond };
            var batch = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords + 2 * GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);
            try
            {
                ulong readbacksAfterOracle = extractor.CounterReadbacks;
                extractor.DispatchCountToBatch(mirror, tables, first, batch, 0);
                extractor.DispatchCountToBatch(mirror, tables, second, batch, 1);
                extractor.PrefixCountBatch(batch, 2, vertexAlignment: 256, indexAlignment: 512);
                var words = new uint[batch.count];
                batch.GetData(words); // one verification-only transfer for both descriptors

                Assert.AreEqual(readbacksAfterOracle, extractor.CounterReadbacks,
                    "Appending batch records must not create per-descriptor readbacks.");
                for (int record = 0; record < 2; record++)
                {
                    int word = GpuSurfaceExtractor.BatchHeaderWords
                             + record * GpuSurfaceExtractor.BatchRecordWords;
                    Assert.AreEqual(0u, words[word]);
                    Assert.AreEqual((uint)expected[record].VertexCount, words[word + 2]);
                    Assert.AreEqual((uint)expected[record].IndexCount, words[word + 3]);
                    Assert.AreEqual(0u, words[word + 6] % 256u);
                    Assert.AreEqual(0u, words[word + 7] % 512u);
                }
                int firstWord = GpuSurfaceExtractor.BatchHeaderWords;
                int secondWord = firstWord + GpuSurfaceExtractor.BatchRecordWords;
                Assert.AreEqual(words[firstWord + 6] + words[secondWord + 6], words[0]);
                Assert.AreEqual(words[firstWord + 7] + words[secondWord + 7], words[1]);
                Assert.AreEqual(2u, words[2]);
                Assert.AreEqual(0u, words[firstWord + 1]);
                Assert.AreEqual(1u, words[secondWord + 1]);
                Assert.AreEqual(0u, words[firstWord + 4]);
                Assert.AreEqual(words[firstWord + 6], words[secondWord + 4]);
                Assert.AreEqual(0u, words[firstWord + 5]);
                Assert.AreEqual(words[firstWord + 7], words[secondWord + 5]);
            }
            finally
            {
                batch.Release();
                voxels.Dispose(); semantics.Dispose(); boundaries.Dispose();
            }
        }

        [Test]
        public void ContiguousBatchReservationCanRetireIndependentSubleases()
        {
            using var arena = new SurfaceGeometryArena(2048, 4096, 8);
            Assert.IsTrue(arena.TryAcquireBatch(512, 1024, 2, out SurfaceGeometryLease batch));
            var first = new SurfaceGeometryLease(
                batch.VertexStart, 256, batch.IndexStart, 512, batch.ArgsWordStart);
            var second = new SurfaceGeometryLease(
                batch.VertexStart + 256, 256,
                batch.IndexStart + 512, 512,
                batch.ArgsWordStart + SurfaceGeometryArena.ArgsWordsPerDraw);

            arena.Release(in second);
            arena.Release(in first);
            arena.RetireExpiredLeases(3);

            Assert.AreEqual(0, arena.UsedVertices);
            Assert.AreEqual(0, arena.UsedIndices);
            Assert.AreEqual(0, arena.UsedArgsRecords);
            Assert.IsTrue(arena.TryAcquireBatch(512, 1024, 2, out SurfaceGeometryLease reused));
            Assert.AreEqual(batch.VertexStart, reused.VertexStart);
            Assert.AreEqual(batch.IndexStart, reused.IndexStart);
            Assert.AreEqual(batch.ArgsWordStart, reused.ArgsWordStart);
        }

        [Test]
        public void BatchReservationFailureIsAtomicAtDrawPressureLimit()
        {
            using var arena = new SurfaceGeometryArena(2048, 4096, 8)
            {
                MaxActiveLeases = 1,
            };

            Assert.IsFalse(arena.TryAcquireBatch(512, 1024, 2, out _));
            Assert.AreEqual(0, arena.UsedVertices);
            Assert.AreEqual(0, arena.UsedIndices);
            Assert.AreEqual(0, arena.UsedArgsRecords);
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
