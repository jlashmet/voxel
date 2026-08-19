using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Runs the CPU transition mesher so the GPU port can be checked against it.
    ///
    /// Transition cells are where a GPU port is most likely to be quietly wrong and least likely to
    /// be caught by eye: they are a thin slab at a ring boundary, and getting the axis frame, the
    /// inverted sign convention or the winding wrong produces a seam that looks like the LOD popping
    /// it was meant to hide. So the comparison runs the real <see cref="TransitionMeshJob"/>.
    ///
    /// The face snapshot is built here rather than taken from the GPU. Feeding the GPU's own snapshot
    /// to both sides would make a sampling bug invisible — both meshers would agree on the same wrong
    /// input — so the caller asserts the two snapshots match and then meshes from this one.
    ///
    /// Not part of the frame path: it allocates and blocks, and exists for verification.
    /// </summary>
    public static class CpuTransitionOracle
    {
        /// <summary>
        /// Samples one face at the finer neighbour's spacing, given a world occupancy function.
        ///
        /// Mirrors <c>CpuTransvoxelChunkCache.StepTransitionFaceSnapshot</c>. The addressing is
        /// restated because that method reads through a region source the oracle has no way to
        /// supply; what is under test is the mesher below, not this.
        ///
        /// One deliberate difference: solidity comes from <see cref="TransvoxelDensityJob"/>'s
        /// predicate rather than storage occupancy. The cache's snapshot takes the occupancy bit,
        /// which counts materials 11 and 16 as solid where the regular density path does not, so
        /// the two disagree for exactly those materials. The transition slab has to weld to the
        /// regular surface, so it follows the regular path's answer here.
        /// </summary>
        public static void SampleFace(int3 chunkOriginVoxel, int cellsPerAxis, int sourceStep,
                                      int face, Func<int3, byte> sampleMaterial,
                                      in MaterialPaletteView palette,
                                      float[] density, byte[] materials, uint[] surfaces)
        {
            if (sampleMaterial == null) throw new ArgumentNullException(nameof(sampleMaterial));

            int axis = face >> 1;
            bool positive = (face & 1) != 0;
            int3 uAxis, vAxis;
            switch (axis)
            {
                case 0: uAxis = new int3(0, 1, 0); vAxis = new int3(0, 0, 1); break;
                case 1: uAxis = new int3(0, 0, 1); vAxis = new int3(1, 0, 0); break;
                default: uAxis = new int3(1, 0, 0); vAxis = new int3(0, 1, 0); break;
            }

            int voxelsPerAxis = cellsPerAxis * sourceStep;
            int3 faceOrigin = chunkOriginVoxel;
            if (positive) faceOrigin[axis] += voxelsPerAxis;

            int halfStep = math.max(1, sourceStep / 2);
            int samplesPerAxis = cellsPerAxis * 2 + 1;

            for (int index = 0; index < samplesPerAxis * samplesPerAxis; index++)
            {
                int u = index % samplesPerAxis;
                int v = index / samplesPerAxis;
                int3 voxel = faceOrigin + uAxis * (u * halfStep) + vAxis * (v * halfStep);

                byte material = sampleMaterial(voxel);
                bool occupied = TransvoxelDensityJob.IsSolidSample(material);

                density[index] = occupied ? 0.5f : -0.5f;
                materials[index] = occupied ? material : VoxelGrid.MaterialEmpty;
                surfaces[index] = occupied ? palette.GetDefaultSurfaceStyle(material) : 0u;
            }
        }

        /// <summary>Meshes one face's transition cells from a snapshot and returns its triangles.</summary>
        public static List<OracleTriangle> MeshFace(
            int3 chunkOriginVoxel, int cellsPerAxis, int sourceStep, float voxelSize, int face,
            float[] density, byte[] materials, uint[] surfaces)
        {
            int samplesPerAxis = cellsPerAxis * 2 + 1;
            int samples = samplesPerAxis * samplesPerAxis;

            var tables = new TransvoxelLookupTables();
            var faceDensity = new NativeArray<float>(samples, Allocator.TempJob);
            var faceMaterials = new NativeArray<byte>(samples, Allocator.TempJob);
            var faceSurfaces = new NativeArray<uint>(samples, Allocator.TempJob);
            var vertices = new NativeList<SmoothSurfaceVertex>(1024, Allocator.TempJob);
            var indices = new NativeList<uint>(1024, Allocator.TempJob);

            try
            {
                faceDensity.CopyFrom(density);
                faceMaterials.CopyFrom(materials);
                faceSurfaces.CopyFrom(surfaces);

                var job = new TransitionMeshJob
                {
                    FaceDensity = faceDensity,
                    FaceMaterials = faceMaterials,
                    FaceSurfaces = faceSurfaces,
                    FaceSamplesPerAxis = samplesPerAxis,
                    TransitionCellClass = tables.TransitionCellClass,
                    TransitionGeometryCounts = tables.TransitionGeometryCounts,
                    TransitionCellIndices = tables.TransitionCellIndices,
                    TransitionVertexData = tables.TransitionVertexData,
                    VertexDataStride = tables.TransitionVertexStride,
                    CellIndexStride = tables.TransitionIndexStride,
                    Vertices = vertices,
                    Indices = indices,
                    ChunkOriginVoxel = chunkOriginVoxel,
                    CellsPerAxis = cellsPerAxis,
                    SourceStep = sourceStep,
                    VoxelSize = voxelSize,
                    Face = face,
                };
                job.Execute();

                var triangles = new List<OracleTriangle>(indices.Length / 3);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                    triangles.Add(new OracleTriangle(
                        (float3)(UnityEngine.Vector3)vertices[(int)indices[i]].Position,
                        (float3)(UnityEngine.Vector3)vertices[(int)indices[i + 1]].Position,
                        (float3)(UnityEngine.Vector3)vertices[(int)indices[i + 2]].Position));
                return triangles;
            }
            finally
            {
                faceDensity.Dispose();
                faceMaterials.Dispose();
                faceSurfaces.Dispose();
                vertices.Dispose();
                indices.Dispose();
                tables.Dispose();
            }
        }
    }
}
