using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public struct DecorationThinSurfaceVertex
    {
        public float3 Position;
        public float3 Normal;
        public float2 Uv;
    }

    public struct DecorationThinSurfaceRange
    {
        public GeneratedPropId Id;
        public DecorationPropFamily Family;
        public int VertexStart;
        public int IndexStart;
        public int VertexCount;
        public int IndexCount;
    }

    /// <summary>
    /// Aggregated presentation geometry for all thin decorations in one semantic batch. Geometry is
    /// true surface geometry: rugs and wall art are quads offset by a small fraction of one voxel and
    /// do not consume a full voxel slab.
    /// </summary>
    public sealed class DecorationThinSurfaceBatch
    {
        public DecorationThinSurfaceVertex[] Vertices = new DecorationThinSurfaceVertex[0];
        public int[] Indices = new int[0];
        public DecorationThinSurfaceRange[] Ranges = new DecorationThinSurfaceRange[0];

        public int SurfaceCount => Ranges?.Length ?? 0;
        public bool IsWellFormed =>
            Vertices != null && Indices != null && Ranges != null &&
            Vertices.Length == SurfaceCount * 4 && Indices.Length == SurfaceCount * 6;
    }

    public static class DecorationThinSurfaceBatchBuilder
    {
        public static bool TryBuild(
            DecorationPlacement[] placements,
            float voxelWorldSize,
            float surfaceOffsetWorld,
            out DecorationThinSurfaceBatch batch)
        {
            batch = new DecorationThinSurfaceBatch();
            if (placements == null || voxelWorldSize <= 0f ||
                surfaceOffsetWorld < 0f || surfaceOffsetWorld >= voxelWorldSize * 0.5f)
                return false;

            int count = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].IsWellFormed && placements[i].Backend == DecorationRenderBackend.ThinSurface)
                    count++;
            }

            var vertices = new DecorationThinSurfaceVertex[count * 4];
            var indices = new int[count * 6];
            var ranges = new DecorationThinSurfaceRange[count];
            int surface = 0;

            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!placement.IsWellFormed || placement.Backend != DecorationRenderBackend.ThinSurface)
                    continue;

                int vertexStart = surface * 4;
                int indexStart = surface * 6;
                if (!TryWriteQuad(
                        in placement,
                        voxelWorldSize,
                        surfaceOffsetWorld,
                        vertices,
                        vertexStart))
                    return false;

                indices[indexStart] = vertexStart;
                indices[indexStart + 1] = vertexStart + 1;
                indices[indexStart + 2] = vertexStart + 2;
                indices[indexStart + 3] = vertexStart;
                indices[indexStart + 4] = vertexStart + 2;
                indices[indexStart + 5] = vertexStart + 3;
                ranges[surface] = new DecorationThinSurfaceRange
                {
                    Id = placement.Id,
                    Family = placement.Family,
                    VertexStart = vertexStart,
                    IndexStart = indexStart,
                    VertexCount = 4,
                    IndexCount = 6,
                };
                surface++;
            }

            batch.Vertices = vertices;
            batch.Indices = indices;
            batch.Ranges = ranges;
            return batch.IsWellFormed;
        }

        private static bool TryWriteQuad(
            in DecorationPlacement placement,
            float voxelWorldSize,
            float surfaceOffsetWorld,
            DecorationThinSurfaceVertex[] vertices,
            int start)
        {
            DecorationBounds bounds = placement.Bounds;
            float minX = bounds.Min.x * voxelWorldSize;
            float maxX = bounds.MaxExclusive.x * voxelWorldSize;
            float minY = bounds.Min.y * voxelWorldSize;
            float maxY = bounds.MaxExclusive.y * voxelWorldSize;
            float minZ = bounds.Min.z * voxelWorldSize;
            float maxZ = bounds.MaxExclusive.z * voxelWorldSize;

            float3 normal;
            float3 p0;
            float3 p1;
            float3 p2;
            float3 p3;

            if (placement.Family == DecorationPropFamily.Rug || math.abs(placement.Facing.y) == 1)
            {
                float y = minY + surfaceOffsetWorld;
                normal = new float3(0f, 1f, 0f);
                p0 = new float3(minX, y, minZ);
                p1 = new float3(minX, y, maxZ);
                p2 = new float3(maxX, y, maxZ);
                p3 = new float3(maxX, y, minZ);
            }
            else if (math.abs(placement.Facing.x) == 1)
            {
                bool positive = placement.Facing.x > 0;
                float x = positive ? minX + surfaceOffsetWorld : maxX - surfaceOffsetWorld;
                normal = new float3(placement.Facing.x, 0f, 0f);
                if (positive)
                {
                    p0 = new float3(x, minY, minZ);
                    p1 = new float3(x, maxY, minZ);
                    p2 = new float3(x, maxY, maxZ);
                    p3 = new float3(x, minY, maxZ);
                }
                else
                {
                    p0 = new float3(x, minY, maxZ);
                    p1 = new float3(x, maxY, maxZ);
                    p2 = new float3(x, maxY, minZ);
                    p3 = new float3(x, minY, minZ);
                }
            }
            else if (math.abs(placement.Facing.z) == 1)
            {
                bool positive = placement.Facing.z > 0;
                float z = positive ? minZ + surfaceOffsetWorld : maxZ - surfaceOffsetWorld;
                normal = new float3(0f, 0f, placement.Facing.z);
                if (positive)
                {
                    p0 = new float3(maxX, minY, z);
                    p1 = new float3(maxX, maxY, z);
                    p2 = new float3(minX, maxY, z);
                    p3 = new float3(minX, minY, z);
                }
                else
                {
                    p0 = new float3(minX, minY, z);
                    p1 = new float3(minX, maxY, z);
                    p2 = new float3(maxX, maxY, z);
                    p3 = new float3(maxX, minY, z);
                }
            }
            else
            {
                return false;
            }

            vertices[start] = Vertex(p0, normal, new float2(0f, 0f));
            vertices[start + 1] = Vertex(p1, normal, new float2(0f, 1f));
            vertices[start + 2] = Vertex(p2, normal, new float2(1f, 1f));
            vertices[start + 3] = Vertex(p3, normal, new float2(1f, 0f));
            return true;
        }

        private static DecorationThinSurfaceVertex Vertex(float3 position, float3 normal, float2 uv) =>
            new DecorationThinSurfaceVertex
            {
                Position = position,
                Normal = normal,
                Uv = uv,
            };
    }
}
