using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Reproduces the exact CSSampleDensity expression shape after the split-local control proved
    /// that the same coordinate, cache, classifier and SampleField implementation are correct.
    /// </summary>
    public sealed class GpuInlineSampleAssignmentProbeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuFullMesherOccupancyProbe.compute";
        private const int BrickEdge = 4;

        [Test]
        public void InlineUavAssignmentPreservesSampleFieldOutParameters()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the inline-assignment probe cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {ShaderPath}");
            int kernel = shader.FindKernel("CSProbeInlineSampleAssignment");

            using var brickCache = Structured(BuildBrickCache());
            using var brickMaterials = Structured(new uint[1]);
            using var brickSurfaces = Structured(new uint[1]);
            using var brickBoundaries = Structured(new uint[1]);
            using var styles = Structured(BuildStyles());
            using var joins = Structured(new uint[16 * 16]);
            using var coatings = Structured(new uint[32 * 3]);
            using var defaults = Structured(BuildDefaults());
            using var density = new ComputeBuffer(1, sizeof(float), ComputeBufferType.Structured);
            using var material = Structured(new uint[1]);
            using var surface = Structured(new uint[1]);
            using var boundary = Structured(new uint[1]);
            density.SetData(new float[1]);

            shader.SetInts("_ChunkOriginVoxel", 0, 0, 0);
            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", BrickEdge);
            shader.SetInt("_GridSize", 13);
            shader.SetInt("_Padding", 2);
            shader.SetInt("_SourceStep", 1);
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
            shader.SetBuffer(kernel, "_SampleMaterialWrite", material);
            shader.SetBuffer(kernel, "_SampleSurfaceWrite", surface);
            shader.SetBuffer(kernel, "_SampleBoundaryWrite", boundary);

            shader.Dispatch(kernel, 1, 1, 1);

            var densities = new float[1];
            var materials = new uint[1];
            var surfaces = new uint[1];
            var boundaries = new uint[1];
            density.GetData(densities);
            material.GetData(materials);
            surface.GetData(surfaces);
            boundary.GetData(boundaries);
            string result = $"density={densities[0]:F5} material={materials[0]} "
                          + $"surface=0x{surfaces[0]:X8} boundary={boundaries[0]}";

            Assert.AreEqual(0.5f, densities[0], 1e-4f,
                "Inline SampleField-to-UAV assignment changed the proven split-local result: " + result);
            Assert.AreEqual(1u, materials[0], result);
            Assert.AreEqual(2u, surfaces[0] & 0xFFFFu, result);
            Assert.AreEqual(1u, (surfaces[0] >> 26) & 1u, result);
            Assert.AreEqual(0u, boundaries[0], result);
        }

        private static uint[] BuildBrickCache()
        {
            var cache = new uint[BrickEdge * BrickEdge * BrickEdge];
            for (int z = 0; z < BrickEdge; z++)
            for (int y = 0; y < BrickEdge; y++)
            for (int x = 0; x < BrickEdge; x++)
            {
                bool solid = y < 2;
                cache[x + BrickEdge * (y + BrickEdge * z)] = solid ? 1u | (1u << 8) : 0u;
            }
            return cache;
        }

        private static uint[] BuildStyles()
        {
            var words = new uint[32];
            words[1] = 255u << 8;
            words[2] = 1u;
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
