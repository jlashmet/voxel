using System.Text;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuDensitySampleDiagnosticTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        [Test]
        public void ReportFirstDensityStageDivergences()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            const int sourceStep = 1;
            const int solidBrickYLimit = 2;
            const byte material = 1;
            int3 brickCacheOrigin = new(-1, -1, -1);

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
            int brickCount = edge * edge * edge;
            var kinds = new byte[brickCount];
            var uniforms = new byte[brickCount];

            extractor.ClearBrickCache();
            for (int z = 0; z < edge; z++)
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
            {
                bool solid = y < solidBrickYLimit;
                int index = x + edge * (y + edge * z);
                kinds[index] = solid ? (byte)1 : (byte)0;
                uniforms[index] = solid ? material : (byte)0;
                extractor.SetBrickCacheEntry(
                    new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? material : (byte)0,
                        -1));
            }

            const int capacity = 16384;
            using var vertices = new ComputeBuffer(
                capacity, GpuSurfaceExtractor.ReadbackVertex.Stride, ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);

            extractor.Extract(
                mirror, tables, int3.zero, brickCacheOrigin, sourceStep, 0.1f,
                vertices, indices, capacity, capacity);

            int sampleCount = extractor.GridSize * extractor.GridSize * extractor.GridSize;
            var gpuDensity = new float[sampleCount];
            var gpuMaterials = new uint[sampleCount];
            var gpuSurfaces = new uint[sampleCount];
            var gpuBoundaries = new uint[sampleCount];
            extractor.ReadDensity(gpuDensity);
            extractor.ReadSampleMaterials(gpuMaterials);
            extractor.ReadSampleSurfaces(gpuSurfaces);
            extractor.ReadSampleBoundaries(gpuBoundaries);

            CpuDensityFieldSnapshot cpu = CpuDensityOracle.SampleMixedNeighbourhood(
                int3.zero, brickCacheOrigin, edge,
                CellsPerAxis, Padding, sourceStep,
                kinds, uniforms, null, null, null,
                surfaces, coatings, palette);

            int densityMismatch = 0;
            int materialMismatch = 0;
            int surfaceMismatch = 0;
            int boundaryMismatch = 0;
            var report = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                bool d = Mathf.Abs(cpu.Density[i] - gpuDensity[i]) > 1e-4f;
                bool m = cpu.Materials[i] != gpuMaterials[i];
                bool s = cpu.Surfaces[i] != gpuSurfaces[i];
                bool b = cpu.Boundaries[i] != gpuBoundaries[i];
                if (d) densityMismatch++;
                if (m) materialMismatch++;
                if (s) surfaceMismatch++;
                if (b) boundaryMismatch++;
                if (!(d || m || s || b) || shown >= 24) continue;

                int gx = i % extractor.GridSize;
                int yz = i / extractor.GridSize;
                int gy = yz % extractor.GridSize;
                int gz = yz / extractor.GridSize;
                int3 p = (new int3(gx, gy, gz) - Padding) * sourceStep;
                report.Append("\nidx=").Append(i)
                    .Append(" p=").Append(p)
                    .Append(" cpuD=").Append(cpu.Density[i].ToString("F5"))
                    .Append(" gpuD=").Append(gpuDensity[i].ToString("F5"))
                    .Append(" cpuM=").Append(cpu.Materials[i])
                    .Append(" gpuM=").Append(gpuMaterials[i])
                    .Append(" cpuS=0x").Append(cpu.Surfaces[i].ToString("X8"))
                    .Append(" gpuS=0x").Append(gpuSurfaces[i].ToString("X8"))
                    .Append(" cpuB=").Append(cpu.Boundaries[i])
                    .Append(" gpuB=").Append(gpuBoundaries[i]);
                shown++;
            }

            Assert.Fail(
                $"density={densityMismatch}, material={materialMismatch}, surface={surfaceMismatch}, "
              + $"boundary={boundaryMismatch}, grid={extractor.GridSize}, edge={edge}."
              + report);
        }
    }
}
