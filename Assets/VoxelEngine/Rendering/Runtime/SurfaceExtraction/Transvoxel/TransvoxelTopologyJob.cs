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
    /// Every scheduled core cell owns a sparse NativeStream lane. Boundary lanes may also emit
    /// cells from the chunk's one-cell negative shell so a continuous surface is owned by the
    /// chunk containing its deterministic inside-density corner.
    /// Workers never contend and capacity cannot truncate a chunk silently. Memory is proportional
    /// to emitted surface geometry rather than chunk volume; the main thread reads lanes in
    /// deterministic cell order before publication.
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

            // The normal core cell covers every interior cell and the chunk's positive faces.
            // A chunk also evaluates the one-cell shell on its negative faces. The ownership test
            // inside PolygoniseCell guarantees exactly one of the two neighbouring chunks emits a
            // shared boundary cell: whichever owns the first inside-density corner in fixed order.
            PolygoniseCell(new int3(x, y, z));
            if (x == 0) PolygoniseCell(new int3(-1, y, z));
            if (y == 0) PolygoniseCell(new int3(x, -1, z));
            if (z == 0) PolygoniseCell(new int3(x, y, -1));
            if (x == 0 && y == 0) PolygoniseCell(new int3(-1, -1, z));
            if (x == 0 && z == 0) PolygoniseCell(new int3(-1, y, -1));
            if (y == 0 && z == 0) PolygoniseCell(new int3(x, -1, -1));
            if (x == 0 && y == 0 && z == 0) PolygoniseCell(new int3(-1, -1, -1));
            Output.EndForEachIndex();
        }

        private void PolygoniseCell(int3 cell)
        {
            FixedList128Bytes<float> densities = default;
            FixedList64Bytes<byte> materials = default;
            FixedList128Bytes<uint> surfaces = default;
            FixedList64Bytes<byte> boundaries = default;
            int caseCode = 0;
            bool authoredBoundary = false;
            bool displacedCoating = false;
            int selectedInsideCorner = -1;
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
                else if (selectedInsideCorner < 0) selectedInsideCorner = i;
            }

            if (caseCode == 0 || caseCode == 255) return;

            // Density sampling carries the dominant render material alongside the scalar field.
            // That material can be Stone even at an AIR-centred, negative-density sample because a
            // neighbouring solid supplies the surface's presentation identity. Ownership must
            // therefore follow the reconstructed field sign, not Materials[]. Otherwise the first
            // outside shell corner can look "solid" and incorrectly reject the solid-side chunk.
            if (!OwnsSelectedInsideSample(cell, selectedInsideCorner, CellsPerAxis)) return;

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
            if (!continuous) return;

            int cellClass = CellClass[caseCode];
            byte counts = GeometryCounts[cellClass];
            int sourceVertexCount = counts >> 4;
            int indexCount = (counts & 0x0f) * 3;
            int outputVertexCount = planar && !rounded ? indexCount : sourceVertexCount;
            if (outputVertexCount > MaxVerticesPerCell || indexCount > MaxIndicesPerCell)
            {
                Output.Write((byte)1);
                Output.Write((byte)0);
                Output.Write((byte)0);
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
                if ((b0.IsAuthored && !b0.AppliesAlong(axis))
                    || (b1.IsAuthored && !b1.AppliesAlong(axis)))
                {
                    d0 = IsSolid(materials[c0]) ? 0.5f : -0.5f;
                    d1 = IsSolid(materials[c1]) ? 0.5f : -0.5f;
                }
                float t0 = math.abs(d1 - d0) > 1e-7f ? d1 / (d1 - d0) : 0.5f;
                float t1 = 1f - t0;
                float3 local = ((float3)(cell + o0) * t0 + (float3)(cell + o1) * t1)
                             * SourceStep;
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
            if (planar && !rounded)
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
        }

        internal static bool OwnsSelectedInsideSample(
            int3 cell, int selectedInsideCorner, int cellsPerAxis)
        {
            if ((uint)selectedInsideCorner >= 8u) return false;
            int3 sample = cell + Corner(selectedInsideCorner);
            int edge = math.max(1, cellsPerAxis);
            return math.all(sample >= 0) && math.all(sample < edge);
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

        private static uint Pack(byte material, uint surface)
        {
            surface = TransvoxelDensityJob.StripAuthoritativeOccupancy(surface);
            return material
                | (((surface >> 16) & 0xffu) << 8)
                | ((surface & 0xffu) << 16)
                | (((surface >> 24) & 0xffu) << 24);
        }
    }
}
