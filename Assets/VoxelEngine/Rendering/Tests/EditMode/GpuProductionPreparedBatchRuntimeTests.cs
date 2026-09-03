using Unity.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Runtime coverage for the production persistent-mirror -> GPU resolver -> dense batch ->
    /// mesher chain. Standalone GPU/CPU oracles intentionally populate the legacy dense cache
    /// directly, so they cannot catch a broken production preparation binding.
    /// </summary>
    public sealed class GpuProductionPreparedBatchRuntimeTests
    {
        private const string MesherShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";
        private const int CellsPerAxis = 8;
        private const int Padding = 2;
        private const int RecordCount = 2;

        [Test]
        public void TwoSeparatedRequestsUseTheirOwnPreparedDenseSlicesForCountAndWrite()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; production GPU batching cannot run.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(MesherShaderPath);
            Assert.NotNull(shader, $"Compute shader missing at {MesherShaderPath}");

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(shader, CellsPerAxis, Padding);
            using var resources = extractor.CreateCountBatchResources(RecordCount);
            using var counters = new ComputeBuffer(
                GpuSurfaceExtractor.BatchHeaderWords
                + RecordCount * GpuSurfaceExtractor.BatchRecordWords,
                sizeof(uint), ComputeBufferType.Structured);

            ConfigureCatalogues(extractor);

            int edge = extractor.BrickCacheEdge;
            int3 solidCacheOrigin = new(-1, -1, -1);
            int3 surfaceCacheOrigin = new(20, -1, 20);

            PublishUniformWindow(mirror, solidCacheOrigin, edge, solidBrickYLimit: edge);
            PublishUniformWindow(mirror, surfaceCacheOrigin, edge, solidBrickYLimit: 2);

            var requests = new[]
            {
                new GpuChunkExtraction(
                    chunkOriginVoxel: int3.zero,
                    brickCacheOrigin: solidCacheOrigin,
                    sourceStep: 1,
                    voxelSize: 0.1f),
                new GpuChunkExtraction(
                    chunkOriginVoxel: new int3(
                        (surfaceCacheOrigin.x + 1) * VoxelReadGrid.BlockEdge,
                        0,
                        (surfaceCacheOrigin.z + 1) * VoxelReadGrid.BlockEdge),
                    brickCacheOrigin: surfaceCacheOrigin,
                    sourceStep: 1,
                    voxelSize: 0.1f),
            };

            extractor.DispatchCountBatch(
                mirror, tables, requests, RecordCount, counters, resources);

            var words = new uint[counters.count];
            counters.GetData(words);
            int first = GpuSurfaceExtractor.BatchHeaderWords;
            int second = first + GpuSurfaceExtractor.BatchRecordWords;

            Assert.AreEqual(0u, words[first + 2]);
            Assert.AreEqual(0u, words[first + 3],
                "The first request is solid throughout its prepared neighbourhood and must emit no surface.");
            Assert.Greater(words[second + 2], 0u,
                "The far-away second request crosses solid-to-air. Zero vertices means the batch "
              + "mesher did not select its own GPU-prepared dense slice.");
            Assert.Greater(words[second + 3], 0u,
                "The far-away second request must emit triangles through the production prepared path.");

            uint expectedVertices = words[second + 2];
            uint expectedIndices = words[second + 3];
            extractor.PrefixCountBatch(
                counters, RecordCount,
                SurfaceGeometryArena.VertexAlignment,
                SurfaceGeometryArena.IndexAlignment);

            const int Capacity = 65536;
            using var vertices = new ComputeBuffer(
                Capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                ComputeBufferType.Structured);
            using var indices = new ComputeBuffer(
                Capacity, sizeof(uint), ComputeBufferType.Structured);

            extractor.DispatchBaseWriteBatch(
                mirror, tables, RecordCount, counters, resources,
                vertices, indices);

            counters.GetData(words);
            Assert.AreEqual(0u, words[first + 8]);
            Assert.AreEqual(0u, words[first + 9]);
            Assert.AreEqual(expectedVertices, words[second + 8],
                "Production write must consume the same prepared slice and emit exactly its counted vertices.");
            Assert.AreEqual(expectedIndices, words[second + 9],
                "Production write must consume the same prepared slice and emit exactly its counted indices.");
        }

        private static void PublishUniformWindow(GpuVoxelBrickMirror mirror, int3 origin, int edge,
                                                 int solidBrickYLimit)
        {
            for (int z = 0; z < edge; z++)
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
            {
                int3 coordinate = origin + new int3(x, y, z);
                VoxelBrickDelta delta = y < solidBrickYLimit
                    ? VoxelBrickDelta.UniformAt(coordinate, generation: 1, material: 1)
                    : VoxelBrickDelta.EmptyAt(coordinate, generation: 1);
                GpuBrickPublish publish = mirror.Publish(
                    delta,
                    default(NativeArray<byte>),
                    default(NativeArray<ushort>),
                    default(NativeArray<byte>),
                    elementOffset: 0,
                    hasPayload: false);
                Assert.AreEqual(GpuBrickPublish.MetadataOnly, publish,
                    $"Failed to publish synthetic persistent mirror entry at {coordinate}.");
            }
        }

        private static void ConfigureCatalogues(GpuSurfaceExtractor extractor)
        {
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < defaultStyles.Length; i++)
                defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(
                SurfaceCatalogueView.CreateBuiltIns(), default, defaultStyles);
        }
    }
}
