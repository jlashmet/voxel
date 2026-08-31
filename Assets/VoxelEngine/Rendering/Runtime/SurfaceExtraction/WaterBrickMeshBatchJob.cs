using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Greedy water surface extraction over a small immutable material snapshot batch. Liquid
    /// identity is supplied as an opaque presentation mask; no game material IDs live here.
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
        public uint WaterMaterialMask;
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
                    if (!MergeMask(batch, brickBaseVoxel, axis, axisA, axisB,
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

        private bool MergeMask(int batch, int3 brickBaseVoxel,
                               int axis, int axisA, int axisB,
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

                if (!EmitQuad(batch, material, brickBaseVoxel, axis, axisA, axisB,
                              sign, layer, a, b, width, height))
                    return false;
            }
            return true;
        }

        private bool EmitQuad(int batch, byte material, int3 brickBaseVoxel,
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

            int3 c0 = SourceCell(axis, axisA, axisB, layer, a, b);
            int3 c1 = SourceCell(axis, axisA, axisB, layer, a + width - 1, b);
            int3 c2 = SourceCell(axis, axisA, axisB, layer, a + width - 1, b + height - 1);
            int3 c3 = SourceCell(axis, axisA, axisB, layer, a, b + height - 1);

            uint baseMaterial = material;
            uint f0 = TopologyFlags(batch, c0, axis);
            uint f1 = TopologyFlags(batch, c1, axis);
            uint f2 = TopologyFlags(batch, c2, axis);
            uint f3 = TopologyFlags(batch, c3, axis);
            uint baseIndex = (uint)Vertices.Length;
            Vertices.AddNoResize(Vertex(p0, normal, baseMaterial | f0));
            Vertices.AddNoResize(Vertex(p1, normal, baseMaterial | f1));
            Vertices.AddNoResize(Vertex(p2, normal, baseMaterial | f2));
            Vertices.AddNoResize(Vertex(p3, normal, baseMaterial | f3));
            AddQuadIndices(baseIndex, p0, p1, p2, normal);

            // Surface-local mist can brighten an impact boundary, but convincing spray needs pixels
            // outside the falling sheet. Emit one small semantic spray skirt at a true lower boundary
            // into the same canonical water mesh. The shader makes it visible only for profiles that
            // opt into waterfall mist, so still/river materials retain their existing appearance.
            if (axis != 1)
            {
                bool impact0 = (f0 & SmoothSurfaceVertex.WaterImpactFlag) != 0;
                bool impact1 = (f1 & SmoothSurfaceVertex.WaterImpactFlag) != 0;
                bool impact2 = (f2 & SmoothSurfaceVertex.WaterImpactFlag) != 0;
                bool impact3 = (f3 & SmoothSurfaceVertex.WaterImpactFlag) != 0;
                if (axisA == 1 && impact0 && impact3)
                {
                    if (!EmitImpactSpray(baseMaterial, p0, p3, normal)) return false;
                }
                else if (axisB == 1 && impact0 && impact1)
                {
                    if (!EmitImpactSpray(baseMaterial, p0, p1, normal)) return false;
                }
            }
            return true;
        }

        private bool EmitImpactSpray(uint baseMaterial, float3 edge0, float3 edge1, float3 normal)
        {
            if (Vertices.Length + 4 > Vertices.Capacity
                || Indices.Length + 6 > Indices.Capacity)
            {
                Overflow[0] = 1;
                return false;
            }

            float3 plumeOffset = normal * (VoxelSize * 1.6f) + new float3(0f, VoxelSize * 2.4f, 0f);
            float3 p0 = edge0;
            float3 p1 = edge1;
            float3 p2 = edge1 + plumeOffset;
            float3 p3 = edge0 + plumeOffset;
            uint sprayMaterial = baseMaterial
                               | SmoothSurfaceVertex.WaterImpactFlag
                               | SmoothSurfaceVertex.WaterEdgeFlag
                               | SmoothSurfaceVertex.WaterSprayFlag;
            uint baseIndex = (uint)Vertices.Length;
            Vertices.AddNoResize(Vertex(p0, normal, sprayMaterial));
            Vertices.AddNoResize(Vertex(p1, normal, sprayMaterial));
            Vertices.AddNoResize(Vertex(p2, normal, sprayMaterial));
            Vertices.AddNoResize(Vertex(p3, normal, sprayMaterial));
            AddQuadIndices(baseIndex, p0, p1, p2, normal);
            return true;
        }

        private void AddQuadIndices(uint baseIndex, float3 p0, float3 p1, float3 p2, float3 normal)
        {
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
        }

        private uint TopologyFlags(int batch, int3 localCell, int faceAxis)
        {
            // Horizontal water surfaces retain the existing profile treatment. Vertical faces get
            // scene-independent topology sampled from the same canonical voxel snapshot used to
            // extract the face, so the shader can localize lip, impact, and side-edge responses.
            if (faceAxis == 1)
                return 0u;

            uint flags = 0u;
            if (!IsWaterAt(batch, localCell + new int3(0, 1, 0)))
                flags |= SmoothSurfaceVertex.WaterLipFlag;
            if (!IsWaterAt(batch, localCell + new int3(0, -1, 0)))
                flags |= SmoothSurfaceVertex.WaterImpactFlag;

            int tangentAxis = faceAxis == 0 ? 2 : 0;
            int3 tangent = int3.zero;
            tangent[tangentAxis] = 1;
            if (!IsWaterAt(batch, localCell - tangent)
                || !IsWaterAt(batch, localCell + tangent))
                flags |= SmoothSurfaceVertex.WaterEdgeFlag;
            return flags;
        }

        private bool IsWaterAt(int batch, int3 localCell) =>
            IsWater(SampleMaterial(batch, localCell));

        private byte SampleMaterial(int batch, int3 localCell)
        {
            int outsideAxis = -1;
            int outsideSign = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                if ((uint)localCell[axis] < Edge)
                    continue;
                if (localCell[axis] != -1 && localCell[axis] != Edge)
                    return 0;
                if (outsideAxis >= 0)
                    return 0;
                outsideAxis = axis;
                outsideSign = localCell[axis] < 0 ? -1 : 1;
            }

            int snapshotBase = batch * SnapshotStride;
            if (outsideAxis < 0)
            {
                int index = localCell.x + localCell.y * Edge + localCell.z * Edge * Edge;
                return SnapshotMaterials[snapshotBase + index];
            }

            int axisA = (outsideAxis + 1) % 3;
            int axisB = (outsideAxis + 2) % 3;
            if ((uint)localCell[axisA] >= Edge || (uint)localCell[axisB] >= Edge)
                return 0;
            int face = outsideAxis * 2 + (outsideSign > 0 ? 1 : 0);
            int faceBase = snapshotBase + VoxelsPerBrick + face * FaceArea;
            return SnapshotMaterials[faceBase + localCell[axisA] + localCell[axisB] * Edge];
        }

        private static int3 SourceCell(int axis, int axisA, int axisB,
                                       int layer, int a, int b)
        {
            int3 cell = int3.zero;
            cell[axis] = layer;
            cell[axisA] = a;
            cell[axisB] = b;
            return cell;
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
        private bool IsWater(byte material) =>
            material < 32 && (WaterMaterialMask & (1u << material)) != 0;
    }
}
