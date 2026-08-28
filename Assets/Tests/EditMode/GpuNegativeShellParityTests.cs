using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Guards the minimum-face ownership path used by the production GPU cutover. The CPU
    /// topology job owns crossing cells whose origin is one cell outside the chunk; the GPU
    /// mesher must emit the identical negative-shell geometry so those chunks do not need a
    /// CPU-only eligibility fallback.
    /// </summary>
    public sealed class GpuNegativeShellParityTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;
        private const float VoxelSize = 0.1f;
        private const byte Material = 1;

        [TestCase(1)]
        [TestCase(2)]
        public void MinimumFaceWithAirPredecessorMatchesCpuNegativeShell(int sourceStep)
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the GPU ownership path cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < defaultStyles.Length; i++)
                defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            int edge = extractor.BrickCacheEdge;
            int3 brickCacheOrigin = new(-1, -1, -1);
            var kinds = new byte[edge * edge * edge];
            var uniforms = new byte[kinds.Length];

            extractor.ClearBrickCache();
            for (int z = 0; z < edge; z++)
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
            {
                // Cache column zero is the predecessor brick on the chunk's -X side. Keeping it
                // empty while every brick from the chunk onward is solid makes the only surface
                // crossing the owned x=-1 regular-cell shell.
                bool solid = x >= 1;
                int i = x + edge * (y + edge * z);
                kinds[i] = solid ? (byte)1 : (byte)0;
                uniforms[i] = solid ? Material : (byte)0;
                extractor.SetBrickCacheEntry(
                    new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? Material : (byte)0,
                        -1));
            }

            const int capacity = 65536;
            var vertices = new ComputeBuffer(
                capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, brickCacheOrigin,
                    sourceStep, VoxelSize, vertices, indices, capacity, capacity);
                Assert.IsFalse(result.Overflowed);
                Assert.Greater(result.IndexCount, 0,
                    "The GPU emitted no minimum-face geometry. Without the negative ownership "
                  + "shell this is the exact hole the obsolete CPU fallback was hiding.");

                var gpuVertices = new GpuSurfaceExtractor.ReadbackVertex[result.VertexCount];
                var gpuIndices = new uint[result.IndexCount];
                vertices.GetData(gpuVertices, 0, 0, result.VertexCount);
                indices.GetData(gpuIndices, 0, 0, result.IndexCount);

                Dictionary<string, int> gpu = TriangleKeys(gpuVertices, gpuIndices);
                List<OracleTriangle> cpuTriangles = CpuTopologyOracle.MeshNeighbourhood(
                    int3.zero, brickCacheOrigin, edge,
                    CellsPerAxis, Padding, sourceStep, VoxelSize,
                    kinds, uniforms, null, null, null,
                    surfaces, coatings, palette);
                Assert.Greater(cpuTriangles.Count, 0,
                    "The CPU oracle produced no negative-shell geometry, so the fixture proves nothing.");

                var cpu = new Dictionary<string, int>();
                foreach (OracleTriangle triangle in cpuTriangles)
                {
                    string key = triangle.Key();
                    cpu[key] = cpu.TryGetValue(key, out int count) ? count + 1 : 1;
                }

                int missing = 0;
                int extra = 0;
                foreach (KeyValuePair<string, int> pair in cpu)
                    if (!gpu.TryGetValue(pair.Key, out int count) || count != pair.Value) missing++;
                foreach (KeyValuePair<string, int> pair in gpu)
                    if (!cpu.TryGetValue(pair.Key, out int count) || count != pair.Value) extra++;

                Assert.AreEqual(0, missing + extra,
                    $"SourceStep {sourceStep}: GPU negative-shell ownership diverged from CPU; "
                  + $"missing={missing} extra={extra} cpuTriangles={cpuTriangles.Count} "
                  + $"gpuIndices={result.IndexCount}.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }

        private static Dictionary<string, int> TriangleKeys(
            GpuSurfaceExtractor.ReadbackVertex[] vertices, uint[] indices)
        {
            var keys = new Dictionary<string, int>();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                string key = new OracleTriangle(
                    (float3)(Vector3)vertices[indices[i]].Position,
                    (float3)(Vector3)vertices[indices[i + 1]].Position,
                    (float3)(Vector3)vertices[indices[i + 2]].Position).Key();
                keys[key] = keys.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            return keys;
        }
    }
}
