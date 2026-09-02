using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Isolates the production CSSampleDensity resource binding. The kernel does not consume the
    /// sampled-field SRV aliases while it is producing them, so binding the same buffers as SRV and
    /// UAV is unnecessary. This probe compares that current extractor binding with write-only output
    /// binding while keeping the production shader, density helpers, cache and catalogue identical.
    /// </summary>
    public sealed class GpuSampleBufferAliasingProbeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;
        private const int GridSize = CellsPerAxis + Padding * 2 + 1;
        private const int BrickEdge = 4;
        private const int SampleCount = GridSize * GridSize * GridSize;

        [Test]
        public void SampleKernelDoesNotAliasItsOutputsAsReadOnlyInputs()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the sample binding probe cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");
            int kernel = shader.FindKernel("CSSampleDensity");

            using var brickCache = Structured(BuildBrickCache());
            using var brickMaterials = Structured(new uint[1]);
            using var brickSurfaces = Structured(new uint[1]);
            using var brickBoundaries = Structured(new uint[1]);
            using var styles = Structured(BuildStyles());
            using var joins = Structured(new uint[16 * 16]);
            using var coatings = Structured(new uint[32 * 3]);
            using var defaults = Structured(BuildDefaults());

            Sample firstWriteOnly = Dispatch(shader, kernel, brickCache, brickMaterials,
                brickSurfaces, brickBoundaries, styles, joins, coatings, defaults, aliasReadBuffers: false);
            Sample firstAliased = Dispatch(shader, kernel, brickCache, brickMaterials,
                brickSurfaces, brickBoundaries, styles, joins, coatings, defaults, aliasReadBuffers: true);

            Assert.AreEqual(0.5f, firstWriteOnly.Density, 1e-4f,
                $"Write-only binding must preserve the solid Planar centre: {firstWriteOnly}.");
            Assert.AreEqual(1u, firstWriteOnly.Material, $"Write-only material: {firstWriteOnly}.");
            Assert.AreEqual(1u, (firstWriteOnly.Surface >> 26) & 1u,
                $"Write-only authoritative occupancy: {firstWriteOnly}.");

            Assert.AreEqual(firstWriteOnly.Density, firstAliased.Density, 1e-4f,
                "Binding sampled-field outputs simultaneously as SRV and UAV changes CSSampleDensity. "
              + $"writeOnly=[{firstWriteOnly}] aliased=[{firstAliased}].");
            Assert.AreEqual(firstWriteOnly.Material, firstAliased.Material,
                $"writeOnly=[{firstWriteOnly}] aliased=[{firstAliased}]");
            Assert.AreEqual(firstWriteOnly.Surface, firstAliased.Surface,
                $"writeOnly=[{firstWriteOnly}] aliased=[{firstAliased}]");
            Assert.AreEqual(firstWriteOnly.Boundary, firstAliased.Boundary,
                $"writeOnly=[{firstWriteOnly}] aliased=[{firstAliased}]");
        }

        private readonly struct Sample
        {
            public readonly float Density;
            public readonly uint Material;
            public readonly uint Surface;
            public readonly uint Boundary;
            public Sample(float density, uint material, uint surface, uint boundary)
            {
                Density = density;
                Material = material;
                Surface = surface;
                Boundary = boundary;
            }
            public override string ToString() =>
                $"density={Density:F5} material={Material} surface=0x{Surface:X8} boundary={Boundary}";
        }

        private static Sample Dispatch(ComputeShader shader, int kernel,
                                       ComputeBuffer brickCache, ComputeBuffer brickMaterials,
                                       ComputeBuffer brickSurfaces, ComputeBuffer brickBoundaries,
                                       ComputeBuffer styles, ComputeBuffer joins,
                                       ComputeBuffer coatings, ComputeBuffer defaults,
                                       bool aliasReadBuffers)
        {
            using var density = new ComputeBuffer(SampleCount, sizeof(float), ComputeBufferType.Structured);
            using var materials = new ComputeBuffer(SampleCount, sizeof(uint), ComputeBufferType.Structured);
            using var surfaces = new ComputeBuffer(SampleCount, sizeof(uint), ComputeBufferType.Structured);
            using var boundaries = new ComputeBuffer(SampleCount, sizeof(uint), ComputeBufferType.Structured);

            shader.SetInts("_ChunkOriginVoxel", 0, 0, 0);
            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", BrickEdge);
            shader.SetInt("_CellsPerAxis", CellsPerAxis);
            shader.SetInt("_GridSize", GridSize);
            shader.SetInt("_Padding", Padding);
            shader.SetInt("_SourceStep", 1);
            shader.SetFloat("_VoxelSize", 0.1f);
            shader.SetInt("_SolidWaterMaterialMask", 0);

            shader.SetBuffer(kernel, "_BrickCache", brickCache);
            shader.SetBuffer(kernel, "_BrickMaterials", brickMaterials);
            shader.SetBuffer(kernel, "_BrickSurfaceSemantics", brickSurfaces);
            shader.SetBuffer(kernel, "_BrickBoundarySamples", brickBoundaries);
            shader.SetBuffer(kernel, "_StyleWords", styles);
            shader.SetBuffer(kernel, "_JoinWords", joins);
            shader.SetBuffer(kernel, "_CoatingWords", coatings);
            shader.SetBuffer(kernel, "_MaterialDefaultStyle", defaults);
            shader.SetBuffer(kernel, "_DensityWrite", density);
            shader.SetBuffer(kernel, "_SampleMaterialWrite", materials);
            shader.SetBuffer(kernel, "_SampleSurfaceWrite", surfaces);
            shader.SetBuffer(kernel, "_SampleBoundaryWrite", boundaries);

            if (aliasReadBuffers)
            {
                shader.SetBuffer(kernel, "_Density", density);
                shader.SetBuffer(kernel, "_SampleMaterial", materials);
                shader.SetBuffer(kernel, "_SampleSurface", surfaces);
                shader.SetBuffer(kernel, "_SampleBoundary", boundaries);
            }

            shader.Dispatch(kernel, (SampleCount + 63) / 64, 1, 1);

            var d = new float[SampleCount];
            var m = new uint[SampleCount];
            var s = new uint[SampleCount];
            var b = new uint[SampleCount];
            density.GetData(d);
            materials.GetData(m);
            surfaces.GetData(s);
            boundaries.GetData(b);
            return new Sample(d[0], m[0], s[0], b[0]);
        }

        private static uint[] BuildBrickCache()
        {
            var cache = new uint[BrickEdge * BrickEdge * BrickEdge];
            for (int z = 0; z < BrickEdge; z++)
            for (int y = 0; y < BrickEdge; y++)
            for (int x = 0; x < BrickEdge; x++)
            {
                bool solid = y < 2;
                int index = x + BrickEdge * (y + BrickEdge * z);
                cache[index] = VoxelEngine.Rendering.Runtime.GpuVoxel.GpuSurfaceExtractor.PackBrickCacheEntry(
                    solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                    solid ? (byte)1 : (byte)0, -1);
            }
            return cache;
        }

        private static uint[] BuildStyles()
        {
            var words = new uint[32];
            words[1] = 255u << 8; // Smooth with full curvature.
            words[2] = 1u;        // Planar reconstruction.
            return words;
        }

        private static uint[] BuildDefaults()
        {
            var defaults = new uint[256];
            defaults[1] = 2u;
            return defaults;
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }
    }
}
