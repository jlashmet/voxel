using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Parallel continuous-cell polygonisation over an immutable density snapshot.
    /// Every cell owns a sparse NativeStream lane, so workers never contend and capacity cannot
    /// truncate a chunk silently. Memory is proportional to emitted surface geometry rather than
    /// chunk volume; the main thread reads lanes in deterministic cell order before publication.
    /// </summary>
    [BurstCompile]
    internal struct TransvoxelTopologyJob : IJobParallelFor
    {
        public const int MaxVerticesPerCell = 15;
        public const int MaxIndicesPerCell = 15;

        [ReadOnly] public NativeArray<float> Density;
        [ReadOnly] public NativeArray<byte> Materials;
        [ReadOnly] public NativeArray<uint> SurfaceSemantics;
        [ReadOnly] public NativeArray<byte> BoundarySamples;
        [ReadOnly] public NativeArray<byte> CellClass;
        [ReadOnly] public NativeArray<byte> GeometryCounts;
        [ReadOnly] public NativeArray<byte> CellVertexIndices;
        [ReadOnly] public NativeArray<ushort> EdgeCodes;
        public SurfaceCatalogueView Catalogue;
        public CoatingCatalogueView Coatings;
        public int3 ChunkOriginVoxel;
        public int CellsPerAxis;
        public int GridSize;
        public int Padding;
        public int SourceStep;
        public float VoxelSize;

        public NativeStream.Writer Output;

        public void Execute(int cellIndex)
        {
            Output.BeginForEachIndex(cellIndex);
            int x = cellIndex % CellsPerAxis;
            int y = (cellIndex / CellsPerAxis) % CellsPerAxis;
            int z = cellIndex / (CellsPerAxis * CellsPerAxis);
            int3 cell = new(x, y, z);
            FixedList128Bytes<float> densities = default;
            FixedList64Bytes<byte> materials = default;
            FixedList128Bytes<uint> surfaces = default;
            FixedList64Bytes<byte> boundaries = default;
            int caseCode = 0;
            bool authoredBoundary = false;
            bool displacedCoating = false;
            for (int i = 0; i < 8; i++)
            {
                int3 grid = cell + Padding + Corner(i);
                int sample = GridIndex(grid);
                float density = Density[sample];
                byte material = Materials[sample];
                uint surface = SurfaceSemantics[sample];
                byte boundary = BoundarySamples[sample];
                densities.Add(density);
                materials.Add(material);
                surfaces.Add(surface);
                boundaries.Add(boundary);
                authoredBoundary |= boundary != 0;
                displacedCoating |= Coatings.Get((byte)(surface >> 16)).Displacement != 0;
                if (density < 0f) caseCode |= 1 << i;
            }

            if (caseCode == 0 || caseCode == 255) { WriteEmpty(); return; }
            bool continuous = false;
            bool planar = false;
            bool rounded = false;
            for (int i = 0; i < 8; i++)
            {
                if (!IsSolid(materials[i])) continue;
                SurfaceStyleReadDefinition definition = Catalogue.Get((ushort)surfaces[i]);
                bool include = definition.Reconstruction == SurfaceReconstruction.Smooth
                    || definition.Reconstruction == SurfaceReconstruction.Rounded
                    || definition.Reconstruction == SurfaceReconstruction.Planar
                       && (authoredBoundary || displacedCoating);
                if (!include) continue;
                continuous = true;
                if (definition.Reconstruction == SurfaceReconstruction.Planar) planar = true;
                else rounded = true;
            }
            if (!continuous) { WriteEmpty(); return; }

            int cellClass = CellClass[caseCode];
            byte counts = GeometryCounts[cellClass];
            int sourceVertexCount = counts >> 4;
            int indexCount = (counts & 0x0f) * 3;
            bool flatPlanar = UsesFlatTriangleNormals(planar, rounded, authoredBoundary);
            int outputVertexCount = flatPlanar ? indexCount : sourceVertexCount;
            if (outputVertexCount > MaxVerticesPerCell || indexCount > MaxIndicesPerCell)
            {
                Output.Write((byte)1);
                Output.Write((byte)0);
                Output.Write((byte)0);
                Output.EndForEachIndex();
                return;
            }

            FixedList512Bytes<SmoothSurfaceVertex> cellVertices = default;
            for (int i = 0; i < sourceVertexCount; i++)
            {
                ushort edge = EdgeCodes[caseCode * 12 + i];
                int c0 = (edge >> 4) & 0x0f;
                int c1 = edge & 0x0f;
                float d0 = densities[c0];
                float d1 = densities[c1];
                int3 o0 = Corner(c0);
                int3 o1 = Corner(c1);
                int3 delta = math.abs(o1 - o0);
                int axis = delta.x != 0 ? 0 : delta.y != 0 ? 1 : 2;
                var b0 = new VoxelBoundarySample { Packed = boundaries[c0] };
                var b1 = new VoxelBoundarySample { Packed = boundaries[c1] };
                bool extrusionCapEdge = (b0.IsAuthored && !b0.AppliesAlong(axis))
                    || (b1.IsAuthored && !b1.AppliesAlong(axis));
                if (extrusionCapEdge)
                {
                    d0 = IsSolid(materials[c0]) ? 0.5f : -0.5f;
                    d1 = IsSolid(materials[c1]) ? 0.5f : -0.5f;
                }
                float t0 = math.abs(d1 - d0) > 1e-7f ? d1 / (d1 - d0) : 0.5f;
                float t1 = 1f - t0;
                float3 local = ((float3)(cell + o0) * t0
                              + (float3)(cell + o1) * t1) * SourceStep;

                // The combined authored SDF on a front/back cap is min(profile, depth). At the
                // cap layer depth is always +0.5 voxel, which can hide a profile distance well over
                // one voxel and flatten the X/Y gradient to zero. For a cell already proven to be
                // on the in-plane analytic perimeter, recover the same extrusion-invariant profile
                // from up to two samples inward, where depth no longer wins the min(). Keep the
                // extrusion coordinate on the exact occupancy half-step and move only transverse
                // coordinates to that recovered profile zero crossing.
                if (extrusionCapEdge)
                {
                    bool c0Solid = IsSolid(materials[c0]);
                    int3 solidOffset = c0Solid ? o0 : o1;
                    int3 airOffset = c0Solid ? o1 : o0;
                    VoxelBoundarySample solidBoundary = c0Solid ? b0 : b1;
                    int3 solidGrid = cell + Padding + solidOffset;
                    int inwardDirection = airOffset[axis] > solidOffset[axis] ? -1 : 1;

                    int3 inwardGrid1 = solidGrid;
                    inwardGrid1[axis] = math.clamp(
                        inwardGrid1[axis] + inwardDirection, 0, GridSize - 1);
                    int3 inwardGrid2 = solidGrid;
                    inwardGrid2[axis] = math.clamp(
                        inwardGrid2[axis] + inwardDirection * 2, 0, GridSize - 1);
                    var inwardBoundary1 = new VoxelBoundarySample
                    {
                        Packed = BoundarySamples[GridIndex(inwardGrid1)]
                    };
                    var inwardBoundary2 = new VoxelBoundarySample
                    {
                        Packed = BoundarySamples[GridIndex(inwardGrid2)]
                    };

                    bool inPlaneBoundary = HasInPlaneOccupancyTransition(solidGrid, axis);
                    VoxelBoundarySample profileBoundary = ResolveExtrusionCapProfileSample(
                        solidBoundary, inwardBoundary1, inwardBoundary2,
                        axis, inPlaneBoundary);
                    if (profileBoundary.IsAuthored)
                    {
                        int3 profileGrid = solidGrid;
                        if (IsCompatibleExtrusionProfileSample(inwardBoundary2, axis)
                            && inwardBoundary2.Packed == profileBoundary.Packed)
                            profileGrid = inwardGrid2;
                        else if (IsCompatibleExtrusionProfileSample(inwardBoundary1, axis)
                                 && inwardBoundary1.Packed == profileBoundary.Packed)
                            profileGrid = inwardGrid1;

                        local = ProjectExtrusionCapProfile(
                            local, axis, profileBoundary, DensityNormal(profileGrid));
                    }
                }

                float3 position = (ChunkOriginVoxel + local + 0.5f) * VoxelSize;
                float3 normal = math.normalizesafe(DensityNormal(cell + Padding + o0) * t0
                                                  + DensityNormal(cell + Padding + o1) * t1,
                                                    new float3(0f, 1f, 0f));
                int selected = d0 > d1 ? c0 : c1;
                byte material = materials[selected];
                uint surface = surfaces[selected];
                if (!IsSolid(material))
                {
                    for (int corner = 0; corner < 8; corner++)
                    {
                        if (!IsSolid(materials[corner])) continue;
                        material = materials[corner];
                        surface = surfaces[corner];
                        break;
                    }
                }
                cellVertices.Add(new SmoothSurfaceVertex
                {
                    Position = (Vector3)position,
                    Normal = (Vector3)normal,
                    Material = Pack(material, surface),
                    Active = 0x0000FF00u,
                });
            }

            Output.Write((byte)0);
            Output.Write((byte)outputVertexCount);
            Output.Write((byte)indexCount);
            if (flatPlanar)
            {
                for (int i = 0; i < indexCount; i += 3)
                {
                    int tableBase = cellClass * MaxIndicesPerCell;
                    SmoothSurfaceVertex a = cellVertices[CellVertexIndices[tableBase + i]];
                    SmoothSurfaceVertex b = cellVertices[CellVertexIndices[tableBase + i + 1]];
                    SmoothSurfaceVertex c = cellVertices[CellVertexIndices[tableBase + i + 2]];
                    float3 face = math.normalizesafe(math.cross(
                        (float3)b.Position - (float3)a.Position,
                        (float3)c.Position - (float3)a.Position), new float3(0f, 1f, 0f));
                    float3 expected = (float3)a.Normal + (float3)b.Normal + (float3)c.Normal;
                    if (math.dot(face, expected) < 0f) face = -face;
                    a.Normal = b.Normal = c.Normal = (Vector3)face;
                    Output.Write(a);
                    Output.Write(b);
                    Output.Write(c);
                }
                for (byte i = 0; i < indexCount; i++) Output.Write(i);
            }
            else
            {
                for (int i = 0; i < sourceVertexCount; i++)
                    Output.Write(cellVertices[i]);
                int tableBase = cellClass * MaxIndicesPerCell;
                for (int i = 0; i < indexCount; i++)
                    Output.Write(CellVertexIndices[tableBase + i]);
            }
            Output.EndForEachIndex();
        }

        internal static bool UsesFlatTriangleNormals(bool planar, bool rounded, bool authoredBoundary)
            => planar && !rounded && !authoredBoundary;

        internal static bool IsExtrusionCapRimSample(
            VoxelBoundarySample boundary, int edgeAxis) =>
            boundary.IsAuthored
            && boundary.ExtrusionAxis == edgeAxis
            && boundary.SignedQ3 >= 0
            && boundary.SignedQ3 < 4;

        internal static VoxelBoundarySample ResolveExtrusionCapProfileSample(
            VoxelBoundarySample capBoundary,
            VoxelBoundarySample inwardBoundary1,
            VoxelBoundarySample inwardBoundary2,
            int edgeAxis,
            bool inPlaneBoundary)
        {
            if (!IsCompatibleExtrusionProfileSample(capBoundary, edgeAxis)) return default;
            if (!IsExtrusionCapRimSample(capBoundary, edgeAxis) && !inPlaneBoundary) return default;

            VoxelBoundarySample best = capBoundary;
            if (IsCompatibleExtrusionProfileSample(inwardBoundary1, edgeAxis)
                && inwardBoundary1.SignedQ3 >= best.SignedQ3)
                best = inwardBoundary1;
            if (IsCompatibleExtrusionProfileSample(inwardBoundary2, edgeAxis)
                && inwardBoundary2.SignedQ3 >= best.SignedQ3)
                best = inwardBoundary2;
            return best;
        }

        internal static float3 ProjectExtrusionCapProfile(
            float3 local, int edgeAxis, VoxelBoundarySample profileBoundary, float3 densityNormal)
        {
            if (!IsCompatibleExtrusionProfileSample(profileBoundary, edgeAxis)) return local;
            densityNormal[edgeAxis] = 0f;
            float lengthSq = math.lengthsq(densityNormal);
            if (lengthSq <= 1e-8f) return local;
            float distance = profileBoundary.SignedQ3 * 0.125f;
            return local + densityNormal * math.rsqrt(lengthSq) * distance;
        }

        internal static float3 ProjectExtrusionCapRim(
            float3 local, int edgeAxis, VoxelBoundarySample boundary, float3 densityNormal)
        {
            if (!IsExtrusionCapRimSample(boundary, edgeAxis)) return local;
            return ProjectExtrusionCapProfile(local, edgeAxis, boundary, densityNormal);
        }

        private static bool IsCompatibleExtrusionProfileSample(
            VoxelBoundarySample boundary, int edgeAxis) =>
            boundary.IsAuthored
            && boundary.ExtrusionAxis == edgeAxis
            && boundary.SignedQ3 >= 0;

        private bool HasInPlaneOccupancyTransition(int3 grid, int faceAxis)
        {
            int axisA = (faceAxis + 1) % 3;
            int axisB = (faceAxis + 2) % 3;
            for (int b = -1; b <= 1; b++)
            for (int a = -1; a <= 1; a++)
            {
                if (a == 0 && b == 0) continue;
                int3 neighbour = grid;
                neighbour[axisA] += a;
                neighbour[axisB] += b;
                if (!IsSolid(Materials[GridIndex(neighbour)])) return true;
            }
            return false;
        }

        private void WriteEmpty()
        {
            Output.Write((byte)1);
            Output.Write((byte)0);
            Output.Write((byte)0);
            Output.EndForEachIndex();
        }

        private int GridIndex(int3 grid) => grid.x + GridSize * (grid.y + GridSize * grid.z);

        private float3 DensityNormal(int3 grid)
        {
            float x = Density[GridIndex(math.clamp(grid + new int3(-1, 0, 0), 0, GridSize - 1))]
                    - Density[GridIndex(math.clamp(grid + new int3(1, 0, 0), 0, GridSize - 1))];
            float y = Density[GridIndex(math.clamp(grid + new int3(0, -1, 0), 0, GridSize - 1))]
                    - Density[GridIndex(math.clamp(grid + new int3(0, 1, 0), 0, GridSize - 1))];
            float z = Density[GridIndex(math.clamp(grid + new int3(0, 0, -1), 0, GridSize - 1))]
                    - Density[GridIndex(math.clamp(grid + new int3(0, 0, 1), 0, GridSize - 1))];
            return math.normalizesafe(new float3(x, y, z), new float3(0f, 1f, 0f));
        }

        private static int3 Corner(int index) => new(
            index & 1, (index >> 2) & 1, (index >> 1) & 1);

        private static bool IsSolid(byte material) =>
            material != 0 && material != 11 && material != 16;

        private static uint Pack(byte material, uint surface) => material
            | (((surface >> 16) & 0xffu) << 8)
            | ((surface & 0xffu) << 16)
            | (((surface >> 24) & 0xffu) << 24);
    }
}
