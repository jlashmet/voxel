using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Greedy faceted mesher for feature-preserving coarse block summaries. The job operates on a
    /// padded brick grid but emits only the core. Every 8^3 source block contributes a 2^3 grid of
    /// 4^3-voxel HLOD subcells; large coplanar faces merge while holes remain explicit.
    ///
    /// Output capacity is fixed by the caller. Overflow aborts the job and is reported explicitly;
    /// streaming must retry/backpressure rather than allocating on the frame path.
    /// </summary>
    [BurstCompile]
    public struct SurfaceBlockHlodMeshJob : IJob
    {
        public const int SubcellsPerBrickAxis = 2;
        public const int SubcellVoxelEdge = 4;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        [ReadOnly] public NativeArray<SurfaceBlockHlodSummary> Summaries;
        public int SummaryGridEdge;
        public int PaddingBricks;
        public int CoreBrickEdge;
        public int3 CoreOriginVoxel;
        public float VoxelSize;

        public NativeArray<byte> MaskScratch;
        public NativeList<SmoothSurfaceVertex> Vertices;
        public NativeList<uint> Indices;
        public NativeArray<int> Overflow;

        public void Execute()
        {
            Overflow[0] = 0;
            Vertices.Clear();
            Indices.Clear();

            int subcellEdge = CoreBrickEdge * SubcellsPerBrickAxis;
            int faceArea = subcellEdge * subcellEdge;
            if (MaskScratch.Length < faceArea)
            {
                Overflow[0] = 1;
                return;
            }

            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                for (int layer = 0; layer < subcellEdge; layer++)
                {
                    BuildMask(axis, axisA, axisB, sign, layer, subcellEdge);
                    if (!MergeMask(axis, axisA, axisB, sign, layer, subcellEdge))
                        return;
                }
            }
        }

        private void BuildMask(int axis, int axisA, int axisB, int sign,
                               int layer, int subcellEdge)
        {
            for (int b = 0; b < subcellEdge; b++)
            for (int a = 0; a < subcellEdge; a++)
            {
                int3 cell = int3.zero;
                cell[axis] = layer;
                cell[axisA] = a;
                cell[axisB] = b;
                byte material = SampleMaterial(cell);
                if (material == 0)
                {
                    MaskScratch[a + b * subcellEdge] = 0;
                    continue;
                }

                int3 neighbour = cell;
                neighbour[axis] += sign;
                MaskScratch[a + b * subcellEdge] = SampleMaterial(neighbour) == 0
                    ? material : (byte)0;
            }
        }

        private bool MergeMask(int axis, int axisA, int axisB, int sign,
                               int layer, int subcellEdge)
        {
            for (int b = 0; b < subcellEdge; b++)
            for (int a = 0; a < subcellEdge; a++)
            {
                byte material = MaskScratch[a + b * subcellEdge];
                if (material == 0) continue;

                int width = 1;
                while (a + width < subcellEdge
                       && MaskScratch[a + width + b * subcellEdge] == material)
                    width++;

                int height = 1;
                bool extend = true;
                while (b + height < subcellEdge && extend)
                {
                    for (int k = 0; k < width; k++)
                    {
                        if (MaskScratch[a + k + (b + height) * subcellEdge] == material)
                            continue;
                        extend = false;
                        break;
                    }
                    if (extend) height++;
                }

                for (int hb = 0; hb < height; hb++)
                for (int ha = 0; ha < width; ha++)
                    MaskScratch[a + ha + (b + hb) * subcellEdge] = 0;

                if (!EmitQuad(material, axis, axisA, axisB, sign,
                              layer, a, b, width, height))
                    return false;
            }
            return true;
        }

        private bool EmitQuad(byte material, int axis, int axisA, int axisB, int sign,
                              int layer, int a, int b, int width, int height)
        {
            if (Vertices.Length + 4 > Vertices.Capacity
                || Indices.Length + 6 > Indices.Capacity)
            {
                Overflow[0] = 1;
                return false;
            }

            int plane = layer + (sign > 0 ? 1 : 0);
            int3 v0 = CoreOriginVoxel;
            int3 v1 = CoreOriginVoxel;
            int3 v2 = CoreOriginVoxel;
            int3 v3 = CoreOriginVoxel;
            v0[axis] += plane * SubcellVoxelEdge;
            v1[axis] += plane * SubcellVoxelEdge;
            v2[axis] += plane * SubcellVoxelEdge;
            v3[axis] += plane * SubcellVoxelEdge;
            v0[axisA] += a * SubcellVoxelEdge;
            v0[axisB] += b * SubcellVoxelEdge;
            v1[axisA] += (a + width) * SubcellVoxelEdge;
            v1[axisB] += b * SubcellVoxelEdge;
            v2[axisA] += (a + width) * SubcellVoxelEdge;
            v2[axisB] += (b + height) * SubcellVoxelEdge;
            v3[axisA] += a * SubcellVoxelEdge;
            v3[axisB] += (b + height) * SubcellVoxelEdge;

            float3 p0 = (float3)v0 * VoxelSize;
            float3 p1 = (float3)v1 * VoxelSize;
            float3 p2 = (float3)v2 * VoxelSize;
            float3 p3 = (float3)v3 * VoxelSize;
            float3 normal = float3.zero;
            normal[axis] = sign;

            uint baseVertex = (uint)Vertices.Length;
            Vertices.AddNoResize(Vertex(p0, normal, material));
            Vertices.AddNoResize(Vertex(p1, normal, material));
            Vertices.AddNoResize(Vertex(p2, normal, material));
            Vertices.AddNoResize(Vertex(p3, normal, material));

            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                Indices.AddNoResize(baseVertex);
                Indices.AddNoResize(baseVertex + 2);
                Indices.AddNoResize(baseVertex + 1);
                Indices.AddNoResize(baseVertex);
                Indices.AddNoResize(baseVertex + 3);
                Indices.AddNoResize(baseVertex + 2);
            }
            else
            {
                Indices.AddNoResize(baseVertex);
                Indices.AddNoResize(baseVertex + 1);
                Indices.AddNoResize(baseVertex + 2);
                Indices.AddNoResize(baseVertex);
                Indices.AddNoResize(baseVertex + 2);
                Indices.AddNoResize(baseVertex + 3);
            }
            return true;
        }

        private byte SampleMaterial(int3 coreSubcell)
        {
            int3 brick = new(
                FloorDiv(coreSubcell.x, SubcellsPerBrickAxis) + PaddingBricks,
                FloorDiv(coreSubcell.y, SubcellsPerBrickAxis) + PaddingBricks,
                FloorDiv(coreSubcell.z, SubcellsPerBrickAxis) + PaddingBricks);
            if (math.any(brick < 0) || math.any(brick >= SummaryGridEdge)) return 0;

            int lx = FloorMod(coreSubcell.x, SubcellsPerBrickAxis);
            int ly = FloorMod(coreSubcell.y, SubcellsPerBrickAxis);
            int lz = FloorMod(coreSubcell.z, SubcellsPerBrickAxis);
            int subcell = lx | (ly << 1) | (lz << 2);
            int index = brick.x + SummaryGridEdge
                      * (brick.y + SummaryGridEdge * brick.z);
            SurfaceBlockHlodSummary summary = Summaries[index];
            return summary.IsOccupied(subcell) ? summary.MaterialAt(subcell) : (byte)0;
        }

        private static SmoothSurfaceVertex Vertex(float3 position, float3 normal, byte material) =>
            new()
            {
                Position = new Vector3(position.x, position.y, position.z),
                Normal = new Vector3(normal.x, normal.y, normal.z),
                Material = material,
                Active = FullyLitOcclusion,
            };

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
