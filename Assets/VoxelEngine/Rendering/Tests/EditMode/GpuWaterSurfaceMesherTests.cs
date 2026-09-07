using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuWaterSurfaceMesherTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PagedGpuWaterMatchesCanonicalFacesTopologyAndSpray(int fixture)
        {
            const uint waterMask = (1u << 11) | (1u << 16);
            const float voxelSize = 0.1f;
            const int bricks = 2;
            var snapshots = new byte[bricks * (GpuWaterSurfaceMesher.SnapshotWords * 4)];
            for (int brick = 0; brick < bricks; brick++)
            {
                int start = brick * (GpuWaterSurfaceMesher.SnapshotWords * 4);
                if (fixture == 0) snapshots[start + 7 + 8 * (7 + 8 * 7)] = 11;
                if (fixture == 1)
                    for (int y = 0; y < 8; y++) snapshots[start + 8 * y] = 16;
                if (fixture == 2 || fixture == 3)
                    for (int i = 0; i < 512; i++) snapshots[start + i] = 11;
                if (fixture == 3)
                    for (int i = 512; i < (GpuWaterSurfaceMesher.SnapshotWords * 4); i++)
                        snapshots[start + i] = (byte)(i % 3 == 0 ? 1 : 16);
                if (fixture == 4)
                    for (int i = 0; i < (GpuWaterSurfaceMesher.SnapshotWords * 4); i++)
                        snapshots[start + i] = (byte)((i * 17 + brick) % 5 == 0 ? 11
                            : (i % 7 == 0 ? 16 : (i % 11 == 0 ? 1 : 0)));
            }
            using var origins = new NativeArray<int3>(new[] { new int3(-8), new int3(8, -8, 0) }, Allocator.Temp);
            // Captured from the retired CPU oracle before deletion, after actual GPU parity
            // passed for every fixture (gpu-water-retirement-oracles.xml). Canonical semantic
            // tests independently assert faces, identity, seams and spray; this digest preserves
            // every quantized vertex/normal/material/active value without retaining CPU meshing.
            int[] expectedVertexCounts = { 144, 144, 144, 0, 15172 };
            int[] expectedIndexCounts = { 216, 216, 216, 0, 22758 };
            string[] expectedDigests =
            {
                "4cd279f67c0eedb8fec1d38efdc5132a0b54bda025d3d67baf89f636335af715",
                "d3957e4ef3dd1e54e322f3b3acb75353c4d8b1e39a54d465b82390801f75edae",
                "8bc15ca8b6091891349778c20e713f001dfe7323bcd9660207b38b1afcd7ce3d",
                "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                "fdd1e638422357b0fdd87379751fe435693061a7f3c72865725877f4703b60f7",
            };

            var shader = Object.Instantiate(Resources.Load<ComputeShader>("GpuWaterSurfaceMesher"));
            var arenaShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            try
            {
                Assert.That(shader, Is.Not.Null);
                using var gpuOrigins = new ComputeBuffer(bricks, 16);
                gpuOrigins.SetData(new[] { new int4(origins[0], 0), new int4(origins[1], 0) });
                var packed = new uint[snapshots.Length / 4];
                for (int i = 0; i < snapshots.Length; i++) packed[i / 4] |= (uint)snapshots[i] << ((i % 4) * 8);
                using var materials = new ComputeBuffer(packed.Length, 4);
                materials.SetData(packed);
                using var counters = new ComputeBuffer(21, 4);
                using var descriptors = new ComputeBuffer(1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride);
                using var arena = new GpuSurfacePageArena(arenaShader, 65536, 131072, 2);
                Assert.That(arena.TryAcquireHandle(out int handle), Is.True);
                const ulong generation = 7;
                arena.QueueGeneration(handle, generation); arena.FlushHandleCommands(1);
                descriptors.SetData(new[] { new GpuSurfaceExtractor.BatchChunkDescriptor
                    { Handle = (uint)handle, GenerationLow = (uint)generation } });
                // Separate slices prove that later portions append instead of clearing counts.
                for (int i = 0; i < bricks; i++)
                    GpuWaterSurfaceMesher.Count(shader, gpuOrigins, materials, counters, waterMask, voxelSize, i, 1);
                arena.AllocateBatch(descriptors, counters, 1, 17, 1);
                for (int i = 0; i < bricks; i++)
                    GpuWaterSurfaceMesher.Write(shader, gpuOrigins, materials, counters, arena, waterMask, voxelSize, i, 1);
                arena.PublishBatch(descriptors, counters, 1, 17, 1);
                var words = new uint[21]; counters.GetData(words); // Test-only GPU observation.
                Assert.That(words[14], Is.Zero);
                Assert.That(words[6], Is.EqualTo(expectedVertexCounts[fixture]));
                Assert.That(words[7], Is.EqualTo(expectedIndexCounts[fixture]));
                Assert.That(words[12], Is.EqualTo(words[6]));
                Assert.That(words[13], Is.EqualTo(words[7]));
                var vertices = new SmoothSurfaceVertex[arena.Vertices.count]; arena.Vertices.GetData(vertices);
                var indices = new uint[arena.Indices.count]; arena.Indices.GetData(indices);
                var vertexPages = new uint[arena.VertexPageTable.count]; arena.VertexPageTable.GetData(vertexPages);
                var indexPages = new uint[arena.IndexPageTable.count]; arena.IndexPageTable.GetData(indexPages);
                int bank = handle * 2 + (int)words[18];
                SmoothSurfaceVertex Vertex(uint i) => vertices[vertexPages[bank * GpuSurfacePageArena.MaxVertexPagesPerChunk
                    + i / GpuSurfacePageArena.VertexPageSize] * GpuSurfacePageArena.VertexPageSize + i % GpuSurfacePageArena.VertexPageSize];
                uint Index(uint i) => indices[indexPages[bank * GpuSurfacePageArena.MaxIndexPagesPerChunk
                    + i / GpuSurfacePageArena.IndexPageSize] * GpuSurfacePageArena.IndexPageSize + i % GpuSurfacePageArena.IndexPageSize];
                var actual = new List<string>();
                for (uint i = 0; i < words[6]; i++) actual.Add(Key(Vertex(i)));
                actual.Sort(System.StringComparer.Ordinal);
                using (var digest = System.Security.Cryptography.SHA256.Create())
                {
                    string actualDigest = System.BitConverter.ToString(digest.ComputeHash(
                        System.Text.Encoding.UTF8.GetBytes(string.Join("\n", actual)))).Replace("-", "").ToLowerInvariant();
                    Assert.That(actualDigest, Is.EqualTo(expectedDigests[fixture]));
                }
                for (uint i = 0; i < words[7]; i += 3)
                {
                    uint ia = Index(i), ib = Index(i + 1), ic = Index(i + 2);
                    Assert.That(ia, Is.LessThan(words[6])); Assert.That(ib, Is.LessThan(words[6])); Assert.That(ic, Is.LessThan(words[6]));
                    var a = Vertex(ia); var b = Vertex(ib); var c = Vertex(ic);
                    Assert.That(Vector3.Dot(Vector3.Cross(b.Position - a.Position, c.Position - a.Position), a.Normal),
                        Is.GreaterThanOrEqualTo(-0.000001f));
                }
            }
            finally { Object.DestroyImmediate(shader); Object.DestroyImmediate(arenaShader); }
        }

        private static string Key(SmoothSurfaceVertex v) =>
            $"{Mathf.RoundToInt(v.Position.x * 10000)},{Mathf.RoundToInt(v.Position.y * 10000)},{Mathf.RoundToInt(v.Position.z * 10000)}"
            + $"/{v.Normal.x},{v.Normal.y},{v.Normal.z}/{v.Material:X8}/{v.Active:X8}";
    }
}
