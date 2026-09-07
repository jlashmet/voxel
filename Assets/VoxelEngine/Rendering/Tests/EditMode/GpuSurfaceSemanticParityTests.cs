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
                // only the 8x8 top and bottom planes are exposed: two merged exact quads.
                Assert.AreEqual(2 * 4, counts.VertexCount);
                Assert.AreEqual(2 * 6, counts.IndexCount);

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
            var areas = new float[2];
            var masks = new int[2];
            var triangles = new HashSet<(int, int, int)>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                var corners = new Vector3[3];
                var keys = new int[3];
                int plane = -1, cornerMask = 0;
                for (int corner = 0; corner < 3; corner++)
                {
                    Assert.Less(indices[i + corner], (uint)vertices.Length);
                    var vertex = vertices[indices[i + corner]];
                    Vector3 p = vertex.Position;
                    Assert.That(p.y, Is.EqualTo(0f).Or.EqualTo(4f));
                    Assert.That(p.x, Is.EqualTo(0f).Or.EqualTo((float)Cells));
                    Assert.That(p.z, Is.EqualTo(0f).Or.EqualTo((float)Cells));
                    int currentPlane = p.y == 0f ? 0 : 1;
                    if (plane < 0) plane = currentPlane;
                    Assert.AreEqual(plane, currentPlane, "Triangle bridges distinct boundary planes.");
                    Assert.AreEqual(plane == 0 ? Vector3.down : Vector3.up, vertex.Normal);
                    corners[corner] = p;
                    int key = (p.x == 0 ? 0 : 1) + (p.z == 0 ? 0 : 2);
                    keys[corner] = key + plane * 4;
                    cornerMask |= 1 << key;
                }
                Vector3 cross = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
                float areaTwice = Vector3.Dot(cross, plane == 0 ? Vector3.down : Vector3.up);
                Assert.AreEqual(Cells * Cells, areaTwice, 1e-6f, "Merged triangle area/winding changed.");
                System.Array.Sort(keys);
                Assert.True(triangles.Add((keys[0], keys[1], keys[2])), "Duplicate boundary triangle.");
                if (masks[plane] != 0)
                {
                    Assert.AreEqual(15, masks[plane] | cornerMask, "Complementary triangles must cover all four corners.");
                    Assert.AreNotEqual(masks[plane], cornerMask);
                    int shared = masks[plane] & cornerMask;
                    Assert.That(shared, Is.EqualTo(9).Or.EqualTo(6), "Triangles must share a diagonal, not an outer edge.");
                }
                masks[plane] = cornerMask;
                areas[plane] += areaTwice * 0.5f;
            }
            Assert.AreEqual(4, triangles.Count);
            Assert.AreEqual(Cells * Cells, areas[0]);
            Assert.AreEqual(Cells * Cells, areas[1]);
        }

        [TestCase(0, 4)] // two material stripes on each boundary plane
        [TestCase(1, 12)] // through-hole: four rectangles per plane plus four interior walls
        [TestCase(2, 128)] // checkerboard prevents all top/bottom face merging
        public void MergedFacesPreserveEveryMaterialBoundaryAndHole(int pattern, int expectedQuads)
        {
            using var mirror = new GpuVoxelBrickMirror(8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, Cells, 2);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, null);
            PublishRepeatedHalfBrick(mirror, extractor, SurfaceStyles.Cubic, 0,
                out var voxels, out var semantics, out var boundaries);
            using var vertices = new ComputeBuffer(Capacity, GpuSurfaceExtractor.ReadbackVertex.Stride);
            using var indices = new ComputeBuffer(Capacity, sizeof(uint));
            try
            {
                for (int z = 0; z < 8; z++)
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++) voxels[x + 8 * (y + 8 * z)] = PatternMaterial(new int3(x,y,z), pattern);
                Assert.AreEqual(GpuBrickPublish.Uploaded, mirror.Publish(VoxelBrickDelta.MixedAt(int3.zero, 2, 0),
                    voxels, semantics, boundaries, 0, true));
                // Republish may choose a new slot; use the actual current GPU mirror identity.
                Assert.True(mirror.TryGetSlot(int3.zero, out int slot));
                uint entry = GpuSurfaceExtractor.PackBrickCacheEntry(VoxelBrickContent.Mixed, 0, slot);
                for (int z = 0; z < extractor.BrickCacheEdge; z++)
                for (int y = 0; y < extractor.BrickCacheEdge; y++)
                for (int x = 0; x < extractor.BrickCacheEdge; x++)
                    extractor.SetBrickCacheEntry(new int3(x,y,z), entry);
                var request = new GpuChunkExtraction(new int3(-8), new int3(-2), 1, 1f);
                var counts = extractor.Count(mirror, tables, request);
                Assert.False(counts.Unsupported);
                Assert.AreEqual(expectedQuads * 4, counts.VertexCount);
                Assert.AreEqual(expectedQuads * 6, counts.IndexCount);
                var result = extractor.WriteRange(mirror, tables, request, vertices, indices,
                    0, counts.VertexCount, 0, counts.IndexCount);
                Assert.False(result.Overflowed);
                Assert.AreEqual(counts.VertexCount, result.VertexCount);
                Assert.AreEqual(counts.IndexCount, result.IndexCount);
                var output = new GpuSurfaceExtractor.ReadbackVertex[result.VertexCount];
                var outputIndices = new uint[result.IndexCount];
                vertices.GetData(output); indices.GetData(outputIndices);
                var expected = new HashSet<(int axis, int sign, int plane, int a, int b, byte material)>();
                for (int z = -8; z < 0; z++)
                for (int y = -8; y < 0; y++)
                for (int x = -8; x < 0; x++)
                {
                    int3 cell = new(x,y,z); byte material = PatternMaterial(cell, pattern);
                    if (material == 0) continue;
                    for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        int3 neighbour = cell; neighbour[axis] += sign;
                        if (PatternMaterial(neighbour, pattern) != 0) continue;
                        expected.Add((axis, sign, cell[axis] + (sign > 0 ? 1 : 0),
                            cell[(axis + 1) % 3], cell[(axis + 2) % 3], material));
                    }
                }
                for (int v = 0; v < output.Length; v += 4)
                {
                    Vector3 normal = output[v].Normal;
                    int axis = normal.x != 0 ? 0 : normal.y != 0 ? 1 : 2;
                    int sign = (int)normal[axis], aa = (axis + 1) % 3, bb = (axis + 2) % 3;
                    Assert.That(sign, Is.EqualTo(-1).Or.EqualTo(1));
                    Vector3 min = output[v].Position, max = min;
                    for (int c = 0; c < 4; c++)
                    {
                        Assert.AreEqual(normal, output[v+c].Normal);
                        Assert.AreEqual(output[v].Material, output[v+c].Material);
                        min = Vector3.Min(min, output[v+c].Position); max = Vector3.Max(max, output[v+c].Position);
                    }
                    Assert.AreEqual(min[axis], max[axis]);
                    Assert.AreEqual(Mathf.Round(min[axis]), min[axis]);
                    for (int b = (int)min[bb]; b < (int)max[bb]; b++)
                    for (int a = (int)min[aa]; a < (int)max[aa]; a++)
                        Assert.True(expected.Remove((axis,sign,(int)min[axis],a,b,(byte)(output[v].Material&255))),
                            "Merged face crossed a hole/material boundary or duplicated coverage.");
                }
                Assert.That(expected, Is.Empty, "A canonical occupied boundary face was omitted.");
                float area = 0;
                for (int i = 0; i < outputIndices.Length; i += 3)
                {
                    var a = output[outputIndices[i]]; var b = output[outputIndices[i+1]]; var c = output[outputIndices[i+2]];
                    float signedArea = Vector3.Dot(Vector3.Cross(b.Position-a.Position,c.Position-a.Position),a.Normal)*0.5f;
                    Assert.That(signedArea, Is.GreaterThan(0)); area += signedArea;
                }
                Assert.AreEqual(pattern == 1 ? 142f : 128f, area, "Indexed area must equal the exact occupied boundary.");
            }
            finally { voxels.Dispose(); semantics.Dispose(); boundaries.Dispose(); }
        }

        private static byte PatternMaterial(int3 cell, int pattern)
        {
            int x = ((cell.x % 8) + 8) % 8, y = ((cell.y % 8) + 8) % 8, z = ((cell.z % 8) + 8) % 8;
            if (y >= 4 || pattern == 1 && x == 2 && z == 2) return 0;
            return (byte)(pattern == 0 ? (x < 4 ? 1 : 2) : pattern == 2 ? 1 + ((x+z)&1) : 1);
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
