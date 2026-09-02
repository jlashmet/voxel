using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuStandaloneDensitySamplerProbeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuStandaloneDensitySamplerProbe.compute";

        [Test]
        public void ExactProductionSamplingKernelShapePreservesPlanarCentreOccupancyWhenCompiledAlone()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the standalone sampler cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {ShaderPath}");
            int kernel = shader.FindKernel("CSSampleDensity");

            const int brickEdge = 4;
            var cache = new uint[brickEdge * brickEdge * brickEdge];
            for (int z = 0; z < brickEdge; z++)
            for (int y = 0; y < brickEdge; y++)
            for (int x = 0; x < brickEdge; x++)
                cache[x + brickEdge * (y + brickEdge * z)] = y < 2 ? 1u | (1u << 8) : 0u;

            using var brickCache = Structured(cache);
            using var brickMaterials = Structured(new uint[1]);
            using var brickSurfaces = Structured(new uint[1]);
            using var brickBoundaries = Structured(new uint[1]);
            var styleWords = new uint[32];
            styleWords[2] = 1u; // Planar reconstruction.
            using var styles = Structured(styleWords);
            using var joins = Structured(new uint[16 * 16]);
            using var coatings = Structured(new uint[32 * 3]);
            var defaults = new uint[256];
            defaults[1] = 2u;
            using var materialDefaults = Structured(defaults);

            const int grid = 13;
            int samples = grid * grid * grid;
            using var density = new ComputeBuffer(samples, sizeof(float), ComputeBufferType.Structured);
            using var materials = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            using var surfaces = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            using var boundaries = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);

            shader.SetInt("_SolidWaterMaterialMask", 0);
            shader.SetBuffer(kernel, "_BrickCache", brickCache);
            shader.SetBuffer(kernel, "_BrickMaterials", brickMaterials);
            shader.SetBuffer(kernel, "_BrickSurfaceSemantics", brickSurfaces);
            shader.SetBuffer(kernel, "_BrickBoundarySamples", brickBoundaries);
            shader.SetBuffer(kernel, "_StyleWords", styles);
            shader.SetBuffer(kernel, "_JoinWords", joins);
            shader.SetBuffer(kernel, "_CoatingWords", coatings);
            shader.SetBuffer(kernel, "_MaterialDefaultStyle", materialDefaults);
            shader.SetBuffer(kernel, "_DensityWrite", density);
            shader.SetBuffer(kernel, "_SampleMaterialWrite", materials);
            shader.SetBuffer(kernel, "_SampleSurfaceWrite", surfaces);
            shader.SetBuffer(kernel, "_SampleBoundaryWrite", boundaries);
            shader.SetInts("_ChunkOriginVoxel", 0, 0, 0);
            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", brickEdge);
            shader.SetInt("_GridSize", grid);
            shader.SetInt("_Padding", 2);
            shader.SetInt("_SourceStep", 1);

            shader.Dispatch(kernel, (samples + 63) / 64, 1, 1);

            var densityValues = new float[samples];
            var materialValues = new uint[samples];
            var surfaceValues = new uint[samples];
            var boundaryValues = new uint[samples];
            density.GetData(densityValues);
            materials.GetData(materialValues);
            surfaces.GetData(surfaceValues);
            boundaries.GetData(boundaryValues);

            Assert.AreEqual(0.5f, densityValues[0], 1e-4f,
                "Exact CSSampleDensity expression/dispatch shape must preserve the solid Planar centre when compiled outside the full mesher.");
            Assert.AreEqual(1u, materialValues[0]);
            Assert.AreEqual(2u, surfaceValues[0] & 0xFFFFu);
            Assert.AreEqual(1u, (surfaceValues[0] >> 26) & 1u,
                $"Standalone sampler lost authoritative occupancy; surface=0x{surfaceValues[0]:X8}.");
            Assert.AreEqual(0u, boundaryValues[0]);
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }
    }
}
