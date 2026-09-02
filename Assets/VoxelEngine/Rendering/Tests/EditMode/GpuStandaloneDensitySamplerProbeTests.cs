using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuStandaloneDensitySamplerProbeTests
    {
        private const string DenseShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuStandaloneDensitySamplerProbe.compute";
        private const string PersistentShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuStandalonePersistentDensitySamplerProbe.compute";
        private const uint PersistentLookupMagic = 0x47505540u;
        private const uint DirectoryOccupied = 1u;
        private const int DirectoryWordsPerEntry = 5;

        [Test]
        public void ExactProductionSamplingKernelShapePreservesPlanarCentreOccupancyWhenCompiledAlone()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the standalone sampler cannot run.");

            var result = Run(DenseShaderPath, persistent: false);
            Assert.AreEqual(0.5f, result.density, 1e-4f,
                "Exact CSSampleDensity expression/dispatch shape must preserve the solid Planar centre when compiled outside the full mesher.");
            Assert.AreEqual(1u, result.material);
            Assert.AreEqual(2u, result.surface & 0xFFFFu);
            Assert.AreEqual(1u, (result.surface >> 26) & 1u,
                $"Standalone sampler lost authoritative occupancy; surface=0x{result.surface:X8}.");
            Assert.AreEqual(0u, result.boundary);
        }

        [Test]
        public void ForcedPersistentLookupMatchesDenseSamplerForSamePlanarWorld()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the standalone sampler cannot run.");

            var dense = Run(DenseShaderPath, persistent: false);
            var persistent = Run(PersistentShaderPath, persistent: true);

            Assert.AreEqual(dense.material, persistent.material,
                $"Persistent lookup changed centre material; density={persistent.density:F5}, surface=0x{persistent.surface:X8}.");
            Assert.AreEqual(dense.density, persistent.density, 1e-4f,
                $"Persistent lookup changed Planar centre occupancy; material={persistent.material}, surface=0x{persistent.surface:X8}.");
            Assert.AreEqual(dense.surface, persistent.surface,
                $"Persistent lookup changed centre surface semantics; material={persistent.material}, density={persistent.density:F5}.");
            Assert.AreEqual(dense.boundary, persistent.boundary);
        }

        private static (float density, uint material, uint surface, uint boundary) Run(
            string shaderPath, bool persistent)
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderPath);
            Assert.NotNull(shader, $"Probe shader missing at {shaderPath}");
            int kernel = shader.FindKernel("CSSampleDensity");

            const int brickEdge = 4;
            uint[] cache;
            uint[] brickMaterialWords;
            if (persistent)
            {
                const int directoryCapacity = 128;
                const int directoryMask = directoryCapacity - 1;
                const int directoryWordOffset = 8;
                cache = new uint[brickEdge * brickEdge * brickEdge];
                cache[0] = PersistentLookupMagic;
                cache[1] = (uint)directoryWordOffset << 2;
                cache[2] = (uint)directoryMask << 2;
                brickMaterialWords = new uint[directoryWordOffset
                                            + directoryCapacity * DirectoryWordsPerEntry];
                for (int z = -1; z <= 2; z++)
                for (int y = -1; y <= 2; y++)
                for (int x = -1; x <= 2; x++)
                {
                    uint entry = y < 1 ? 1u | (1u << 8) : 0u;
                    InsertDirectory(brickMaterialWords, directoryWordOffset, directoryMask,
                                    x, y, z, entry);
                }
            }
            else
            {
                cache = new uint[brickEdge * brickEdge * brickEdge];
                for (int z = 0; z < brickEdge; z++)
                for (int y = 0; y < brickEdge; y++)
                for (int x = 0; x < brickEdge; x++)
                    cache[x + brickEdge * (y + brickEdge * z)] =
                        y < 2 ? 1u | (1u << 8) : 0u;
                brickMaterialWords = new uint[1];
            }

            using var brickCache = Structured(cache);
            using var brickMaterials = Structured(brickMaterialWords);
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
            return (densityValues[0], materialValues[0], surfaceValues[0], boundaryValues[0]);
        }

        private static void InsertDirectory(uint[] words, int wordOffset, int mask,
                                            int x, int y, int z, uint entry)
        {
            uint start = HashBrickCoordinate(x, y, z) & (uint)mask;
            for (uint probe = 0; probe <= (uint)mask; probe++)
            {
                uint slot = (start + probe) & (uint)mask;
                int word = wordOffset + (int)slot * DirectoryWordsPerEntry;
                if (words[word + 4] != 0u) continue;
                words[word + 0] = unchecked((uint)x);
                words[word + 1] = unchecked((uint)y);
                words[word + 2] = unchecked((uint)z);
                words[word + 3] = entry;
                words[word + 4] = DirectoryOccupied;
                return;
            }
            Assert.Fail("Synthetic persistent directory unexpectedly filled.");
        }

        private static uint HashBrickCoordinate(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u;
                h ^= (uint)y * 0xd8163841u;
                h ^= (uint)z * 0xcb1ab31fu;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                return h;
            }
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }
    }
}
