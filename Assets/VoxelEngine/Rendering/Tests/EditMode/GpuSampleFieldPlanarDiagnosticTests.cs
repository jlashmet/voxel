using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuSampleFieldPlanarDiagnosticTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";

        [Test]
        public void UniformPlanarSolidTakesTheCentreEarlyReturnEverywhere()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {ShaderPath}");

            const int cells = 8;
            const int padding = 2;
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(shader, cells, padding);

            var defaults = new uint[256];
            defaults[1] = SurfaceStyles.Planar;
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, defaults);

            uint uniformSolid = GpuSurfaceExtractor.PackBrickCacheEntry(
                VoxelBrickContent.Uniform, 1, -1);
            extractor.ClearBrickCache();
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
                extractor.SetBrickCacheEntry(new int3(x, y, z), uniformSolid);

            const int capacity = 4096;
            using var vertices = new ComputeBuffer(
                capacity, GpuSurfaceExtractor.ReadbackVertex.Stride, ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            extractor.Extract(
                mirror, tables, int3.zero, new int3(-1, -1, -1), 1, 0.1f,
                vertices, indices, capacity, capacity);

            int sampleCount = extractor.GridSize * extractor.GridSize * extractor.GridSize;
            var density = new float[sampleCount];
            var material = new uint[sampleCount];
            var surface = new uint[sampleCount];
            extractor.ReadDensity(density);
            extractor.ReadSampleMaterials(material);
            extractor.ReadSampleSurfaces(surface);

            const uint expectedSurface = (1u << 26) | SurfaceStyles.Planar;
            int mismatch = 0;
            int first = -1;
            for (int i = 0; i < sampleCount; i++)
            {
                if (Mathf.Abs(density[i] - 0.5f) <= 1e-4f
                    && material[i] == 1u
                    && surface[i] == expectedSurface)
                    continue;
                mismatch++;
                if (first < 0) first = i;
            }

            Assert.AreEqual(0, mismatch,
                $"Planar centre-return mismatches={mismatch}; first={first}; "
              + $"density={(first >= 0 ? density[first] : 0.5f):F5}, "
              + $"material={(first >= 0 ? material[first] : 1u)}, "
              + $"surface=0x{(first >= 0 ? surface[first] : expectedSurface):X8}.");
        }
    }
}
