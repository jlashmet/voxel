using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuPreparedLookupCompilerProbeTests
    {
        private const string DeclarationShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuPreparedLookupDeclarationProbe.compute";
        private const string ReachabilityShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuPreparedLookupReachabilityProbe.compute";

        [Test]
        public void ExtraPreparedSrvsDeclaredButUnusedPreserveDenseSampleField()
        {
            var result = Run(DeclarationShaderPath);
            AssertPlanarCentre(result, "Extra SRV declarations alone changed dense SampleField.");
        }

        [Test]
        public void RuntimeFalsePreparedSrvBranchPreservesDenseSampleField()
        {
            var result = Run(ReachabilityShaderPath);
            AssertPlanarCentre(result,
                "A reachable alternate-resource branch changed dense SampleField even when the branch was false.");
        }

        private static void AssertPlanarCentre(
            (float density, uint material, uint surface, uint boundary) result, string message)
        {
            Assert.AreEqual(0.5f, result.density, 1e-4f,
                $"{message} density={result.density:F5} material={result.material} surface=0x{result.surface:X8} boundary={result.boundary}");
            Assert.AreEqual(1u, result.material, message);
            Assert.AreEqual(2u, result.surface & 0xFFFFu, message);
            Assert.AreEqual(1u, (result.surface >> 26) & 1u, message);
            Assert.AreEqual(0u, result.boundary, message);
        }

        private static (float density, uint material, uint surface, uint boundary) Run(string shaderPath)
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the compiler probe cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {shaderPath}");
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
            styleWords[2] = 1u;
            using var styles = Structured(styleWords);
            using var joins = Structured(new uint[16 * 16]);
            using var coatings = Structured(new uint[32 * 3]);
            var defaults = new uint[256];
            defaults[1] = 2u;
            using var materialDefaults = Structured(defaults);

            // Bind the probe-only resources even for the declaration control so both kernels have
            // the same Unity/Metal resource setup. Request count 0 keeps the reachable branch false.
            using var probeRequests = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.Structured);
            probeRequests.SetData(new[] { new Vector4Int(0, 0, 0, 0) });
            using var probeEntries = Structured(new[] { 1u | (1u << 8) });

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
            shader.SetBuffer(kernel, "_ProbePreparedRequests", probeRequests);
            shader.SetBuffer(kernel, "_ProbePreparedEntries", probeEntries);
            shader.SetInt("_ProbePreparedRequestCount", 0);
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
            return (densityValues[0], materialValues[0], surfaceValues[0], boundaryValues[0]);
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }
    }
}
