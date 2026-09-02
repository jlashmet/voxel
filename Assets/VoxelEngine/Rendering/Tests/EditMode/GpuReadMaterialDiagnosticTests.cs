using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuReadMaterialDiagnosticTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/GpuDensityReadMaterialDiagnostic.compute";

        [Test]
        public void UniformSolidCacheReadsSolidAtEveryPaddedSample()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            const int edge = 4;
            const int grid = 13;
            const int padding = 2;
            const int sourceStep = 1;
            const int count = grid * grid * grid;
            uint uniformSolid = 1u | (1u << 8); // kind=Uniform, material=1

            var cacheData = new uint[edge * edge * edge];
            for (int i = 0; i < cacheData.Length; i++) cacheData[i] = uniformSolid;

            using var cache = new ComputeBuffer(cacheData.Length, sizeof(uint), ComputeBufferType.Structured);
            using var materials = new ComputeBuffer(8192, sizeof(uint), ComputeBufferType.Structured);
            using var surfaces = new ComputeBuffer(256, sizeof(uint), ComputeBufferType.Structured);
            using var boundaries = new ComputeBuffer(128, sizeof(uint), ComputeBufferType.Structured);
            using var rawOutput = new ComputeBuffer(count, sizeof(uint), ComputeBufferType.Structured);
            using var solidOutput = new ComputeBuffer(count, sizeof(uint), ComputeBufferType.Structured);
            cache.SetData(cacheData);

            int kernel = shader.FindKernel("CSMain");
            shader.SetBuffer(kernel, "_BrickCache", cache);
            shader.SetBuffer(kernel, "_BrickMaterials", materials);
            shader.SetBuffer(kernel, "_BrickSurfaceSemantics", surfaces);
            shader.SetBuffer(kernel, "_BrickBoundarySamples", boundaries);
            shader.SetBuffer(kernel, "_DiagRawMaterial", rawOutput);
            shader.SetBuffer(kernel, "_DiagSolid", solidOutput);
            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", edge);
            shader.SetInts("_DiagChunkOriginVoxel", 0, 0, 0);
            shader.SetInt("_DiagGridSize", grid);
            shader.SetInt("_DiagPadding", padding);
            shader.SetInt("_DiagSourceStep", sourceStep);
            shader.SetInt("_SolidWaterMaterialMask", 0);
            shader.Dispatch(kernel, (count + 63) / 64, 1, 1);

            var raw = new uint[count];
            var solid = new uint[count];
            rawOutput.GetData(raw);
            solidOutput.GetData(solid);

            int rawMismatch = 0;
            int solidMismatch = 0;
            int firstRaw = -1;
            int firstSolid = -1;
            for (int i = 0; i < count; i++)
            {
                if (raw[i] != 1u)
                {
                    rawMismatch++;
                    if (firstRaw < 0) firstRaw = i;
                }
                if (solid[i] != 1u)
                {
                    solidMismatch++;
                    if (firstSolid < 0) firstSolid = i;
                }
            }

            Assert.AreEqual(0, rawMismatch + solidMismatch,
                $"Raw ReadMaterial mismatch={rawMismatch} first={firstRaw} value="
              + $"{(firstRaw >= 0 ? raw[firstRaw] : 1u)}; IsSolidSample mismatch={solidMismatch} "
              + $"first={firstSolid} value={(firstSolid >= 0 ? solid[firstSolid] : 1u)}.");
        }
    }
}
