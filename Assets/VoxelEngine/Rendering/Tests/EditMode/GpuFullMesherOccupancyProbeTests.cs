using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Runs ReadMaterial -> IsSolidSample -> SampleField in kernels compiled with the complete
    /// production VoxelBrickMesher compute source. These are isolation probes, not a second mesher.
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

            using var brickCache = Structured(new uint[] { 1u | (1u << 8) });
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
            using var probeWords = Structured(new uint[16]);
            using var probeFloats = FloatBuffer();

            BindCommon(shader, kernel, brickCache, brickMaterials, brickSurfaces, brickBoundaries,
                       styles, joins, coatings, materialDefaults, probeWords, probeFloats);
            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", 1);

            shader.Dispatch(kernel, 1, 1, 1);

            var words = ReadWords(probeWords);
            var floats = ReadFloats(probeFloats);

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

        [Test]
        public void SampleDispatchThreadZeroComputesExpectedWorldCoordinateAndCentreOccupancy()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the dispatch-coordinate probe cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {ShaderPath}");
            int kernel = shader.FindKernel("CSProbeDispatchCoordinate");

            const int brickEdge = 4;
            var cache = new uint[brickEdge * brickEdge * brickEdge];
            for (int z = 0; z < brickEdge; z++)
            for (int y = 0; y < brickEdge; y++)
            for (int x = 0; x < brickEdge; x++)
            {
                bool solid = y < 2;
                cache[x + brickEdge * (y + brickEdge * z)] = solid ? 1u | (1u << 8) : 0u;
            }

            using var brickCache = Structured(cache);
            using var brickMaterials = Structured(new uint[1]);
            using var brickSurfaces = Structured(new uint[1]);
            using var brickBoundaries = Structured(new uint[1]);
            var styleWords = new uint[32];
            styleWords[1] = 255u << 8;
            styleWords[2] = 1u;
            using var styles = Structured(styleWords);
            using var joins = Structured(new uint[16 * 16]);
            using var coatings = Structured(new uint[32 * 3]);
            var defaults = new uint[256];
            defaults[1] = 2u;
            using var materialDefaults = Structured(defaults);
            using var probeWords = Structured(new uint[16]);
            using var probeFloats = FloatBuffer();

            BindCommon(shader, kernel, brickCache, brickMaterials, brickSurfaces, brickBoundaries,
                       styles, joins, coatings, materialDefaults, probeWords, probeFloats);
            shader.SetInts("_ChunkOriginVoxel", 0, 0, 0);
            shader.SetInts("_BrickCacheOrigin", -1, -1, -1);
            shader.SetInt("_BrickCacheEdge", brickEdge);
            shader.SetInt("_GridSize", 13);
            shader.SetInt("_Padding", 2);
            shader.SetInt("_SourceStep", 1);

            shader.Dispatch(kernel, 35, 1, 1);

            var words = ReadWords(probeWords);
            var floats = ReadFloats(probeFloats);
            int px = unchecked((int)words[0]);
            int py = unchecked((int)words[1]);
            int pz = unchecked((int)words[2]);
            int ox = unchecked((int)words[9]);
            int oy = unchecked((int)words[10]);
            int oz = unchecked((int)words[11]);
            int edge = unchecked((int)words[12]);
            int localY = unchecked((int)words[14]);
            int brickIndex = unchecked((int)words[15]);
            string lookup = $"p=({px},{py},{pz}) origin=({ox},{oy},{oz}) edge={edge} "
                          + $"cache0=0x{words[13]:X8} localY={localY} index={brickIndex} "
                          + $"raw={words[3]} solid={words[4]} mask=0x{words[8]:X8} density={floats[0]:F5}";

            Assert.AreEqual(-2, px, $"Thread zero x mismatch; {lookup}.");
            Assert.AreEqual(-2, py, $"Thread zero y mismatch; {lookup}.");
            Assert.AreEqual(-2, pz, $"Thread zero z mismatch; {lookup}.");
            Assert.AreEqual(-1, ox, $"Brick-cache origin x mismatch; {lookup}.");
            Assert.AreEqual(-1, oy, $"Brick-cache origin y mismatch; {lookup}.");
            Assert.AreEqual(-1, oz, $"Brick-cache origin z mismatch; {lookup}.");
            Assert.AreEqual(brickEdge, edge, $"Brick-cache edge mismatch; {lookup}.");
            Assert.AreEqual(0x00000101u, words[13], $"Brick-cache word zero mismatch; {lookup}.");
            Assert.AreEqual(0, localY, $"Expected local brick y zero; {lookup}.");
            Assert.AreEqual(0, brickIndex, $"Expected brick index zero; {lookup}.");
            Assert.AreEqual(1u, words[3], $"Raw centre material must be 1; {lookup}.");
            Assert.AreEqual(1u, words[4], $"Direct centre occupancy must be solid; {lookup}.");
            Assert.AreEqual(1u, words[5], $"SampleField must preserve material 1; {lookup}.");
            Assert.AreEqual(1u, (words[6] >> 26) & 1u,
                $"SampleField lost authoritative occupancy; {lookup}, sampledSurface=0x{words[6]:X8}.");
            Assert.AreEqual(0.5f, floats[0], 1e-4f,
                $"SampleField at computed coordinate diverged; {lookup}, sampledSurface=0x{words[6]:X8}.");
        }

        private static void BindCommon(ComputeShader shader, int kernel,
                                       ComputeBuffer brickCache, ComputeBuffer brickMaterials,
                                       ComputeBuffer brickSurfaces, ComputeBuffer brickBoundaries,
                                       ComputeBuffer styles, ComputeBuffer joins,
                                       ComputeBuffer coatings, ComputeBuffer materialDefaults,
                                       ComputeBuffer probeWords, ComputeBuffer probeFloats)
        {
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
        }

        private static ComputeBuffer FloatBuffer()
        {
            var buffer = new ComputeBuffer(1, sizeof(float), ComputeBufferType.Structured);
            buffer.SetData(new float[1]);
            return buffer;
        }

        private static uint[] ReadWords(ComputeBuffer buffer)
        {
            var values = new uint[16];
            buffer.GetData(values);
            return values;
        }

        private static float[] ReadFloats(ComputeBuffer buffer)
        {
            var values = new float[1];
            buffer.GetData(values);
            return values;
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }
    }
}
