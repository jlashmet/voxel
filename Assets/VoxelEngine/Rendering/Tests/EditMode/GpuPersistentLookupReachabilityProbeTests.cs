using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuPersistentLookupReachabilityProbeTests
    {
        private const string PersistentPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuPersistentLookupReachabilityProbe.compute";
        private const string DensePath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuDenseOnlyReachabilityControl.compute";

        [Test]
        public void PersistentHelperReachabilityDoesNotChangeDensePlanarOccupancy()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device.");

            var persistent = Run(PersistentPath);
            var dense = Run(DensePath);

            Assert.AreEqual(1u, dense.material, "Dense control must read material 1.");
            Assert.AreEqual(0.5f, dense.density, 1e-4f, "Dense control must classify the centre solid.");
            Assert.AreEqual(1u << 26, dense.surface, "Dense control must carry authoritative occupancy.");

            Assert.AreEqual(dense.material, persistent.material,
                $"Reachable persistent helper changed dense material; density={persistent.density:F5}, surface=0x{persistent.surface:X8}.");
            Assert.AreEqual(dense.density, persistent.density, 1e-4f,
                $"Reachable persistent helper changed dense occupancy; material={persistent.material}, surface=0x{persistent.surface:X8}.");
            Assert.AreEqual(dense.surface, persistent.surface,
                $"Reachable persistent helper changed authoritative occupancy; material={persistent.material}, density={persistent.density:F5}.");
        }

        private static (uint material, uint surface, float density) Run(string path)
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            Assert.NotNull(shader, $"Probe shader missing at {path}");
            int kernel = shader.FindKernel("CSProbe");

            const int edge = 4;
            var cache = new uint[edge * edge * edge];
            for (int z = 0; z < edge; z++)
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
                cache[x + edge * (y + edge * z)] = y < 2 ? 1u | (1u << 8) : 0u;

            using var brickCache = new ComputeBuffer(cache.Length, sizeof(uint), ComputeBufferType.Structured);
            brickCache.SetData(cache);
            using var brickMaterials = new ComputeBuffer(8, sizeof(uint), ComputeBufferType.Structured);
            brickMaterials.SetData(new uint[8]);
            using var words = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);
            using var density = new ComputeBuffer(1, sizeof(float), ComputeBufferType.Structured);
            words.SetData(new uint[2]);
            density.SetData(new float[1]);

            shader.SetInt("_SolidWaterMaterialMask", 0);
            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", edge);
            shader.SetBuffer(kernel, "_BrickCache", brickCache);
            if (path == PersistentPath)
                shader.SetBuffer(kernel, "_BrickMaterials", brickMaterials);
            shader.SetBuffer(kernel, "_ProbeWords", words);
            shader.SetBuffer(kernel, "_ProbeDensity", density);
            shader.Dispatch(kernel, 1, 1, 1);

            var outputWords = new uint[2];
            var outputDensity = new float[1];
            words.GetData(outputWords);
            density.GetData(outputDensity);
            return (outputWords[0], outputWords[1], outputDensity[0]);
        }
    }
}
