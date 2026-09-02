using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Runs ReadMaterial -> IsSolidSample -> SampleField in a kernel compiled with the complete
    /// production VoxelBrickMesher compute source. This is an isolation probe, not a second mesher.
    /// </summary>
    public sealed class GpuFullMesherOccupancyProbeTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuFullMesherOccupancyProbe.compute";

        [Test]
        public void FullMesherContextPreservesDirectOccupancyIntoPlanarSampleField()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the full-mesher probe cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {ShaderPath}");
            int kernel = shader.FindKernel("CSProbeFullMesherOccupancy");

            using var brickCache = Structured(new uint[] { 1u | (1u << 8) }); // Uniform, material 1.
            using var brickMaterials = Structured(new uint[1]);
            using var brickSurfaces = Structured(new uint[1]);
            using var brickBoundaries = Structured(new uint[1]);

            var styleWords = new uint[32];
            styleWords[2] = 1u; // style 2 reconstructs as Planar.
            using var styles = Structured(styleWords);
            using var joins = Structured(new uint[16 * 16]);
            using var coatings = Structured(new uint[32 * 3]);
            var defaults = new uint[256];
            defaults[1] = 2u;
            using var materialDefaults = Structured(defaults);
            using var probeWords = Structured(new uint[9]);
            using var probeFloats = new ComputeBuffer(1, sizeof(float), ComputeBufferType.Structured);
            probeFloats.SetData(new float[1]);

            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", 1);
            shader.SetInt("_SolidWaterMaterialMask", 0);
            shader.SetBuffer(kernel, "_BrickCache", brickCache);
            shader.SetBuffer(kernel, "_BrickMaterials", brickMaterials);
            shader.SetBuffer(kernel, "_BrickSurfaceSemantics", brickSurfaces);
            shader.SetBuffer(kernel, "_BrickBoundarySamples", brickBoundaries);
            shader.SetBuffer(kernel, "_StyleWords", styles);
            shader.SetBuffer(kernel, "_JoinWords", joins);
            shader.SetBuffer(kernel, "_CoatingWords", coatings);
            shader.SetBuffer(kernel, "_MaterialDefaultStyle", materialDefaults);
            shader.SetBuffer(kernel, "_ProbeWords", probeWords);
            shader.SetBuffer(kernel, "_ProbeFloats", probeFloats);

            shader.Dispatch(kernel, 1, 1, 1);

            var words = new uint[9];
            var floats = new float[1];
            probeWords.GetData(words);
            probeFloats.GetData(floats);

            Assert.AreEqual(1u, words[0], "ReadMaterial must return material 1 for the uniform brick.");
            Assert.AreEqual(0u, words[1], "Uniform brick has no authored surface payload.");
            Assert.AreEqual(0u, words[2], "Uniform brick has no authored boundary payload.");
            Assert.AreEqual(1u, words[3],
                $"Direct IsSolidSample(1) failed in full-mesher context; mask=0x{words[8]:X8}.");
            Assert.AreEqual(2u, words[4] & 0xFFFFu,
                "Material-default resolution must select the Planar style before SampleField.");
            Assert.AreEqual(1u, words[5], "Planar SampleField must preserve the dominant material.");
            Assert.AreEqual(2u, words[6] & 0xFFFFu, "Planar SampleField must preserve resolved style.");
            Assert.AreEqual(1u, (words[6] >> 26) & 1u,
                $"Planar SampleField lost authoritative occupancy despite directSolid={words[3]}.");
            Assert.AreEqual(0u, words[7], "Planar SampleField must preserve boundary zero.");
            Assert.AreEqual(0.5f, floats[0], 1e-4f,
                $"Planar SampleField density diverged after directSolid={words[3]}; "
              + $"rawMaterial={words[0]}, resolvedSurface=0x{words[4]:X8}, "
              + $"sampledMaterial={words[5]}, sampledSurface=0x{words[6]:X8}, "
              + $"mask=0x{words[8]:X8}.");
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }
    }
}
