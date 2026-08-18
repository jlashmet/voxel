using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>One triangle, as three positions, for comparing two meshers.</summary>
    public readonly struct OracleTriangle
    {
        public readonly float3 A, B, C;

        public OracleTriangle(float3 a, float3 b, float3 c) { A = a; B = b; C = c; }

        /// <summary>
        /// A key that ignores which corner the triangle starts from but not its winding.
        ///
        /// The two meshers allocate vertices in different orders — the CPU walks cells in sequence,
        /// the GPU reserves with atomics — so comparing index buffers directly would report a
        /// difference that is not one. Winding is kept because a flipped triangle is a real defect:
        /// it faces away and leaves a hole.
        /// </summary>
        /// <summary>Winding-insensitive key, used only to tell "wrong shape" from "wrong winding".</summary>
        public string UnorderedKey(float quantum = 1e-4f)
        {
            (long, long, long) Q(float3 v) => (
                (long)math.round(v.x / quantum),
                (long)math.round(v.y / quantum),
                (long)math.round(v.z / quantum));
            var corners = new List<(long, long, long)> { Q(A), Q(B), Q(C) };
            corners.Sort();
            return string.Join("|", corners);
        }

        public string Key(float quantum = 1e-4f)
        {
            (long, long, long) Q(float3 v) => (
                (long)math.round(v.x / quantum),
                (long)math.round(v.y / quantum),
                (long)math.round(v.z / quantum));

            var corners = new[] { Q(A), Q(B), Q(C) };
            int start = 0;
            for (int i = 1; i < 3; i++)
                if (corners[i].CompareTo(corners[start]) < 0) start = i;

            var ordered = new (long, long, long)[3];
            for (int i = 0; i < 3; i++) ordered[i] = corners[(start + i) % 3];
            return string.Join("|", ordered);
        }
    }

    /// <summary>
    /// Runs the CPU density and topology jobs so the GPU mesher can be compared against the real
    /// implementation rather than a restatement of it.
    ///
    /// Not part of the frame path: it allocates, blocks, and exists for verification.
    /// </summary>
    public static class CpuTopologyOracle
    {
        /// <summary>
        /// Meshes one chunk over a uniform brick neighbourhood and returns its triangles.
        /// The neighbourhood is described exactly as the GPU brick cache describes it.
        /// </summary>
        public static List<OracleTriangle> MeshUniformNeighbourhood(
            int3 chunkOriginVoxel, int3 brickCacheOrigin, int brickCacheEdge,
            int cellsPerAxis, int padding, int sourceStep, float voxelSize,
            byte uniformMaterial, int solidBrickYLimit,
            in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
            in MaterialPaletteView palette)
        {
            int uniformCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;
            var kinds = new byte[uniformCount];
            var uniforms = new byte[uniformCount];
            for (int z = 0; z < brickCacheEdge; z++)
            for (int y = 0; y < brickCacheEdge; y++)
            for (int x = 0; x < brickCacheEdge; x++)
            {
                int i = x + brickCacheEdge * (y + brickCacheEdge * z);
                bool solid = y < solidBrickYLimit;
                kinds[i] = (byte)(solid ? 1 : 0);
                uniforms[i] = solid ? uniformMaterial : (byte)0;
            }
            return MeshNeighbourhood(chunkOriginVoxel, brickCacheOrigin, brickCacheEdge,
                                     cellsPerAxis, padding, sourceStep, voxelSize,
                                     kinds, uniforms, null, null, null,
                                     surfaces, coatings, palette);
        }

        /// <summary>
        /// Meshes one chunk over an explicitly described brick neighbourhood, so mixed payloads —
        /// where authored boundaries and coatings actually live — can be compared too.
        /// </summary>
        public static List<OracleTriangle> MeshNeighbourhood(
            int3 chunkOriginVoxel, int3 brickCacheOrigin, int brickCacheEdge,
            int cellsPerAxis, int padding, int sourceStep, float voxelSize,
            byte[] brickKinds, byte[] brickUniformMaterials,
            byte[] mixedVoxels, ushort[] mixedSurfaceSemantics, byte[] mixedBoundarySamples,
            in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
            in MaterialPaletteView palette)
        {
            int gridSize = cellsPerAxis + padding * 2 + 1;
            int samples = gridSize * gridSize * gridSize;
            int cells = cellsPerAxis * cellsPerAxis * cellsPerAxis;
            int brickCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;

            var tables = new TransvoxelLookupTables();
            var bricks = new NativeArray<TransvoxelDensityBrick>(brickCount, Allocator.TempJob);
            var density = new NativeArray<float>(samples, Allocator.TempJob);
            var materials = new NativeArray<byte>(samples, Allocator.TempJob);
            var semantics = new NativeArray<uint>(samples, Allocator.TempJob);
            var boundaries = new NativeArray<byte>(samples, Allocator.TempJob);
            var payloadVoxels = new NativeArray<byte>(
                mixedVoxels is { Length: > 0 } ? mixedVoxels.Length : 1, Allocator.TempJob);
            var payloadSemantics = new NativeArray<ushort>(
                mixedSurfaceSemantics is { Length: > 0 } ? mixedSurfaceSemantics.Length : 1,
                Allocator.TempJob);
            var payloadBoundary = new NativeArray<byte>(
                mixedBoundarySamples is { Length: > 0 } ? mixedBoundarySamples.Length : 1,
                Allocator.TempJob);
            var stream = new NativeStream(cells, Allocator.TempJob);

            try
            {
                if (mixedVoxels is { Length: > 0 }) payloadVoxels.CopyFrom(mixedVoxels);
                if (mixedSurfaceSemantics is { Length: > 0 })
                    payloadSemantics.CopyFrom(mixedSurfaceSemantics);
                if (mixedBoundarySamples is { Length: > 0 })
                    payloadBoundary.CopyFrom(mixedBoundarySamples);

                for (int i = 0; i < brickCount; i++)
                {
                    byte kind = brickKinds[i];
                    bricks[i] = new TransvoxelDensityBrick
                    {
                        Kind = kind,
                        UniformMaterial = brickUniformMaterials[i],
                        // Every mixed brick in these fixtures shares one payload, which is enough to
                        // exercise the boundary and coating paths without building a whole world.
                        MixedOffset = 0,
                    };
                }

                var densityJob = new TransvoxelDensityJob
                {
                    Bricks = bricks,
                    MixedVoxels = payloadVoxels,
                    MixedSurfaceSemantics = payloadSemantics,
                    MixedBoundarySamples = payloadBoundary,
                    Palette = palette,
                    Catalogue = surfaces,
                    Coatings = coatings,
                    Density = density,
                    Materials = materials,
                    SurfaceSemantics = semantics,
                    BoundarySamples = boundaries,
                    ChunkOriginVoxel = chunkOriginVoxel,
                    BrickCacheOrigin = brickCacheOrigin,
                    BrickCacheEdge = brickCacheEdge,
                    GridSize = gridSize,
                    Padding = padding,
                    SourceStep = sourceStep,
                };
                for (int i = 0; i < samples; i++) densityJob.Execute(i);

                var topologyJob = new TransvoxelTopologyJob
                {
                    Density = density,
                    Materials = materials,
                    SurfaceSemantics = semantics,
                    BoundarySamples = boundaries,
                    CellClass = tables.RegularCellClass,
                    GeometryCounts = tables.RegularGeometryCounts,
                    CellVertexIndices = tables.RegularCellVertexIndices,
                    EdgeCodes = tables.RegularEdgeCodes,
                    Catalogue = surfaces,
                    Coatings = coatings,
                    ChunkOriginVoxel = chunkOriginVoxel,
                    CellsPerAxis = cellsPerAxis,
                    GridSize = gridSize,
                    Padding = padding,
                    SourceStep = sourceStep,
                    VoxelSize = voxelSize,
                    Output = stream.AsWriter(),
                };
                for (int cell = 0; cell < cells; cell++) topologyJob.Execute(cell);

                return Decode(stream, cells);
            }
            finally
            {
                stream.Dispose();
                bricks.Dispose();
                density.Dispose();
                materials.Dispose();
                semantics.Dispose();
                boundaries.Dispose();
                payloadVoxels.Dispose();
                payloadSemantics.Dispose();
                payloadBoundary.Dispose();
                tables.Dispose();
            }
        }

        private static List<OracleTriangle> Decode(NativeStream stream, int cells)
        {
            var triangles = new List<OracleTriangle>();
            NativeStream.Reader reader = stream.AsReader();
            var cellVertices = new List<float3>(16);

            for (int cell = 0; cell < cells; cell++)
            {
                int remaining = reader.BeginForEachIndex(cell);
                if (remaining <= 0) { reader.EndForEachIndex(); continue; }

                byte status = reader.Read<byte>();
                byte vertexCount = reader.Read<byte>();
                byte indexCount = reader.Read<byte>();
                if (status != 0 || vertexCount == 0 || indexCount == 0)
                {
                    reader.EndForEachIndex();
                    continue;
                }

                cellVertices.Clear();
                for (int v = 0; v < vertexCount; v++)
                    cellVertices.Add((float3)(Vector3)reader.Read<SmoothSurfaceVertex>().Position);

                for (int i = 0; i < indexCount; i += 3)
                {
                    int a = reader.Read<byte>();
                    int b = reader.Read<byte>();
                    int c = reader.Read<byte>();
                    triangles.Add(new OracleTriangle(
                        cellVertices[a], cellVertices[b], cellVertices[c]));
                }
                reader.EndForEachIndex();
            }
            return triangles;
        }
    }
}
