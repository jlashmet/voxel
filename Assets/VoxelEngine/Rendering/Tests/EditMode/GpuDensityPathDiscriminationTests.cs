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
    /// Discriminates centre-occupancy failure from the smooth weighted-tap path while keeping the
    /// exact same production compute shader, cache binding, and real CPU density-job oracle.
    /// </summary>
    public sealed class GpuDensityPathDiscriminationTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;
        private const int SourceStep = 1;
        private const byte Material = 1;
        private const int SolidBrickYLimit = 2;

        private struct FixedMaterialCatalogue : IMaterialPresentationCatalogue
        {
            public uint Version => 1;
            public ushort Style;
            public ushort GetDefaultSurfaceStyle(byte materialId) =>
                materialId == Material ? Style : SurfaceStyles.Smooth;
        }

        private readonly struct SampleResult
        {
            public readonly float CpuDensity;
            public readonly float GpuDensity;
            public readonly byte CpuMaterial;
            public readonly uint GpuMaterial;
            public readonly uint CpuSurface;
            public readonly uint GpuSurface;
            public readonly byte CpuBoundary;
            public readonly uint GpuBoundary;

            public SampleResult(float cpuDensity, float gpuDensity,
                                byte cpuMaterial, uint gpuMaterial,
                                uint cpuSurface, uint gpuSurface,
                                byte cpuBoundary, uint gpuBoundary)
            {
                CpuDensity = cpuDensity;
                GpuDensity = gpuDensity;
                CpuMaterial = cpuMaterial;
                GpuMaterial = gpuMaterial;
                CpuSurface = cpuSurface;
                GpuSurface = gpuSurface;
                CpuBoundary = cpuBoundary;
                GpuBoundary = gpuBoundary;
            }

            public bool Matches => Mathf.Abs(CpuDensity - GpuDensity) <= 1e-4f
                                && CpuMaterial == GpuMaterial
                                && CpuSurface == GpuSurface
                                && CpuBoundary == GpuBoundary;

            public override string ToString() =>
                $"density cpu={CpuDensity:F5} gpu={GpuDensity:F5}; "
              + $"material cpu={CpuMaterial} gpu={GpuMaterial}; "
              + $"surface cpu=0x{CpuSurface:X8} gpu=0x{GpuSurface:X8}; "
              + $"boundary cpu={CpuBoundary} gpu={GpuBoundary}";
        }

        [Test]
        public void PlanarEarlyReturnAndSmoothWeightedPathShareCentreOccupancy()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the mesher cannot be exercised.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            SampleResult planar = SampleFirstWorldVoxel(shader, SurfaceStyles.Planar);
            SampleResult smooth = SampleFirstWorldVoxel(shader, SurfaceStyles.Smooth);

            Assert.IsTrue(planar.Matches && smooth.Matches,
                "CPU/GPU first-sample comparison at world (-2,-2,-2). "
              + $"Planar: [{planar}]. Smooth: [{smooth}]. "
              + "Planar uses SampleField's centreSolid early return before AddTap; Smooth uses the "
              + "weighted tap path. Their relative result discriminates centre occupancy from "
              + "smooth-field/tap execution without changing the production shader or bindings.");
        }

        private static SampleResult SampleFirstWorldVoxel(ComputeShader shader, ushort defaultStyle)
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            var source = new FixedMaterialCatalogue { Style = defaultStyle };
            MaterialPaletteView palette = MaterialPaletteView.Capture(in source);
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
                bool solid = y < SolidBrickYLimit;
                int brickIndex = x + brickEdge * (y + brickEdge * z);
                brickKinds[brickIndex] = (byte)(solid ? 1 : 0);
                brickUniformMaterials[brickIndex] = solid ? Material : (byte)0;
                extractor.SetBrickCacheEntry(new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? Material : (byte)0, -1));
            }

            const int capacity = 16384;
            using var vertices = new ComputeBuffer(capacity, sizeof(float) * 6 + sizeof(uint) * 2,
                                                   ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            extractor.Extract(mirror, tables, int3.zero, brickCacheOrigin, SourceStep, 0.1f,
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
                int3.zero, brickCacheOrigin, brickEdge, CellsPerAxis, Padding, SourceStep,
                brickKinds, brickUniformMaterials,
                Array.Empty<byte>(), Array.Empty<ushort>(), Array.Empty<byte>(),
                surfaces, coatings, palette);

            const int first = 0;
            return new SampleResult(cpu.Density[first], gpuDensity[first],
                                    cpu.Materials[first], gpuMaterials[first],
                                    cpu.Surfaces[first], gpuSurfaces[first],
                                    cpu.Boundaries[first], gpuBoundaries[first]);
        }
    }
}
