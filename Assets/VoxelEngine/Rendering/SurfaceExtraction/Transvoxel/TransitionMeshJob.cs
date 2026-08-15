using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Meshes the transition cells on one face of a chunk that borders a finer LOD ring.
    ///
    /// <para><b>Why this exists.</b> Two chunks meshed at different sample spacings disagree
    /// about where the surface crosses their shared face: the finer chunk interpolates between
    /// samples the coarse chunk never took, so its boundary vertices land off the coarse
    /// chunk's edge and the two surfaces separate. That gap is a crack you can see through. A
    /// transition cell reads the shared face at the *finer* neighbour's spacing and welds the
    /// two boundaries into one surface.</para>
    ///
    /// <para><b>The thirteen samples.</b> Each transition cell covers one coarse cell of the
    /// face. Samples 0-8 are a 3x3 grid at <em>half</em> this ring's stride — the finer
    /// neighbour's spacing, and the reason this job needs its own face snapshot rather than the
    /// chunk lattice, which simply does not contain those positions. Samples 9-12 are the four
    /// coarse corners; they coincide with samples 0, 2, 6 and 8 in space but their vertices are
    /// displaced inward, giving the cell the depth that connects it to the regular cells
    /// behind it. The nine-bit case code is built from the high-resolution samples alone.</para>
    ///
    /// <para><b>Orientation.</b> The tables describe one canonical face. This job maps the
    /// canonical (u, v, w) frame onto whichever of the six faces is being stitched, so one
    /// table set serves all of them. <c>w</c> always points into the chunk interior.</para>
    /// </summary>
    [BurstCompile]
    internal struct TransitionMeshJob : IJob
    {
        /// <summary>
        /// Face snapshot at half this ring's stride, (2*CellsPerAxis+1)² samples, row-major in
        /// (u, v). Positive means solid, matching the chunk lattice's sign convention.
        /// </summary>
        [ReadOnly] public NativeArray<float> FaceDensity;
        [ReadOnly] public NativeArray<byte> FaceMaterials;
        [ReadOnly] public NativeArray<uint> FaceSurfaces;
        /// <summary>Samples per axis in the face snapshot: 2*CellsPerAxis + 1.</summary>
        public int FaceSamplesPerAxis;

        [ReadOnly] public NativeArray<byte> TransitionCellClass;
        [ReadOnly] public NativeArray<byte> TransitionGeometryCounts;
        [ReadOnly] public NativeArray<byte> TransitionCellIndices;
        [ReadOnly] public NativeArray<ushort> TransitionVertexData;
        public int VertexDataStride;
        public int CellIndexStride;

        public NativeList<SmoothSurfaceVertex> Vertices;
        public NativeList<uint> Indices;

        public int3 ChunkOriginVoxel;
        public int CellsPerAxis;
        public int SourceStep;
        public float VoxelSize;
        /// <summary>0=-X, 1=+X, 2=-Y, 3=+Y, 4=-Z, 5=+Z.</summary>
        public int Face;

        public void Execute()
        {
            int axis = Face >> 1;
            bool positive = (Face & 1) != 0;

            int3 uAxis, vAxis, wAxis;
            switch (axis)
            {
                case 0:
                    uAxis = new int3(0, 1, 0); vAxis = new int3(0, 0, 1);
                    wAxis = new int3(positive ? -1 : 1, 0, 0);
                    break;
                case 1:
                    uAxis = new int3(0, 0, 1); vAxis = new int3(1, 0, 0);
                    wAxis = new int3(0, positive ? -1 : 1, 0);
                    break;
                default:
                    uAxis = new int3(1, 0, 0); vAxis = new int3(0, 1, 0);
                    wAxis = new int3(0, 0, positive ? -1 : 1);
                    break;
            }

            // Voxel offset of the face plane within the chunk.
            float3 facePlane = positive
                ? (float3)(-(float3)wAxis) * (CellsPerAxis * SourceStep)
                : float3.zero;

            for (int v = 0; v < CellsPerAxis; v++)
            for (int u = 0; u < CellsPerAxis; u++)
                EmitTransitionCell(u, v, uAxis, vAxis, wAxis, facePlane);
        }

        private void EmitTransitionCell(int cellU, int cellV, int3 uAxis, int3 vAxis, int3 wAxis,
                                        float3 facePlane)
        {
            // Samples 0-8: the 3x3 half-stride grid over this coarse cell.
            var densities = new FixedList128Bytes<float>();
            var materials = new FixedList128Bytes<byte>();
            var surfaces = new FixedList512Bytes<uint>();

            int caseCode = 0;
            for (int i = 0; i < 9; i++)
            {
                int du = i % 3;
                int dv = i / 3;
                int index = FaceIndex(cellU * 2 + du, cellV * 2 + dv);
                float density = FaceDensity[index];
                densities.Add(density);
                materials.Add(FaceMaterials[index]);
                surfaces.Add(FaceSurfaces[index]);
                if (density > 0f) caseCode |= 1 << i;
            }

            // Samples 9-12: the coarse corners, spatially coincident with 0, 2, 6, 8.
            // Their values come from the same field, so the two sides agree on the surface
            // crossing; only their vertex placement differs, being pushed inward.
            AppendCoarseCorner(ref densities, ref materials, ref surfaces, cellU, cellV, 0, 0);
            AppendCoarseCorner(ref densities, ref materials, ref surfaces, cellU, cellV, 2, 0);
            AppendCoarseCorner(ref densities, ref materials, ref surfaces, cellU, cellV, 0, 2);
            AppendCoarseCorner(ref densities, ref materials, ref surfaces, cellU, cellV, 2, 2);

            byte cellClass = TransitionCellClass[caseCode];
            int classIndex = cellClass & 0x7F;
            bool reversed = (cellClass & 0x80) != 0;

            byte counts = TransitionGeometryCounts[classIndex];
            int vertexCount = counts >> 4;
            int triangleCount = counts & 0x0F;
            if (triangleCount == 0) return;

            // Depth of the transition slab. The tables express every offset in half-cell
            // units, and place samples 9-12 at w = 2 — two half-cells, so one whole coarse
            // cell inward. Halving this would fold the slab's inner wall into the face and
            // collapse the cell's depth, which is the surface that joins it to the regular
            // cells behind it.
            float slabDepth = SourceStep;

            var emitted = new FixedList512Bytes<uint>();
            for (int i = 0; i < vertexCount; i++)
            {
                ushort descriptor = TransitionVertexData[caseCode * VertexDataStride + i];
                int c0 = (descriptor >> 4) & 0x0F;
                int c1 = descriptor & 0x0F;

                float d0 = densities[c0];
                float d1 = densities[c1];
                float t0 = math.abs(d1 - d0) > 1e-7f ? d1 / (d1 - d0) : 0.5f;
                float t1 = 1f - t0;

                float3 p0 = SamplePosition(c0, cellU, cellV, uAxis, vAxis, wAxis,
                                           facePlane, slabDepth);
                float3 p1 = SamplePosition(c1, cellU, cellV, uAxis, vAxis, wAxis,
                                           facePlane, slabDepth);
                float3 local = p0 * t0 + p1 * t1;
                float3 position = (ChunkOriginVoxel + local + 0.5f) * VoxelSize;

                int selected = d0 > d1 ? c0 : c1;
                byte material = materials[selected];
                uint surface = surfaces[selected];

                emitted.Add((uint)Vertices.Length);
                Vertices.Add(new SmoothSurfaceVertex
                {
                    Position = (UnityEngine.Vector3)position,
                    Normal = (UnityEngine.Vector3)(float3)math.normalizesafe(
                        -(float3)wAxis, new float3(0f, 1f, 0f)),
                    Material = PackSurfaceAttributes(material, surface),
                    Active = 0x0000FF00u,
                });
            }

            for (int t = 0; t < triangleCount; t++)
            {
                int a = TransitionCellIndices[classIndex * CellIndexStride + t * 3 + 0];
                int b = TransitionCellIndices[classIndex * CellIndexStride + t * 3 + 1];
                int c = TransitionCellIndices[classIndex * CellIndexStride + t * 3 + 2];
                if (a >= emitted.Length || b >= emitted.Length || c >= emitted.Length) continue;

                Indices.Add(emitted[a]);
                Indices.Add(reversed ? emitted[c] : emitted[b]);
                Indices.Add(reversed ? emitted[b] : emitted[c]);
            }
        }

        private void AppendCoarseCorner(ref FixedList128Bytes<float> densities,
                                        ref FixedList128Bytes<byte> materials,
                                        ref FixedList512Bytes<uint> surfaces,
                                        int cellU, int cellV, int du, int dv)
        {
            int index = FaceIndex(cellU * 2 + du, cellV * 2 + dv);
            densities.Add(FaceDensity[index]);
            materials.Add(FaceMaterials[index]);
            surfaces.Add(FaceSurfaces[index]);
        }

        /// <summary>
        /// Chunk-local voxel position of a transition sample. Samples 0-8 sit on the face
        /// plane; 9-12 are pushed <paramref name="slabDepth"/> into the chunk, which is what
        /// gives the transition cell the depth that joins it to the regular cells.
        /// </summary>
        private float3 SamplePosition(int sample, int cellU, int cellV,
                                      int3 uAxis, int3 vAxis, int3 wAxis,
                                      float3 facePlane, float slabDepth)
        {
            int du, dv;
            bool coarse = sample >= 9;
            if (coarse)
            {
                int corner = sample - 9;
                du = (corner & 1) * 2;
                dv = ((corner >> 1) & 1) * 2;
            }
            else
            {
                du = sample % 3;
                dv = sample / 3;
            }

            // Half-stride units along the face.
            float half = SourceStep * 0.5f;
            float3 onFace = facePlane
                          + (float3)uAxis * ((cellU * 2 + du) * half)
                          + (float3)vAxis * ((cellV * 2 + dv) * half);
            return coarse ? onFace + (float3)wAxis * slabDepth : onFace;
        }

        private int FaceIndex(int u, int v)
        {
            int cu = math.clamp(u, 0, FaceSamplesPerAxis - 1);
            int cv = math.clamp(v, 0, FaceSamplesPerAxis - 1);
            return cu + FaceSamplesPerAxis * cv;
        }

        private static uint PackSurfaceAttributes(byte material, uint surface) =>
            material
            | (((surface >> 16) & 0xFFu) << 8)
            | ((surface & 0xFFu) << 16)
            | (((surface >> 24) & 0xFFu) << 24);
    }
}
