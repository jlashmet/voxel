using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuCoatingDisplacementParityTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        [TestCase(1)]
        [TestCase(2)]
        public void SnowDisplacementMatchesCpuAndActuallyMovesTheDensityField(int sourceStep)
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Compute shaders unavailable.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            const int perBrick = 8 * 8 * 8;
            var voxels = new byte[perBrick];
            var coatedSemantics = new ushort[perBrick];
            var plainSemantics = new ushort[perBrick];
            var boundaries = new byte[perBrick];
            ushort coated = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.Smooth,
                CoatingId = Coatings.Snow,
            }.PackedStorage;
            ushort plain = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.Smooth,
            }.PackedStorage;

            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                voxels[i] = (byte)(y < 4 ? 1 : 0);
                coatedSemantics[i] = coated;
                plainSemantics[i] = plain;
            }

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(shader, CellsPerAxis, Padding);
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = CoatingCatalogueView.CreateBuiltIns();
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < defaultStyles.Length; i++)
                defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            using var nativeVoxels = new NativeArray<byte>(voxels, Allocator.Temp);
            using var nativeSemantics = new NativeArray<ushort>(coatedSemantics, Allocator.Temp);
            using var nativeBoundaries = new NativeArray<byte>(boundaries, Allocator.Temp);
            Assert.AreEqual(GpuBrickPublish.Uploaded,
                mirror.Publish(VoxelBrickDelta.MixedAt(int3.zero, 1, 0),
                               nativeVoxels, nativeSemantics, nativeBoundaries, 0, true));
            Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));

            int edge = extractor.BrickCacheEdge;
            int3 brickCacheOrigin = new(-1, -1, -1);
            var kinds = new byte[edge * edge * edge];
            var uniforms = new byte[kinds.Length];
            extractor.ClearBrickCache();
            uint mixedEntry = GpuSurfaceExtractor.PackBrickCacheEntry(
                VoxelBrickContent.Mixed, 0, slot);
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

            extractor.Extract(mirror, tables, int3.zero, brickCacheOrigin,
                              sourceStep, 0.1f, vertices, indices, capacity, capacity);
            int sampleCount = extractor.GridSize * extractor.GridSize * extractor.GridSize;
            var gpuDensity = new float[sampleCount];
            extractor.ReadDensity(gpuDensity);

            CpuDensityFieldSnapshot cpuCoated = CpuDensityOracle.SampleMixedNeighbourhood(
                int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding, sourceStep,
                kinds, uniforms, voxels, coatedSemantics, boundaries,
                surfaces, coatings, palette);
            CpuDensityFieldSnapshot cpuPlain = CpuDensityOracle.SampleMixedNeighbourhood(
                int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding, sourceStep,
                kinds, uniforms, voxels, plainSemantics, boundaries,
                surfaces, coatings, palette);

            int gpuMismatch = 0;
            int displacedSamples = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                if (Mathf.Abs(cpuCoated.Density[i] - gpuDensity[i]) > 1e-4f)
                    gpuMismatch++;
                if (Mathf.Abs(cpuCoated.Density[i] - cpuPlain.Density[i]) > 1e-4f)
                    displacedSamples++;
            }

            Assert.Greater(displacedSamples, 0,
                "The Snow fixture must actually change the CPU density field; otherwise this "
              + "regression would not exercise coating displacement.");
            Assert.AreEqual(0, gpuMismatch,
                $"SourceStep {sourceStep}: GPU Snow displacement diverged from the CPU at "
              + $"{gpuMismatch} of {sampleCount} samples.");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void WetPresentationMetadataDoesNotMoveTheDensityField(int sourceStep)
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Compute shaders unavailable.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            const int perBrick = 8 * 8 * 8;
            var voxels = new byte[perBrick];
            var coatedSemantics = new ushort[perBrick];
            var plainSemantics = new ushort[perBrick];
            var boundaries = new byte[perBrick];
            ushort coated = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.Smooth,
                CoatingId = Coatings.Wet,
            }.PackedStorage;
            ushort plain = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.Smooth,
            }.PackedStorage;

            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                voxels[i] = (byte)(y < 4 ? 1 : 0);
                coatedSemantics[i] = coated;
                plainSemantics[i] = plain;
            }

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(shader, CellsPerAxis, Padding);
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = CoatingCatalogueView.CreateBuiltIns();
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < defaultStyles.Length; i++)
                defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            using var nativeVoxels = new NativeArray<byte>(voxels, Allocator.Temp);
            using var nativeSemantics = new NativeArray<ushort>(coatedSemantics, Allocator.Temp);
            using var nativeBoundaries = new NativeArray<byte>(boundaries, Allocator.Temp);
            Assert.AreEqual(GpuBrickPublish.Uploaded,
                mirror.Publish(VoxelBrickDelta.MixedAt(int3.zero, 1, 0),
                               nativeVoxels, nativeSemantics, nativeBoundaries, 0, true));
            Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));

            int edge = extractor.BrickCacheEdge;
            int3 brickCacheOrigin = new(-1, -1, -1);
            var kinds = new byte[edge * edge * edge];
            var uniforms = new byte[kinds.Length];
            extractor.ClearBrickCache();
            uint mixedEntry = GpuSurfaceExtractor.PackBrickCacheEntry(
                VoxelBrickContent.Mixed, 0, slot);
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
                mirror, tables, int3.zero, brickCacheOrigin,
                sourceStep, 0.1f, vertices, indices, capacity, capacity);
            Assert.IsFalse(result.Overflowed);
            Assert.Greater(result.IndexCount, 0,
                "The presentation-only coating fixture must cross a real rendered surface.");

            int sampleCount = extractor.GridSize * extractor.GridSize * extractor.GridSize;
            var gpuDensity = new float[sampleCount];
            extractor.ReadDensity(gpuDensity);

            CpuDensityFieldSnapshot cpuCoated = CpuDensityOracle.SampleMixedNeighbourhood(
                int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding, sourceStep,
                kinds, uniforms, voxels, coatedSemantics, boundaries,
                surfaces, coatings, palette);
            CpuDensityFieldSnapshot cpuPlain = CpuDensityOracle.SampleMixedNeighbourhood(
                int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding, sourceStep,
                kinds, uniforms, voxels, plainSemantics, boundaries,
                surfaces, coatings, palette);

            int gpuMismatch = 0;
            int movedSamples = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                if (Mathf.Abs(cpuCoated.Density[i] - gpuDensity[i]) > 1e-4f)
                    gpuMismatch++;
                if (Mathf.Abs(cpuCoated.Density[i] - cpuPlain.Density[i]) > 1e-4f)
                    movedSamples++;
            }

            Assert.AreEqual(0, movedSamples,
                "Wet is presentation-only metadata and has zero displacement; it must not move "
              + "the authoritative density field or the surface derived from it.");
            Assert.AreEqual(0, gpuMismatch,
                $"SourceStep {sourceStep}: GPU presentation-only coating semantics diverged from "
              + $"the CPU at {gpuMismatch} of {sampleCount} samples.");
        }
    }
}
