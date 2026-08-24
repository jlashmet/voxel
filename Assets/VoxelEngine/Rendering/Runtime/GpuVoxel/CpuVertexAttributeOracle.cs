using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// One CPU topology triangle retaining the vertex attributes that affect shading.
    /// Verification-only: this is decoded from the real TransvoxelTopologyJob stream.
    /// </summary>
    public readonly struct OracleAttributedTriangle
    {
        public readonly SmoothSurfaceVertex A;
        public readonly SmoothSurfaceVertex B;
        public readonly SmoothSurfaceVertex C;

        public OracleAttributedTriangle(SmoothSurfaceVertex a, SmoothSurfaceVertex b,
                                        SmoothSurfaceVertex c)
        {
            A = a;
            B = b;
            C = c;
        }

        public string GeometryKey(float quantum = 1e-4f) => new OracleTriangle(
            (float3)A.Position, (float3)B.Position, (float3)C.Position).Key(quantum);

        public string MaterialKey(float positionQuantum = 1e-4f)
        {
            int start = CanonicalStart(positionQuantum);
            uint[] materials = { A.Material, B.Material, C.Material };
            return $"{materials[start]:X8}|{materials[(start + 1) % 3]:X8}|{materials[(start + 2) % 3]:X8}";
        }

        public string NormalKey(float positionQuantum = 1e-4f, float normalQuantum = 1e-3f)
        {
            int start = CanonicalStart(positionQuantum);
            Vector3[] normals = { A.Normal, B.Normal, C.Normal };
            return $"{QuantizedNormal(normals[start], normalQuantum)}|"
                 + $"{QuantizedNormal(normals[(start + 1) % 3], normalQuantum)}|"
                 + QuantizedNormal(normals[(start + 2) % 3], normalQuantum);
        }

        public bool HasFiniteNormals =>
            math.all(math.isfinite((float3)A.Normal))
            && math.all(math.isfinite((float3)B.Normal))
            && math.all(math.isfinite((float3)C.Normal));

        private int CanonicalStart(float quantum)
        {
            (long, long, long) Quantize(Vector3 v) => (
                (long)math.round(v.x / quantum),
                (long)math.round(v.y / quantum),
                (long)math.round(v.z / quantum));

            var corners = new[] { Quantize(A.Position), Quantize(B.Position), Quantize(C.Position) };
            int start = 0;
            for (int i = 1; i < 3; i++)
                if (corners[i].CompareTo(corners[start]) < 0) start = i;
            return start;
        }

        private static string QuantizedNormal(Vector3 normal, float quantum)
        {
            float3 n = normal;
            if (!math.all(math.isfinite(n))) return "nonfinite";
            return $"{(long)math.round(n.x / quantum)},{(long)math.round(n.y / quantum)},{(long)math.round(n.z / quantum)}";
        }
    }

    /// <summary>
    /// Minimal SceneIssue reproduction seam: runs the real CPU density/topology jobs over the same
    /// neighbourhood description used by the GPU oracle, but preserves normals and packed material
    /// instead of reducing the result to triangle positions.
    /// </summary>
    public static class CpuVertexAttributeOracle
    {
        public static List<OracleAttributedTriangle> MeshNeighbourhood(
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
                    bricks[i] = new TransvoxelDensityBrick
                    {
                        Kind = brickKinds[i],
                        UniformMaterial = brickUniformMaterials[i],
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

        private static List<OracleAttributedTriangle> Decode(NativeStream stream, int cells)
        {
            var triangles = new List<OracleAttributedTriangle>();
            NativeStream.Reader reader = stream.AsReader();
            var cellVertices = new List<SmoothSurfaceVertex>(16);

            for (int cell = 0; cell < cells; cell++)
            {
                int remaining = reader.BeginForEachIndex(cell);
                if (remaining <= 0)
                {
                    reader.EndForEachIndex();
                    continue;
                }

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
                    cellVertices.Add(reader.Read<SmoothSurfaceVertex>());

                for (int i = 0; i < indexCount; i += 3)
                {
                    int a = reader.Read<byte>();
                    int b = reader.Read<byte>();
                    int c = reader.Read<byte>();
                    triangles.Add(new OracleAttributedTriangle(
                        cellVertices[a], cellVertices[b], cellVertices[c]));
                }
                reader.EndForEachIndex();
            }

            return triangles;
        }
    }
}
