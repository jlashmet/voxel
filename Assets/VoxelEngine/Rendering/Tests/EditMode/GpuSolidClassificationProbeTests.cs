using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuSolidClassificationProbeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuSolidClassificationProbe.compute";

        [Test]
        public void MaterialOneIsSolidWhenWaterMaskIsZero()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the classifier cannot be exercised.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {ShaderPath}");

            Shader.SetGlobalInt("_SolidWaterMaterialMask", 0);
            int kernel = shader.FindKernel("CSProbe");
            using var output = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);
            output.SetData(new uint[2]);
            shader.SetBuffer(kernel, "_ProbeOutput", output);
            shader.Dispatch(kernel, 1, 1, 1);

            var values = new uint[2];
            output.GetData(values);
            Assert.AreEqual(0u, values[1], "The probe must observe the zero water mask supplied by the test.");
            Assert.AreEqual(1u, values[0],
                "IsSolidSample(1) must be true when material 1 is not classified as water. "
              + "If this minimal include probe passes while the full mesher reports air for the "
              + "same material/mask, the defect is full-shader code generation/control flow rather "
              + "than the classifier expression or global uniform itself.");
        }
    }
}
