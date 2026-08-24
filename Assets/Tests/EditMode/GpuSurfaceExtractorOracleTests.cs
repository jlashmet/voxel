using System;
using System.Collections.Generic;
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
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";

        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        /// <summary>
        /// Sample spacing for the transition tests.
        ///
        /// Not 1. A face snapshot is 2*CellsPerAxis+1 samples at half this stride, which only spans
        /// the chunk when the stride is even; at 1 the half-stride degenerates to 1 and the snapshot
        /// covers twice the chunk. That is harmless in production because stride 1 is the innermost
        /// ring, which has no finer neighbour and so is never stitched — but it would make this test
        /// compare two meshers over voxels no seam ever touches.
        /// </summary>
        private const int SeamSourceStep = 2;

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

        [TestCase(1)]
        [TestCase(2)]
        public void GpuDensityMatchesTheCpuJobSampleForSample(int sourceStep)
        {
            // The real comparison. Everything downstream — case codes, which cells emit, where the
            // surface crosses each edge — is a deterministic function of these numbers and the
            // shared lookup tables, so agreement here is what makes the two meshers one renderer.
            // The CPU side runs the actual TransvoxelDensityJob, not a second hand-port of the same
            // rules: two hand-ports agreeing would only prove the same assumption was made twice.
            const int solidBrickYLimit = 2;
            const byte material = 1;

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;

            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++) defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            int3 brickCacheOrigin = new int3(-1, -1, -1);
            extractor.ClearBrickCache();
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
            {
                bool solid = y < solidBrickYLimit;
                extractor.SetBrickCacheEntry(new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? material : (byte)0, -1));
            }

            const int capacity = 16384;
            var vertices = new ComputeBuffer(capacity, sizeof(float) * 6 + sizeof(uint) * 2,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                extractor.Extract(mirror, tables, int3.zero, brickCacheOrigin, sourceStep, 0.1f,
                                  vertices, indices, capacity, capacity);

                var gpuDensity = new float[extractor.GridSize * extractor.GridSize * extractor.GridSize];
                extractor.ReadDensity(gpuDensity);

                float[] cpuDensity = CpuDensityOracle.SampleUniformNeighbourhood(
                    int3.zero, brickCacheOrigin, extractor.BrickCacheEdge,
                    CellsPerAxis, Padding, sourceStep: sourceStep,
                    material, solidBelowBrickY: true, solidBrickYLimit,
                    surfaces, coatings, palette);

                Assert.AreEqual(cpuDensity.Length, gpuDensity.Length);

                int mismatches = 0;
                float worst = 0f;
                int worstIndex = -1;
                for (int i = 0; i < cpuDensity.Length; i++)
                {
                    float delta = Mathf.Abs(cpuDensity[i] - gpuDensity[i]);
                    if (delta <= 1e-4f) continue;
                    mismatches++;
                    if (delta <= worst) continue;
                    worst = delta;
                    worstIndex = i;
                }

                Assert.AreEqual(0, mismatches,
                    $"{mismatches} of {cpuDensity.Length} samples disagree; worst {worst:F5} at "
                  + $"index {worstIndex} (cpu {(worstIndex >= 0 ? cpuDensity[worstIndex] : 0f):F5} "
                  + $"vs gpu {(worstIndex >= 0 ? gpuDensity[worstIndex] : 0f):F5}). The GPU port "
                  + "has diverged from TransvoxelDensityJob, so the two meshers would build "
                  + "different surfaces from the same voxels.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void GpuTrianglesMatchTheCpuTopologyJob(int sourceStep)
        {
            // Density agreeing is necessary but not sufficient: the tables, edge decoding,
            // interpolation and winding all sit between a matching field and matching geometry.
            // Triangles are compared as a set, because the two meshers allocate vertices in
            // different orders — the CPU walks cells in sequence, the GPU reserves with atomics —
            // so comparing index buffers directly would report a difference that is not one.
            // Winding is still part of the key: a flipped triangle faces away and leaves a hole.
            const int solidBrickYLimit = 2;
            const byte material = 1;
            const float voxelSize = 0.1f;

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++) defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            int3 brickCacheOrigin = new int3(-1, -1, -1);
            extractor.ClearBrickCache();
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
            {
                bool solid = y < solidBrickYLimit;
                extractor.SetBrickCacheEntry(new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? material : (byte)0, -1));
            }

            const int capacity = 65536;
            var vertices = new ComputeBuffer(capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, brickCacheOrigin, sourceStep, voxelSize,
                    vertices, indices, capacity, capacity);
                Assert.IsFalse(result.Overflowed);
                Assert.Greater(result.IndexCount, 0);

                var gpuVertices = new GpuSurfaceExtractor.ReadbackVertex[result.VertexCount];
                var gpuIndices = new uint[result.IndexCount];
                vertices.GetData(gpuVertices, 0, 0, result.VertexCount);
                indices.GetData(gpuIndices, 0, 0, result.IndexCount);

                var gpuKeys = new Dictionary<string, int>();
                for (int i = 0; i + 2 < gpuIndices.Length; i += 3)
                {
                    var tri = new OracleTriangle(
                        (float3)(Vector3)gpuVertices[gpuIndices[i]].Position,
                        (float3)(Vector3)gpuVertices[gpuIndices[i + 1]].Position,
                        (float3)(Vector3)gpuVertices[gpuIndices[i + 2]].Position);
                    string key = tri.Key();
                    gpuKeys[key] = gpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                List<OracleTriangle> cpu = CpuTopologyOracle.MeshUniformNeighbourhood(
                    int3.zero, brickCacheOrigin, extractor.BrickCacheEdge,
                    CellsPerAxis, Padding, sourceStep, voxelSize,
                    material, solidBrickYLimit, surfaces, coatings, palette);

                var cpuKeys = new Dictionary<string, int>();
                foreach (OracleTriangle tri in cpu)
                {
                    string key = tri.Key();
                    cpuKeys[key] = cpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                Assert.Greater(cpuKeys.Count, 0, "The CPU oracle produced no geometry to compare.");

                int missing = 0, extra = 0;
                string firstMissing = null;
                foreach (KeyValuePair<string, int> pair in cpuKeys)
                {
                    if (gpuKeys.TryGetValue(pair.Key, out int gpuCount) && gpuCount == pair.Value)
                        continue;
                    missing++;
                    firstMissing ??= pair.Key;
                }
                foreach (KeyValuePair<string, int> pair in gpuKeys)
                    if (!cpuKeys.ContainsKey(pair.Key)) extra++;

                // Separate "wrong shape" from "wrong winding": identical corner sets with
                // different order means the triangles are in the right place but facing the wrong
                // way, which is a different bug with a different fix.
                var cpuUnordered = new HashSet<string>();
                foreach (OracleTriangle tri in cpu) cpuUnordered.Add(tri.UnorderedKey());
                var gpuUnordered = new HashSet<string>();
                for (int i = 0; i + 2 < gpuIndices.Length; i += 3)
                    gpuUnordered.Add(new OracleTriangle(
                        (float3)(Vector3)gpuVertices[gpuIndices[i]].Position,
                        (float3)(Vector3)gpuVertices[gpuIndices[i + 1]].Position,
                        (float3)(Vector3)gpuVertices[gpuIndices[i + 2]].Position).UnorderedKey());
                int sharedIgnoringWinding = 0;
                foreach (string key in cpuUnordered)
                    if (gpuUnordered.Contains(key)) sharedIgnoringWinding++;

                Assert.AreEqual(0, missing + extra,
                    $"{missing} CPU triangles absent from the GPU output and {extra} GPU triangles "
                  + $"the CPU never produced, of {cpuKeys.Count} distinct CPU triangles. "
                  + $"{sharedIgnoringWinding} of {cpuUnordered.Count} match when winding is "
                  + $"ignored, so the disagreement is "
                  + (sharedIgnoringWinding == cpuUnordered.Count
                        ? "winding only." : "the surface itself.")
                  + $" First missing: {firstMissing}."
                  + $" First GPU: {(gpuKeys.Count > 0 ? System.Linq.Enumerable.First(gpuKeys.Keys) : "none")}."
                  + $" GPU verts {result.VertexCount}, indices {result.IndexCount};"
                  + $" CPU triangles {cpu.Count}.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void MixedSampleFieldMatchesTheCpuJob(int sourceStep)
        {
            const int perBrick = 512;
            var voxels = new byte[perBrick];
            var semantics = new ushort[perBrick];
            var boundary = new byte[perBrick];
            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                voxels[i] = (byte)(y < 4 ? 1 : 0);
                semantics[i] = (ushort)(((x + z) & 1) == 0 ? SurfaceStyles.Smooth
                                                           : SurfaceStyles.Rounded);
                boundary[i] = y == 3
                    ? VoxelBoundarySample.FromSignedQ4(6, extrusionAxis: 1).Packed
                    : (byte)0;
            }

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++)
                defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            var nativeVoxels = new Unity.Collections.NativeArray<byte>(
                voxels, Unity.Collections.Allocator.Temp);
            var nativeSemantics = new Unity.Collections.NativeArray<ushort>(
                semantics, Unity.Collections.Allocator.Temp);
            var nativeBoundary = new Unity.Collections.NativeArray<byte>(
                boundary, Unity.Collections.Allocator.Temp);
            const int capacity = 65536;
            var vertices = new ComputeBuffer(capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                var delta = VoxelBrickDelta.MixedAt(int3.zero, 1, 0);
                Assert.AreEqual(GpuBrickPublish.Uploaded,
                    mirror.Publish(delta, nativeVoxels, nativeSemantics, nativeBoundary, 0, true));
                Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));

                int edge = extractor.BrickCacheEdge;
                int3 brickCacheOrigin = new int3(-1, -1, -1);
                var kinds = new byte[edge * edge * edge];
                var uniforms = new byte[edge * edge * edge];
                extractor.ClearBrickCache();
                uint mixedEntry = GpuSurfaceExtractor.PackBrickCacheEntry(
                    VoxelBrickContent.Mixed, 0, slot);
                for (int z = 0; z < edge; z++)
                for (int y = 0; y < edge; y++)
                for (int x = 0; x < edge; x++)
                {
                    int i = x + edge * (y + edge * z);
                    extractor.SetBrickCacheEntry(new int3(x, y, z), mixedEntry);
                    kinds[i] = 2;
                }

                extractor.Extract(mirror, tables, int3.zero, brickCacheOrigin, sourceStep, 0.1f,
                                  vertices, indices, capacity, capacity);
                int sampleCount = extractor.GridSize * extractor.GridSize * extractor.GridSize;
                var gpuDensity = new float[sampleCount];
                var gpuMaterials = new uint[sampleCount];
                var gpuSurfaces = new uint[sampleCount];
                var gpuBoundaries = new uint[sampleCount];
                extractor.ReadDensity(gpuDensity);
                extractor.ReadSampleMaterials(gpuMaterials);
                extractor.ReadSampleSurfaces(gpuSurfaces);
                extractor.ReadSampleBoundaries(gpuBoundaries);

                CpuDensityFieldSnapshot cpu = CpuDensityOracle.SampleMixedNeighbourhood(
                    int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding, sourceStep,
                    kinds, uniforms, voxels, semantics, boundary, surfaces, coatings, palette);

                int densityMismatch = 0, materialMismatch = 0, surfaceMismatch = 0, boundaryMismatch = 0;
                int firstDensity = -1, firstMaterial = -1, firstSurface = -1, firstBoundary = -1;
                for (int i = 0; i < sampleCount; i++)
                {
                    if (Mathf.Abs(cpu.Density[i] - gpuDensity[i]) > 1e-4f)
                    {
                        densityMismatch++;
                        if (firstDensity < 0) firstDensity = i;
                    }
                    if (cpu.Materials[i] != (byte)gpuMaterials[i])
                    {
                        materialMismatch++;
                        if (firstMaterial < 0) firstMaterial = i;
                    }
                    if (cpu.Surfaces[i] != gpuSurfaces[i])
                    {
                        surfaceMismatch++;
                        if (firstSurface < 0) firstSurface = i;
                    }
                    if (cpu.Boundaries[i] != (byte)gpuBoundaries[i])
                    {
                        boundaryMismatch++;
                        if (firstBoundary < 0) firstBoundary = i;
                    }
                }

                Assert.AreEqual(0, densityMismatch + materialMismatch + surfaceMismatch + boundaryMismatch,
                    $"SourceStep {sourceStep}: density {densityMismatch} (first {firstDensity}), "
                  + $"material {materialMismatch} (first {firstMaterial}), "
                  + $"surface {surfaceMismatch} (first {firstSurface}), "
                  + $"boundary {boundaryMismatch} (first {firstBoundary}).");
            }
            finally
            {
                nativeVoxels.Dispose();
                nativeSemantics.Dispose();
                nativeBoundary.Dispose();
                vertices.Release();
                indices.Release();
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void MixedBricksWithAuthoredBoundariesAndCoatingsMatchTheCpu(int sourceStep)
        {
            // Uniform bricks never exercise the paths spec 003 added: the authored-boundary short
            // circuit, the coating displacement, and per-voxel surface styles. Those are where the
            // GPU port is most likely to still disagree, because each is a branch the simple
            // fixtures never take.
            const float voxelSize = 0.1f;
            const int perBrick = 512;

            var voxels = new byte[perBrick];
            var semantics = new ushort[perBrick];
            var boundary = new byte[perBrick];
            for (int z = 0; z < 8; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int i = x + 8 * (y + 8 * z);
                voxels[i] = (byte)(y < 4 ? 1 : 0);
                // Alternate two styles so the pairwise join rule is consulted rather than skipped.
                semantics[i] = (ushort)(((x + z) & 1) == 0 ? SurfaceStyles.Smooth
                                                           : SurfaceStyles.Rounded);
                // Author a boundary on the surface layer, along a specific axis, so both the
                // signed-offset decode and the axis test are exercised.
                boundary[i] = y == 3
                    ? VoxelBoundarySample.FromSignedQ4(6, extrusionAxis: 1).Packed
                    : (byte)0;
            }

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            CoatingCatalogueView coatings = default;
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++) defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, coatings, defaultStyles);

            var nativeVoxels = new Unity.Collections.NativeArray<byte>(
                voxels, Unity.Collections.Allocator.Temp);
            var nativeSemantics = new Unity.Collections.NativeArray<ushort>(
                semantics, Unity.Collections.Allocator.Temp);
            var nativeBoundary = new Unity.Collections.NativeArray<byte>(
                boundary, Unity.Collections.Allocator.Temp);

            const int capacity = 65536;
            var vertices = new ComputeBuffer(capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                var delta = VoxelBrickDelta.MixedAt(int3.zero, 1, 0);
                Assert.AreEqual(GpuBrickPublish.Uploaded,
                    mirror.Publish(delta, nativeVoxels, nativeSemantics, nativeBoundary, 0, true));
                Assert.IsTrue(mirror.TryGetSlot(int3.zero, out int slot));

                int edge = extractor.BrickCacheEdge;
                int3 brickCacheOrigin = new int3(-1, -1, -1);
                var kinds = new byte[edge * edge * edge];
                var uniforms = new byte[edge * edge * edge];

                extractor.ClearBrickCache();
                uint mixedEntry = GpuSurfaceExtractor.PackBrickCacheEntry(
                    VoxelBrickContent.Mixed, 0, slot);
                for (int z = 0; z < edge; z++)
                for (int y = 0; y < edge; y++)
                for (int x = 0; x < edge; x++)
                {
                    extractor.SetBrickCacheEntry(new int3(x, y, z), mixedEntry);
                    kinds[x + edge * (y + edge * z)] = 2;      // mixed
                    uniforms[x + edge * (y + edge * z)] = 0;
                }

                GpuExtractionResult result = extractor.Extract(
                    mirror, tables, int3.zero, brickCacheOrigin, sourceStep, voxelSize,
                    vertices, indices, capacity, capacity);
                Assert.IsFalse(result.Overflowed);

                var gpuVertices = new GpuSurfaceExtractor.ReadbackVertex[Mathf.Max(1, result.VertexCount)];
                var gpuIndices = new uint[Mathf.Max(1, result.IndexCount)];
                if (result.VertexCount > 0) vertices.GetData(gpuVertices, 0, 0, result.VertexCount);
                if (result.IndexCount > 0) indices.GetData(gpuIndices, 0, 0, result.IndexCount);

                var gpuKeys = new Dictionary<string, int>();
                for (int i = 0; i + 2 < result.IndexCount; i += 3)
                {
                    string key = new OracleTriangle(
                        (float3)(Vector3)gpuVertices[gpuIndices[i]].Position,
                        (float3)(Vector3)gpuVertices[gpuIndices[i + 1]].Position,
                        (float3)(Vector3)gpuVertices[gpuIndices[i + 2]].Position).Key();
                    gpuKeys[key] = gpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                List<OracleTriangle> cpu = CpuTopologyOracle.MeshNeighbourhood(
                    int3.zero, brickCacheOrigin, edge, CellsPerAxis, Padding, sourceStep, voxelSize,
                    kinds, uniforms, voxels, semantics, boundary,
                    surfaces, coatings, palette);

                var cpuKeys = new Dictionary<string, int>();
                foreach (OracleTriangle tri in cpu)
                {
                    string key = tri.Key();
                    cpuKeys[key] = cpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                Assert.Greater(cpuKeys.Count, 0,
                    "The fixture must produce a surface, or this proves nothing.");

                int missing = 0, extra = 0;
                foreach (KeyValuePair<string, int> pair in cpuKeys)
                    if (!gpuKeys.TryGetValue(pair.Key, out int n) || n != pair.Value) missing++;
                foreach (KeyValuePair<string, int> pair in gpuKeys)
                    if (!cpuKeys.ContainsKey(pair.Key)) extra++;

                Assert.AreEqual(0, missing + extra,
                    $"{missing} CPU triangles missing and {extra} GPU triangles unexpected, of "
                  + $"{cpuKeys.Count}. Mixed bricks carry per-voxel styles, authored boundaries and "
                  + "coatings, so a divergence here is in the spec-003 surface semantics rather "
                  + "than in the Transvoxel tables.");
            }
            finally
            {
                nativeVoxels.Dispose();
                nativeSemantics.Dispose();
                nativeBoundary.Dispose();
                vertices.Release();
                indices.Release();
            }
        }

        /// <summary>
        /// Fills the extractor's brick cache with a world that is solid below a plane of bricks,
        /// and returns the world-voxel occupancy function describing the same world.
        /// </summary>
        private static Func<int3, byte> FillHalfSolidUniformCache(
            GpuSurfaceExtractor extractor, int solidBrickYLimit, byte material)
        {
            extractor.ClearBrickCache();
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
            {
                bool solid = y < solidBrickYLimit;
                extractor.SetBrickCacheEntry(new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? material : (byte)0, -1));
            }

            // The cache origin is one brick below the chunk, so cache brick row y covers world
            // voxel rows [(y - 1) * 8, y * 8).
            int solidBelowVoxelY = (solidBrickYLimit - 1) * VoxelReadGrid.BlockEdge;
            return voxel => voxel.y < solidBelowVoxelY ? material : (byte)0;
        }

        [Test]
        public void GpuTransitionFaceSnapshotMatchesTheCpuSampling()
        {
            // Transition cells read the face at the *finer* neighbour's spacing, which is not a
            // position the chunk lattice contains at all. If the GPU samples that lattice at the
            // wrong stride or maps (u, v) onto the wrong axes, every seam is stitched from the
            // wrong voxels — and it would still look like a plausible surface.
            const int solidBrickYLimit = 2;
            const byte material = 1;
            const int face = 4;   // -Z, so the face plane is the chunk origin itself

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++) defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, default, defaultStyles);

            Func<int3, byte> world = FillHalfSolidUniformCache(extractor, solidBrickYLimit, material);

            const int capacity = 65536;
            var vertices = new ComputeBuffer(capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            try
            {
                extractor.Extract(mirror, tables, int3.zero, new int3(-1, -1, -1),
                                  SeamSourceStep, 0.1f, vertices, indices, capacity, capacity);
                extractor.ExtractTransitionFace(mirror, tables, face, int3.zero,
                                                new int3(-1, -1, -1), SeamSourceStep, 0.1f,
                                                vertices, indices, capacity, capacity);

                int samples = extractor.FaceSamplesPerAxis * extractor.FaceSamplesPerAxis;
                var gpuFace = new float[samples];
                extractor.ReadFaceDensity(gpuFace);

                var cpuFace = new float[samples];
                var cpuMaterials = new byte[samples];
                var cpuSurfaces = new uint[samples];
                CpuTransitionOracle.SampleFace(int3.zero, CellsPerAxis, SeamSourceStep,
                                               face, world, palette,
                                               cpuFace, cpuMaterials, cpuSurfaces);

                int solidSamples = 0;
                foreach (float d in cpuFace) if (d > 0f) solidSamples++;
                Assert.Greater(solidSamples, 0, "The fixture must put solid voxels on this face.");
                Assert.Less(solidSamples, samples, "and air, or there is no transition to mesh.");

                for (int i = 0; i < samples; i++)
                    Assert.AreEqual(cpuFace[i], gpuFace[i], 1e-4f,
                        $"Face sample {i} disagrees. The snapshot is the input to every transition "
                      + "cell on this face, so a difference here is a seam built from the wrong "
                      + "voxels rather than a meshing bug.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }

        [Test]
        public void GpuTransitionCellsMatchTheCpuTransitionMeshJob()
        {
            // The seam itself. Wrong axis frame, wrong sign convention or wrong winding here all
            // produce a slab that is present but does not weld, which reads as the LOD popping the
            // transition cells exist to hide. Every face is checked, because the canonical table is
            // mapped onto six different frames and five of them could be wrong on their own.
            const int solidBrickYLimit = 2;
            const byte material = 1;
            const float voxelSize = 0.1f;

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            SurfaceCatalogueView surfaces = SurfaceCatalogueView.CreateBuiltIns();
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++) defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(surfaces, default, defaultStyles);

            Func<int3, byte> world = FillHalfSolidUniformCache(extractor, solidBrickYLimit, material);

            const int capacity = 65536;
            int comparedFaces = 0;
            int gpuTransitionTriangles = 0;

            for (int face = 0; face < 6; face++)
            {
                var vertices = new ComputeBuffer(capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                                 ComputeBufferType.Structured);
                var indices = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
                try
                {
                    // Extract first only to zero the counters; its regular geometry is then
                    // subtracted off so this compares transition output alone.
                    GpuExtractionResult regular = extractor.Extract(
                        mirror, tables, int3.zero, new int3(-1, -1, -1), SeamSourceStep,
                        voxelSize, vertices, indices, capacity, capacity);

                    GpuExtractionResult total = extractor.ExtractTransitionFace(
                        mirror, tables, face, int3.zero, new int3(-1, -1, -1), SeamSourceStep,
                        voxelSize, vertices, indices, capacity, capacity);
                    Assert.IsFalse(total.Overflowed, $"Face {face} overflowed.");

                    int transitionIndexCount = total.IndexCount - regular.IndexCount;
                    Assert.GreaterOrEqual(transitionIndexCount, 0,
                        $"Face {face}: the transition pass must append, never rewind.");

                    var gpuVertices = new GpuSurfaceExtractor.ReadbackVertex[
                        Mathf.Max(1, total.VertexCount)];
                    var gpuIndices = new uint[Mathf.Max(1, total.IndexCount)];
                    if (total.VertexCount > 0)
                        vertices.GetData(gpuVertices, 0, 0, total.VertexCount);
                    if (total.IndexCount > 0) indices.GetData(gpuIndices, 0, 0, total.IndexCount);

                    var gpuKeys = new Dictionary<string, int>();
                    for (int i = regular.IndexCount; i + 2 < total.IndexCount; i += 3)
                    {
                        string key = new OracleTriangle(
                            (float3)(Vector3)gpuVertices[gpuIndices[i]].Position,
                            (float3)(Vector3)gpuVertices[gpuIndices[i + 1]].Position,
                            (float3)(Vector3)gpuVertices[gpuIndices[i + 2]].Position).Key();
                        gpuKeys[key] = gpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
                    }

                    int samples = extractor.FaceSamplesPerAxis * extractor.FaceSamplesPerAxis;
                    var cpuFace = new float[samples];
                    var cpuMaterials = new byte[samples];
                    var cpuSurfaces = new uint[samples];
                    CpuTransitionOracle.SampleFace(int3.zero, CellsPerAxis, SeamSourceStep,
                                                   face, world, palette,
                                                   cpuFace, cpuMaterials, cpuSurfaces);

                    List<OracleTriangle> cpu = CpuTransitionOracle.MeshFace(
                        int3.zero, CellsPerAxis, SeamSourceStep, voxelSize, face,
                        cpuFace, cpuMaterials, cpuSurfaces);

                    var cpuKeys = new Dictionary<string, int>();
                    foreach (OracleTriangle tri in cpu)
                    {
                        string key = tri.Key();
                        cpuKeys[key] = cpuKeys.TryGetValue(key, out int n) ? n + 1 : 1;
                    }

                    if (cpuKeys.Count > 0) comparedFaces++;
                    gpuTransitionTriangles += transitionIndexCount / 3;

                    int missing = 0, extra = 0;
                    string firstMissing = null;
                    foreach (KeyValuePair<string, int> pair in cpuKeys)
                    {
                        if (gpuKeys.TryGetValue(pair.Key, out int n) && n == pair.Value) continue;
                        missing++;
                        firstMissing ??= pair.Key;
                    }
                    foreach (KeyValuePair<string, int> pair in gpuKeys)
                        if (!cpuKeys.ContainsKey(pair.Key)) extra++;

                    // Separate "wrong place" from "wrong way round": both are defects, but a
                    // flipped slab and a misplaced one have different causes.
                    var cpuUnordered = new HashSet<string>();
                    foreach (OracleTriangle tri in cpu) cpuUnordered.Add(tri.UnorderedKey());
                    var gpuUnordered = new HashSet<string>();
                    for (int i = regular.IndexCount; i + 2 < total.IndexCount; i += 3)
                        gpuUnordered.Add(new OracleTriangle(
                            (float3)(Vector3)gpuVertices[gpuIndices[i]].Position,
                            (float3)(Vector3)gpuVertices[gpuIndices[i + 1]].Position,
                            (float3)(Vector3)gpuVertices[gpuIndices[i + 2]].Position).UnorderedKey());
                    int sharedIgnoringWinding = 0;
                    foreach (string key in cpuUnordered)
                        if (gpuUnordered.Contains(key)) sharedIgnoringWinding++;

                    Assert.AreEqual(0, missing + extra,
                        $"Face {face}: {missing} CPU transition triangles absent from the GPU output "
                      + $"and {extra} GPU triangles the CPU never produced, of {cpuKeys.Count} "
                      + $"distinct CPU triangles ({cpu.Count} total, GPU {transitionIndexCount / 3}). "
                      + $"{sharedIgnoringWinding} of {cpuUnordered.Count} match ignoring winding, so "
                      + "the disagreement is "
                      + (cpuUnordered.Count > 0 && sharedIgnoringWinding == cpuUnordered.Count
                            ? "winding only." : "the slab's position or shape.")
                      + $" First missing: {firstMissing}.");
                }
                finally
                {
                    vertices.Release();
                    indices.Release();
                }
            }

            Assert.Greater(comparedFaces, 0,
                "No face produced transition geometry, so this asserted nothing. The fixture must "
              + "cross the surface on at least one face.");
            Assert.Greater(gpuTransitionTriangles, 0,
                "The GPU emitted no transition geometry on any face. Matching the CPU by both "
              + "producing nothing would prove only that the kernel never ran.");
        }
    }
}
