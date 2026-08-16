using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>Greedy-merges exact face masks into a compact mesh off the render thread.</summary>
    [BurstCompile]
    internal struct FacetedMergeJob : IJob
    {
        public NativeArray<uint> FaceMasks;
        public NativeList<SmoothSurfaceVertex> Vertices;
        public NativeList<uint> Indices;
        public int3 ChunkOrigin;
        public int CellsPerAxis;
        public int SourceStep;
        public float VoxelSize;

        public void Execute()
        {
            Vertices.Clear();
            Indices.Clear();
            int cellsPerPlane = CellsPerAxis * CellsPerAxis;
            for (int plane = 0; plane < 6 * CellsPerAxis; plane++)
            {
                int offset = plane * cellsPerPlane;
                int layer = plane % CellsPerAxis;
                int face = plane / CellsPerAxis;
                int axis = face >> 1;
                int sign = (face & 1) == 0 ? -1 : 1;
                for (int b = 0; b < CellsPerAxis; b++)
                for (int a = 0; a < CellsPerAxis; a++)
                {
                    int start = offset + a + b * CellsPerAxis;
                    uint encoded = FaceMasks[start];
                    if (encoded == 0) continue;
                    int width = 1;
                    while (a + width < CellsPerAxis && FaceMasks[start + width] == encoded)
                        width++;
                    int height = 1;
                    bool extend = true;
                    while (b + height < CellsPerAxis && extend)
                    {
                        for (int k = 0; k < width; k++)
                            if (FaceMasks[offset + a + k + (b + height) * CellsPerAxis]
                                != encoded) { extend = false; break; }
                        if (extend) height++;
                    }
                    for (int db = 0; db < height; db++)
                    for (int da = 0; da < width; da++)
                        FaceMasks[offset + a + da + (b + db) * CellsPerAxis] = 0;
                    Emit(axis, sign, layer, a, b, width, height, encoded - 1u);
                }
            }
        }

        private void Emit(int axis, int sign, int layer, int a, int b, int width,
                          int height, uint attributes)
        {
            int step = math.max(1, SourceStep);
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            float3 p0 = ChunkOrigin;
            p0[axis] += (layer + (sign > 0 ? 1 : 0)) * step;
            p0[axisA] += a * step;
            p0[axisB] += b * step;
            float3 p1 = p0, p2 = p0, p3 = p0;
            p1[axisA] += width * step; p2[axisA] += width * step;
            p2[axisB] += height * step; p3[axisB] += height * step;
            p0 *= VoxelSize; p1 *= VoxelSize; p2 *= VoxelSize; p3 *= VoxelSize;
            float3 normal = float3.zero; normal[axis] = sign;
            uint baseVertex = (uint)Vertices.Length;
            var n = (Vector3)normal;
            const uint lit = 0x0000FF00u;
            Vertices.Add(new SmoothSurfaceVertex { Position=(Vector3)p0, Normal=n, Material=attributes, Active=lit });
            Vertices.Add(new SmoothSurfaceVertex { Position=(Vector3)p1, Normal=n, Material=attributes, Active=lit });
            Vertices.Add(new SmoothSurfaceVertex { Position=(Vector3)p2, Normal=n, Material=attributes, Active=lit });
            Vertices.Add(new SmoothSurfaceVertex { Position=(Vector3)p3, Normal=n, Material=attributes, Active=lit });
            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                Indices.Add(baseVertex); Indices.Add(baseVertex+2); Indices.Add(baseVertex+1);
                Indices.Add(baseVertex); Indices.Add(baseVertex+3); Indices.Add(baseVertex+2);
            }
            else
            {
                Indices.Add(baseVertex); Indices.Add(baseVertex+1); Indices.Add(baseVertex+2);
                Indices.Add(baseVertex); Indices.Add(baseVertex+2); Indices.Add(baseVertex+3);
            }
        }
    }
}
