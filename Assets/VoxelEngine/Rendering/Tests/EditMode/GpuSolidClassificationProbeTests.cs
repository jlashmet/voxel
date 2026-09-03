using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuSolidClassificationProbeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuSolidClassificationProbe.compute";

        [TestCase(1u, 0u, 1u, TestName = "OrdinaryLowMaterialIsSolid")]
        [TestCase(2u, 1u << 2, 0u, TestName = "ConfiguredWaterMaterialIsNonSolid")]
        [TestCase(40u, uint.MaxValue, 1u, TestName = "MaterialIdAboveMaskRangeRemainsSolid")]
        public void SharedGpuClassifierMatchesPresentationMaterialSemantics(
            uint material, uint waterMask, uint expectedSolid)
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the classifier cannot be exercised.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {ShaderPath}");

            try
            {
                Shader.SetGlobalInt("_SolidWaterMaterialMask", unchecked((int)waterMask));
                int kernel = shader.FindKernel("CSProbe");
                using var output = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);
                output.SetData(new uint[2]);
                shader.SetInt("_ProbeMaterial", unchecked((int)material));
                shader.SetBuffer(kernel, "_ProbeOutput", output);
                shader.Dispatch(kernel, 1, 1, 1);

                var values = new uint[2];
                output.GetData(values);
                Assert.AreEqual(waterMask, values[1],
                    "The classifier must observe the presentation water mask supplied by composition.");
                Assert.AreEqual(expectedSolid, values[0],
                    $"Material {material} classification diverged from the shared renderer contract "
                  + $"for water mask 0x{waterMask:X8}.");
            }
            finally
            {
                Shader.SetGlobalInt("_SolidWaterMaterialMask", 0);
            }
        }
    }
}
