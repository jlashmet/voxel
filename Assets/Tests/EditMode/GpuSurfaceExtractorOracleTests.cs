using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Does the compute mesher actually run, and does it agree with the CPU?
    ///
    /// The whole case for moving extraction to the GPU rests on the two producing the same surface.
    /// A GPU mesher that differs is not a faster renderer, it is a second renderer, and two
    /// renderers drifting is a look regression this project has had before. So these dispatch the
    /// real kernels against real buffers rather than asserting on the source.
    ///
    /// They need a graphics device, so they are skipped under -nographics rather than reported as
    /// passing — a green run that executed nothing is worse than a red one.
    /// </summary>
    public sealed class GpuSurfaceExtractorOracleTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/Shaders/VoxelBrickMesher.compute";

        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        private ComputeShader _shader;

        [SetUp]
        public void SetUp()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the mesher cannot be exercised.");

            _shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(_shader, $"Compute shader missing at {ShaderPath}");
        }

        /// <summary>
        /// A brick that is solid below a plane and empty above it, published into the mirror.
        /// Simple enough to reason about, and it produces a surface in every cell it crosses.
        /// </summary>
        private static void BuildHalfSolidBrick(
            out Unity.Collections.NativeArray<byte> voxels,
            out Unity.Collections.NativeArray<ushort> semantics,
            out Unity.Collections.NativeArray<byte> boundary,
            int solidBelowY)
        {
            const int perBrick = 512;
            voxels = new Unity.Collections.NativeArray<byte>(
                perBrick, Unity.Collections.Allocator.Temp);
            semantics = new Unity.Collections.NativeArray<ushort>(
                perBrick, Unity.Collections.Allocator.Temp);
            boundary = new Unity.Collections.NativeArray<byte>(
                perBrick, Unity.Collections.Allocator.Temp);

            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                voxels[i] = (byte)(y < solidBelowY ? 1 : 0);
                semantics[i] = 0;
                boundary[i] = 0;
            }
        }

        [Test]
        public void TheMesherRunsAndProducesGeometryForASurfaceCrossingChunk()
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 64);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default,
                                    materialDefaultStyles: null);

            BuildHalfSolidBrick(out var voxels, out var semantics, out var boundary,
                                solidBelowY: 4);
            try
            {
                // Fill the whole neighbourhood with the same brick so the chunk's density taps never
                // fall off the edge of the cache into implicit air.
                var delta = VoxelBrickDelta.MixedAt(int3.zero, generation: 1, sourceSlot: 0);
                Assert.AreEqual(GpuBrickPublish.Uploaded,
                    mirror.Publish(delta, voxels, semantics, boundary, 0, hasPayload: true));
                Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));

                extractor.ClearBrickCache();
                uint entry = GpuSurfaceExtractor.PackBrickCacheEntry(
                    VoxelBrickContent.Mixed, 0, slot);
                for (int z = 0; z < extractor.BrickCacheEdge; z++)
                for (int y = 0; y < extractor.BrickCacheEdge; y++)
                for (int x = 0; x < extractor.BrickCacheEdge; x++)
                    extractor.SetBrickCacheEntry(new int3(x, y, z), entry);

                const int vertexCapacity = 65536;
                const int indexCapacity = 65536;
                var vertices = new ComputeBuffer(vertexCapacity, sizeof(float) * 6 + sizeof(uint) * 2,
                                                 ComputeBufferType.Structured);
                var indices = new ComputeBuffer(indexCapacity, sizeof(uint),
                                                ComputeBufferType.Structured);
                try
                {
                    GpuExtractionResult result = extractor.Extract(
                        mirror, tables,
                        chunkOriginVoxel: int3.zero,
                        brickCacheOrigin: new int3(-1, -1, -1),
                        sourceStep: 1, voxelSize: 0.1f,
                        vertices, indices, vertexCapacity, indexCapacity);

                    Assert.IsFalse(result.Overflowed,
                        "A chunk this small must fit; overflow means the counts and the writes "
                      + "disagree about how much geometry there is.");
                    Assert.Greater(result.IndexCount, 0,
                        "A brick that is solid below a plane and empty above it crosses the "
                      + "surface, so the mesher must emit triangles. Zero means the kernels ran "
                      + "but the density field, the tables or the cache lookup produced nothing.");
                    Assert.AreEqual(0, result.IndexCount % 3, "Indices must form whole triangles.");
                    Assert.Greater(result.VertexCount, 0);
                }
                finally
                {
                    vertices.Release();
                    indices.Release();
                }
            }
            finally
            {
                voxels.Dispose();
                semantics.Dispose();
                boundary.Dispose();
            }
        }

        [Test]
        public void AnEmptyNeighbourhoodProducesNoGeometry()
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, null);
            extractor.ClearBrickCache();   // every entry reads as empty

            const int capacity = 4096;
            var vertices = new ComputeBuffer(capacity, sizeof(float) * 6 + sizeof(uint) * 2,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, new int3(-1, -1, -1), 1, 0.1f,
                    vertices, indices, capacity, capacity);

                Assert.AreEqual(0, result.IndexCount,
                    "Air has no surface. Emitting here would mean the case-code rejection that "
                  + "keeps the write pass proportional to surface area is not working.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }

        [Test]
        public void SolidEverywhereProducesNoGeometry()
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, null);

            extractor.ClearBrickCache();
            uint uniformSolid = GpuSurfaceExtractor.PackBrickCacheEntry(
                VoxelBrickContent.Uniform, uniformMaterial: 1, slot: -1);
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
                extractor.SetBrickCacheEntry(new int3(x, y, z), uniformSolid);

            const int capacity = 4096;
            var vertices = new ComputeBuffer(capacity, sizeof(float) * 6 + sizeof(uint) * 2,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, new int3(-1, -1, -1), 1, 0.1f,
                    vertices, indices, capacity, capacity);

                Assert.AreEqual(0, result.IndexCount,
                    "The inside of solid rock has no surface either. This is the other half of "
                  + "the case-code rejection, and it is the common case in a voxel world.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }

        [Test]
        public void UniformBricksAreReadWithoutASlot()
        {
            // A uniform brick carries no payload, so the shader must take its material from the
            // cache entry itself. If it instead followed the slot field it would read brick 0's
            // voxels and the world would be made of whatever happened to be published first.
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, null);

            extractor.ClearBrickCache();
            uint solid = GpuSurfaceExtractor.PackBrickCacheEntry(VoxelBrickContent.Uniform, 1, -1);
            uint air = GpuSurfaceExtractor.PackBrickCacheEntry(VoxelBrickContent.Empty, 0, -1);
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
                extractor.SetBrickCacheEntry(new int3(x, y, z), y < 2 ? solid : air);

            const int capacity = 16384;
            var vertices = new ComputeBuffer(capacity, sizeof(float) * 6 + sizeof(uint) * 2,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, new int3(-1, -1, -1), 1, 0.1f,
                    vertices, indices, capacity, capacity);

                Assert.Greater(result.IndexCount, 0,
                    "Uniform solid under uniform air is a surface, and it must be found without "
                  + "any brick payload being published at all.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }
    }
}
