using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuBoundaryExtrusionAxisParityTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;
        private const int PerBrick = 8 * 8 * 8;
        private const float VoxelSize = 0.1f;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void AuthoredBoundaryExtrusionAxisMatchesCpuGeometry(int extrusionAxis)
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Compute shaders unavailable.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            BuildFixture(extrusionAxis, out byte[] voxels, out ushort[] semantics,
                         out byte[] boundaries);

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

            using var nativeVoxels = new NativeArray<byte>(voxels, Allocator.Temp);
            using var nativeSemantics = new NativeArray<ushort>(semantics, Allocator.Temp);
            using var nativeBoundaries = new NativeArray<byte>(boundaries, Allocator.Temp);
            Assert.AreEqual(GpuBrickPublish.Uploaded,
                mirror.Publish(VoxelBrickDelta.MixedAt(int3.zero, 1, 0),
                               nativeVoxels, nativeSemantics, nativeBoundaries, 0, true));
            Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));

            int edge = extractor.BrickCacheEdge;
            int3 brickCacheOrigin = new(-1, -1, -1);
            var kinds = new byte[edge * edge * edge];
            var uniforms = new byte[kinds.Length];
            uint mixedEntry = GpuSurfaceExtractor.PackBrickCacheEntry(
                VoxelBrickContent.Mixed, 0, slot);
            extractor.ClearBrickCache();
            for (int z = 0; z < edge; z++)
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
            {
                int i = x + edge * (y + edge * z);
                kinds[i] = (byte)VoxelBrickContent.Mixed;
                extractor.SetBrickCacheEntry(new int3(x, y, z), mixedEntry);
            }

            const int capacity = 65536;
            using var vertices = new ComputeBuffer(
                capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(
                capacity, sizeof(uint), ComputeBufferType.Structured);

            GpuExtractionResult result = extractor.Extract(
                mirror, tables, int3.zero, brickCacheOrigin, sourceStep: 1, VoxelSize,
                vertices, indices, capacity, capacity);
            Assert.IsFalse(result.Overflowed);
            Assert.Greater(result.IndexCount, 0,
                "The asymmetric boundary fixture must emit real geometry.");

            var gpuVertices = new GpuSurfaceExtractor.ReadbackVertex[result.VertexCount];
            var gpuIndices = new uint[result.IndexCount];
            vertices.GetData(gpuVertices, 0, 0, result.VertexCount);
            indices.GetData(gpuIndices, 0, 0, result.IndexCount);

            var gpuKeys = new Dictionary<string, int>();
            for (int i = 0; i + 2 < gpuIndices.Length; i += 3)
            {
                string key = new OracleTriangle(
                    (float3)(Vector3)gpuVertices[gpuIndices[i]].Position,
                    (float3)(Vector3)gpuVertices[gpuIndices[i + 1]].Position,
                    (float3)(Vector3)gpuVertices[gpuIndices[i + 2]].Position).Key();
                gpuKeys[key] = gpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
            }

            List<OracleTriangle> cpu = CpuTopologyOracle.MeshNeighbourhood(
                int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding,
                sourceStep: 1, VoxelSize, kinds, uniforms, voxels, semantics, boundaries,
                surfaces, coatings, palette);
            var cpuKeys = new Dictionary<string, int>();
            foreach (OracleTriangle triangle in cpu)
            {
                string key = triangle.Key();
                cpuKeys[key] = cpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            Assert.Greater(cpuKeys.Count, 0,
                "The CPU oracle must emit geometry for the boundary fixture.");

            int missing = 0, extra = 0;
            foreach (KeyValuePair<string, int> pair in cpuKeys)
                if (!gpuKeys.TryGetValue(pair.Key, out int n) || n != pair.Value) missing++;
            foreach (KeyValuePair<string, int> pair in gpuKeys)
                if (!cpuKeys.TryGetValue(pair.Key, out int n) || n != pair.Value) extra++;

            Assert.AreEqual(0, missing + extra,
                $"Extrusion axis {extrusionAxis}: {missing} CPU triangles missing and {extra} "
              + "GPU triangles unexpected. The authored-boundary axis controls which crossing "
              + "edges use the signed boundary offset, so disagreement here produces warped or "
              + "cracked production surfaces.");
        }

        private static void BuildFixture(
            int extrusionAxis, out byte[] voxels, out ushort[] semantics, out byte[] boundaries)
        {
            voxels = new byte[PerBrick];
            semantics = new ushort[PerBrick];
            boundaries = new byte[PerBrick];
            ushort smooth = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.Smooth,
            }.PackedStorage;
            byte authoredBoundary = VoxelBoundarySample.FromSignedQ4(
                6, extrusionAxis: extrusionAxis).Packed;

            // Deliberately asymmetric so X/Y/Z extrusion choices cannot collapse to the same
            // geometry by rotational symmetry. Occupancy changes on edges of every axis.
            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                bool solid = ((x + 2 * y + 3 * z) % 7) < 3;
                voxels[i] = solid ? (byte)1 : (byte)0;
                semantics[i] = smooth;
                boundaries[i] = solid ? authoredBoundary : (byte)0;
            }
        }
    }
}
