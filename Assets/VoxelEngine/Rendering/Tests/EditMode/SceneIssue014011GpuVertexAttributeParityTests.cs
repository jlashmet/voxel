using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Bare-bones reproduction required after SceneIssue 014011's third failed visual attempt.
    /// The existing oracle already proves density, sample semantics and triangle positions match;
    /// this isolates the remaining vertex attributes that can change the rendered appearance.
    /// </summary>
    public sealed class SceneIssue014011GpuVertexAttributeParityTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        private ComputeShader _shader;

        [SetUp]
        public void SetUp()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; GPU vertex parity cannot be tested.");

            _shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(_shader, $"Compute shader missing at {ShaderPath}");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void MixedLodVertexMaterialsAndNormalsMatchCpu(int sourceStep)
        {
            const float voxelSize = 0.1f;
            const int perBrick = 512;

            var voxels = new byte[perBrick];
            var semantics = new ushort[perBrick];
            var boundary = new byte[perBrick];
            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                voxels[i] = (byte)(y < 4 ? 1 : 0);
                semantics[i] = (ushort)(((x + z) & 1) == 0 ? SurfaceStyles.Smooth
                                                           : SurfaceStyles.Rounded);
                boundary[i] = y == 3
                    ? VoxelBoundarySample.FromSignedQ4(6, extrusionAxis: 1).Packed
                    : (byte)0;
            }

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++)
                defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            var nativeVoxels = new Unity.Collections.NativeArray<byte>(
                voxels, Unity.Collections.Allocator.Temp);
            var nativeSemantics = new Unity.Collections.NativeArray<ushort>(
                semantics, Unity.Collections.Allocator.Temp);
            var nativeBoundary = new Unity.Collections.NativeArray<byte>(
                boundary, Unity.Collections.Allocator.Temp);

            const int capacity = 65536;
            var vertices = new ComputeBuffer(capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                var delta = VoxelBrickDelta.MixedAt(int3.zero, 1, 0);
                Assert.AreEqual(GpuBrickPublish.Uploaded,
                    mirror.Publish(delta, nativeVoxels, nativeSemantics, nativeBoundary, 0, true));
                Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));

                int edge = extractor.BrickCacheEdge;
                int3 brickCacheOrigin = new int3(-1, -1, -1);
                var kinds = new byte[edge * edge * edge];
                var uniforms = new byte[edge * edge * edge];
                extractor.ClearBrickCache();
                uint mixedEntry = GpuSurfaceExtractor.PackBrickCacheEntry(
                    VoxelBrickContent.Mixed, 0, slot);
                for (int z = 0; z < edge; z++)
                for (int y = 0; y < edge; y++)
                for (int x = 0; x < edge; x++)
                {
                    extractor.SetBrickCacheEntry(new int3(x, y, z), mixedEntry);
                    kinds[x + edge * (y + edge * z)] = 2;
                }

                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, brickCacheOrigin, sourceStep, voxelSize,
                    vertices, indices, capacity, capacity);
                Assert.IsFalse(result.Overflowed);
                Assert.Greater(result.IndexCount, 0, "The reproduction must emit terrain geometry.");

                var readbackVertices = new GpuSurfaceExtractor.ReadbackVertex[
                    Mathf.Max(1, result.VertexCount)];
                var readbackIndices = new uint[Mathf.Max(1, result.IndexCount)];
                vertices.GetData(readbackVertices, 0, 0, result.VertexCount);
                indices.GetData(readbackIndices, 0, 0, result.IndexCount);

                var gpu = new List<OracleAttributedTriangle>(result.IndexCount / 3);
                for (int i = 0; i + 2 < result.IndexCount; i += 3)
                {
                    gpu.Add(new OracleAttributedTriangle(
                        ToSurfaceVertex(readbackVertices[readbackIndices[i]]),
                        ToSurfaceVertex(readbackVertices[readbackIndices[i + 1]]),
                        ToSurfaceVertex(readbackVertices[readbackIndices[i + 2]])));
                }

                List<OracleAttributedTriangle> cpu = CpuVertexAttributeOracle.MeshNeighbourhood(
                    int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding,
                    sourceStep, voxelSize, kinds, uniforms, voxels, semantics, boundary,
                    surfaces, coatings, palette);

                Assert.Greater(cpu.Count, 0, "The CPU reproduction must emit terrain geometry.");
                AssertMultisetEqual(cpu, gpu, t => t.GeometryKey(),
                    $"sourceStep {sourceStep} triangle geometry");

                int nonFiniteCpu = 0;
                int nonFiniteGpu = 0;
                foreach (OracleAttributedTriangle tri in cpu)
                    if (!tri.HasFiniteNormals) nonFiniteCpu++;
                foreach (OracleAttributedTriangle tri in gpu)
                    if (!tri.HasFiniteNormals) nonFiniteGpu++;
                Assert.AreEqual(nonFiniteCpu, nonFiniteGpu,
                    $"sourceStep {sourceStep}: CPU has {nonFiniteCpu} triangles with non-finite "
                  + $"normals but GPU has {nonFiniteGpu}.");

                AssertMultisetEqual(cpu, gpu,
                    t => t.GeometryKey() + "#M:" + t.MaterialKey(),
                    $"sourceStep {sourceStep} packed material");
                AssertMultisetEqual(cpu, gpu,
                    t => t.GeometryKey() + "#N:" + t.NormalKey(),
                    $"sourceStep {sourceStep} vertex normal (1e-3 quantum)");
            }
            finally
            {
                nativeVoxels.Dispose();
                nativeSemantics.Dispose();
                nativeBoundary.Dispose();
                vertices.Release();
                indices.Release();
            }
        }

        private static SmoothSurfaceVertex ToSurfaceVertex(
            GpuSurfaceExtractor.ReadbackVertex vertex) => new SmoothSurfaceVertex
        {
            Position = vertex.Position,
            Normal = vertex.Normal,
            Material = vertex.Material,
            Active = vertex.Active,
        };

        private static void AssertMultisetEqual(
            IEnumerable<OracleAttributedTriangle> cpu,
            IEnumerable<OracleAttributedTriangle> gpu,
            Func<OracleAttributedTriangle, string> key,
            string label)
        {
            Dictionary<string, int> cpuKeys = Count(cpu, key);
            Dictionary<string, int> gpuKeys = Count(gpu, key);
            int missing = 0;
            int extra = 0;
            string firstMissing = null;
            string firstExtra = null;

            foreach (KeyValuePair<string, int> pair in cpuKeys)
            {
                gpuKeys.TryGetValue(pair.Key, out int gpuCount);
                if (gpuCount == pair.Value) continue;
                missing += Math.Max(0, pair.Value - gpuCount);
                firstMissing ??= pair.Key;
            }
            foreach (KeyValuePair<string, int> pair in gpuKeys)
            {
                cpuKeys.TryGetValue(pair.Key, out int cpuCount);
                if (cpuCount == pair.Value) continue;
                extra += Math.Max(0, pair.Value - cpuCount);
                firstExtra ??= pair.Key;
            }

            Assert.AreEqual(0, missing + extra,
                $"{label}: {missing} CPU entries missing from GPU and {extra} unexpected GPU "
              + $"entries. First missing: {firstMissing ?? "none"}. "
              + $"First extra: {firstExtra ?? "none"}.");
        }

        private static Dictionary<string, int> Count(
            IEnumerable<OracleAttributedTriangle> triangles,
            Func<OracleAttributedTriangle, string> key)
        {
            var counts = new Dictionary<string, int>();
            foreach (OracleAttributedTriangle triangle in triangles)
            {
                string value = key(triangle);
                counts[value] = counts.TryGetValue(value, out int n) ? n + 1 : 1;
            }
            return counts;
        }
    }
}
