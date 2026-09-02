using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Diagnostic companion to the density parity gate. It still compares the production compute
    /// mesher with the real TransvoxelDensityJob; the extra readbacks exist only to make the first
    /// semantic divergence actionable instead of reporting only a flattened sample index.
    /// </summary>
    public sealed class GpuDensityDiagnosticOracleTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        [Test]
        public void FirstDensityDivergenceReportsWorldSampleAndSemanticValues()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the mesher cannot be exercised.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            const int sourceStep = 1;
            const int solidBrickYLimit = 2;
            const byte material = 1;

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

            int3 brickCacheOrigin = new int3(-1, -1, -1);
            int brickEdge = extractor.BrickCacheEdge;
            int brickCount = brickEdge * brickEdge * brickEdge;
            var brickKinds = new byte[brickCount];
            var brickUniformMaterials = new byte[brickCount];

            extractor.ClearBrickCache();
            for (int z = 0; z < brickEdge; z++)
            for (int y = 0; y < brickEdge; y++)
            for (int x = 0; x < brickEdge; x++)
            {
                bool solid = y < solidBrickYLimit;
                int brickIndex = x + brickEdge * (y + brickEdge * z);
                brickKinds[brickIndex] = (byte)(solid ? 1 : 0);
                brickUniformMaterials[brickIndex] = solid ? material : (byte)0;
                extractor.SetBrickCacheEntry(new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? material : (byte)0, -1));
            }

            const int capacity = 16384;
            var vertices = new ComputeBuffer(capacity, sizeof(float) * 6 + sizeof(uint) * 2,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                extractor.Extract(mirror, tables, int3.zero, brickCacheOrigin, sourceStep, 0.1f,
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
                    int3.zero, brickCacheOrigin, brickEdge, CellsPerAxis, Padding, sourceStep,
                    brickKinds, brickUniformMaterials,
                    Array.Empty<byte>(), Array.Empty<ushort>(), Array.Empty<byte>(),
                    surfaces, coatings, palette);

                Assert.AreEqual(cpu.Density.Length, sampleCount);
                int first = -1;
                for (int i = 0; i < sampleCount; i++)
                {
                    if (Mathf.Abs(cpu.Density[i] - gpuDensity[i]) <= 1e-4f) continue;
                    first = i;
                    break;
                }

                if (first < 0) return;

                int grid = extractor.GridSize;
                int gx = first % grid;
                int yz = first / grid;
                int gy = yz % grid;
                int gz = yz / grid;
                int3 world = (new int3(gx, gy, gz) - Padding) * sourceStep;

                Assert.Fail(
                    $"First CPU/GPU density divergence at world sample {world} (index {first}): "
                  + $"density cpu={cpu.Density[first]:F5} gpu={gpuDensity[first]:F5}; "
                  + $"material cpu={cpu.Materials[first]} gpu={gpuMaterials[first]}; "
                  + $"surface cpu=0x{cpu.Surfaces[first]:X8} gpu=0x{gpuSurfaces[first]:X8}; "
                  + $"boundary cpu={cpu.Boundaries[first]} gpu={gpuBoundaries[first]}. "
                  + "Both sides were produced by the real CPU density job and production GPU "
                  + "sampling path over the same neighbourhood.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }
    }
}
