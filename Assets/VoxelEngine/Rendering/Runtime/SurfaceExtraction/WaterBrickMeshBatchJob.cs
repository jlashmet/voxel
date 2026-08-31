using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Greedy water surface extraction over a small immutable material snapshot batch. All mask,
    /// merge, winding and vertex emission work runs off the frame thread. The main thread copies
    /// only 896 material bytes per selected 8^3 brick (local payload + six boundary faces).
    /// </summary>
    [BurstCompile]
    internal struct WaterBrickMeshBatchJob : IJob
    {
        internal const int Edge = 8;
        internal const int VoxelsPerBrick = Edge * Edge * Edge;
        internal const int FaceArea = Edge * Edge;
        internal const int FaceCount = 6;
        internal const int SnapshotStride = VoxelsPerBrick + FaceCount * FaceArea;
        internal const int MaxBatch = 8;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        [ReadOnly] public NativeArray<int3> BrickBaseVoxels;
        [ReadOnly] public NativeArray<byte> SnapshotMaterials;
        public int BatchCount;
        public float VoxelSize;
        public NativeArray<byte> MaskScratch;
        public NativeList<SmoothSurfaceVertex> Vertices;
        public NativeList<uint> Indices;
        public NativeArray<int> Overflow;

        public void Execute()
        {
            Overflow[0] = 0;
            for (int batch = 0; batch < BatchCount; batch++)
            {
                if (!EmitBrick(batch, BrickBaseVoxels[batch])) return;
            }
        }

        private bool EmitBrick(int batch, int3 brickBaseVoxel)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                for (int layer = 0; layer < Edge; layer++)
                {
                    BuildMask(batch, axis, axisA, axisB, sign, layer);
                    if (!MergeMask(brickBaseVoxel, axis, axisA, axisB,
                                   sign, layer))
                        return false;
                }
            }
            return true;
        }

        private void BuildMask(int batch, int axis, int axisA, int axisB,
                               int sign, int layer)
        {
            int strideAxis = Stride(axis);
            int strideA = Stride(axisA);
            int strideB = Stride(axisB);
            int neighbourLayer = layer + sign;
            bool crossesBrick = (uint)neighbourLayer >= Edge;
            int layerBase = layer * strideAxis;
            int snapshotBase = batch * SnapshotStride;
            int face = axis * 2 + (sign > 0 ? 1 : 0);
            int faceBase = snapshotBase + VoxelsPerBrick + face * FaceArea;

            for (int b = 0; b < Edge; b++)
            for (int a = 0; a < Edge; a++)
            {
                int index = layerBase + b * strideB + a * strideA;
                byte material = SnapshotMaterials[snapshotBase + index];
                if (!IsWater(material))
                {
                    MaskScratch[a + b * Edge] = 0;
                    continue;
                }

                byte neighbour = crossesBrick
                    ? SnapshotMaterials[faceBase + a + b * Edge]
                    : SnapshotMaterials[snapshotBase + index + sign * strideAxis];
                MaskScratch[a + b * Edge] = neighbour == 0 ? material : (byte)0;
            }
        }

        private bool MergeMask(int3 brickBaseVoxel, int axis, int axisA, int axisB,
                               int sign, int layer)
        {
            for (int b = 0; b < Edge; b++)
            for (int a = 0; a < Edge; a++)
            {
                byte material = MaskScratch[a + b * Edge];
                if (material == 0) continue;

                int width = 1;
                while (a + width < Edge
                       && MaskScratch[a + width + b * Edge] == material)
                    width++;

                int height = 1;
                bool extend = true;
                while (b + height < Edge && extend)
                {
                    for (int k = 0; k < width; k++)
                    {
                        if (MaskScratch[a + k + (b + height) * Edge] == material)
                            continue;
                        extend = false;
                        break;
                    }
                    if (extend) height++;
                }

                for (int hb = 0; hb < height; hb++)
                for (int ha = 0; ha < width; ha++)
                    MaskScratch[a + ha + (b + hb) * Edge] = 0;

                if (!EmitQuad(material, brickBaseVoxel, axis, axisA, axisB,
                              sign, layer, a, b, width, height))
                    return false;
            }
            return true;
        }

        private bool EmitQuad(byte material, int3 brickBaseVoxel,
                              int axis, int axisA, int axisB, int sign, int layer,
                              int a, int b, int width, int height)
        {
            if (Vertices.Length + 4 > Vertices.Capacity
                || Indices.Length + 6 > Indices.Capacity)
            {
                Overflow[0] = 1;
                return false;
            }

            int planeVoxel = brickBaseVoxel[axis] + layer + (sign > 0 ? 1 : 0);
            int a0 = brickBaseVoxel[axisA] + a;
            int b0 = brickBaseVoxel[axisB] + b;
            float3 p0 = Corner(axis, axisA, axisB, planeVoxel, a0, b0);
            float3 p1 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0);
            float3 p2 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0 + height);
            float3 p3 = Corner(axis, axisA, axisB, planeVoxel, a0, b0 + height);
            float3 normal = float3.zero;
            normal[axis] = sign;

            uint baseIndex = (uint)Vertices.Length;
            uint m = material;
            Vertices.AddNoResize(Vertex(p0, normal, m));
            Vertices.AddNoResize(Vertex(p1, normal, m));
            Vertices.AddNoResize(Vertex(p2, normal, m));
            Vertices.AddNoResize(Vertex(p3, normal, m));

            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 2);
                Indices.AddNoResize(baseIndex + 1);
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 3);
                Indices.AddNoResize(baseIndex + 2);
            }
            else
            {
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 1);
                Indices.AddNoResize(baseIndex + 2);
                Indices.AddNoResize(baseIndex);
                Indices.AddNoResize(baseIndex + 2);
                Indices.AddNoResize(baseIndex + 3);
            }
            return true;
        }

        private float3 Corner(int axis, int axisA, int axisB, int plane, int a, int b)
        {
            float3 v = float3.zero;
            v[axis] = plane * VoxelSize;
            v[axisA] = a * VoxelSize;
            v[axisB] = b * VoxelSize;
            return v;
        }

        private static SmoothSurfaceVertex Vertex(float3 position, float3 normal, uint material) =>
            new()
            {
                Position = new Vector3(position.x, position.y, position.z),
                Normal = new Vector3(normal.x, normal.y, normal.z),
                Material = material,
                Active = FullyLitOcclusion,
            };

        private static int Stride(int axis) => axis == 0 ? 1 : axis == 1 ? Edge : Edge * Edge;
        private static bool IsWater(byte material) => material == 11 || material == 16;
    }
}
