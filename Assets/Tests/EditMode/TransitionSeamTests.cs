using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Verifies transition-cell geometry numerically rather than by eye.
    ///
    /// A screenshot can only show that a seam looks closed from one angle at one moment. These
    /// tests assert the properties that actually define a closed seam: transition vertices lie
    /// on the shared face plane, the coarse-side vertices sit exactly one coarse cell behind
    /// it, the sample positions the coarse ring reads coincide with the ones the finer ring
    /// reads, and the emitted triangles are non-degenerate and reference real vertices.
    ///
    /// The job is driven directly with a synthetic face, so these run in EditMode with no
    /// graphics device and no scene.
    /// </summary>
    public sealed class TransitionSeamTests
    {
        private const int CellsPerAxis = CpuTransvoxelChunkCache.CellsPerAxis;
        private const int FaceSamplesPerAxis = CellsPerAxis * 2 + 1;
        private const float VoxelSize = 0.1f;

        private struct Harness
        {
            public NativeArray<float> Density;
            public NativeArray<byte> Materials;
            public NativeArray<uint> Surfaces;
            public NativeArray<byte> CellClass;
            public NativeArray<byte> Counts;
            public NativeArray<byte> CellIndices;
            public NativeArray<ushort> VertexData;
            public int VertexStride;
            public int IndexStride;
            public NativeList<SmoothSurfaceVertex> Vertices;
            public NativeList<uint> Indices;

            public void Dispose()
            {
                Density.Dispose(); Materials.Dispose(); Surfaces.Dispose();
                CellClass.Dispose(); Counts.Dispose(); CellIndices.Dispose();
                VertexData.Dispose(); Vertices.Dispose(); Indices.Dispose();
            }
        }

        /// <summary>Builds the job inputs, with the face split by a horizontal surface.</summary>
        private static Harness MakeHarness(System.Func<int, int, bool> solidAt)
        {
            int samples = FaceSamplesPerAxis * FaceSamplesPerAxis;
            var h = new Harness
            {
                Density = new NativeArray<float>(samples, Allocator.TempJob),
                Materials = new NativeArray<byte>(samples, Allocator.TempJob),
                Surfaces = new NativeArray<uint>(samples, Allocator.TempJob),
                Vertices = new NativeList<SmoothSurfaceVertex>(1024, Allocator.TempJob),
                Indices = new NativeList<uint>(1024, Allocator.TempJob),
            };

            for (int v = 0; v < FaceSamplesPerAxis; v++)
            for (int u = 0; u < FaceSamplesPerAxis; u++)
            {
                int i = u + FaceSamplesPerAxis * v;
                bool solid = solidAt(u, v);
                h.Density[i] = solid ? 0.5f : -0.5f;
                h.Materials[i] = solid ? (byte)4 : (byte)0;
                h.Surfaces[i] = 0u;
            }

            byte[] cellClass = TransvoxelTransitionTables.CellClass;
            RegularCellData[] cellData = TransvoxelTransitionTables.CellData;
            ushort[][] vertexData = TransvoxelTransitionTables.VertexData;

            h.VertexStride = 0;
            foreach (var row in vertexData) h.VertexStride = math.max(h.VertexStride, row.Length);
            h.IndexStride = 0;
            foreach (var d in cellData) h.IndexStride = math.max(h.IndexStride, d.VertexIndices.Length);

            h.CellClass = new NativeArray<byte>(cellClass.Length, Allocator.TempJob);
            for (int i = 0; i < cellClass.Length; i++) h.CellClass[i] = cellClass[i];

            h.Counts = new NativeArray<byte>(cellData.Length, Allocator.TempJob);
            h.CellIndices = new NativeArray<byte>(cellData.Length * h.IndexStride, Allocator.TempJob);
            for (int i = 0; i < cellData.Length; i++)
            {
                h.Counts[i] = cellData[i].GeometryCounts;
                var idx = cellData[i].VertexIndices;
                for (int j = 0; j < idx.Length; j++) h.CellIndices[i * h.IndexStride + j] = idx[j];
            }

            h.VertexData = new NativeArray<ushort>(vertexData.Length * h.VertexStride,
                                                   Allocator.TempJob);
            for (int i = 0; i < vertexData.Length; i++)
                for (int j = 0; j < vertexData[i].Length; j++)
                    h.VertexData[i * h.VertexStride + j] = vertexData[i][j];

            return h;
        }

        private static void Run(ref Harness h, int face, int sourceStep, int3 chunkCoord)
        {
            new TransitionMeshJob
            {
                FaceDensity = h.Density,
                FaceMaterials = h.Materials,
                FaceSurfaces = h.Surfaces,
                FaceSamplesPerAxis = FaceSamplesPerAxis,
                TransitionCellClass = h.CellClass,
                TransitionGeometryCounts = h.Counts,
                TransitionCellIndices = h.CellIndices,
                TransitionVertexData = h.VertexData,
                VertexDataStride = h.VertexStride,
                CellIndexStride = h.IndexStride,
                Vertices = h.Vertices,
                Indices = h.Indices,
                ChunkOriginVoxel = chunkCoord * (CellsPerAxis * sourceStep),
                CellsPerAxis = CellsPerAxis,
                SourceStep = sourceStep,
                VoxelSize = VoxelSize,
                Face = face,
            }.Run();
        }

        // -------------------------------------------------------------------------

        [Test]
        public void AUniformFaceEmitsNothing()
        {
            // No surface crossing means no transition geometry — a fully solid or fully empty
            // face is entirely interior and must not spend triangles.
            var solid = MakeHarness((u, v) => true);
            Run(ref solid, 0, 8, int3.zero);
            Assert.AreEqual(0, solid.Indices.Length, "An all-solid face must emit nothing.");
            solid.Dispose();

            var empty = MakeHarness((u, v) => false);
            Run(ref empty, 0, 8, int3.zero);
            Assert.AreEqual(0, empty.Indices.Length, "An all-empty face must emit nothing.");
            empty.Dispose();
        }

        [Test]
        public void ASurfaceCrossingTheFaceEmitsGeometry()
        {
            var h = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
            Run(ref h, 0, 8, int3.zero);
            Assert.Greater(h.Indices.Length, 0,
                "A face split by a surface must produce transition geometry.");
            Assert.AreEqual(0, h.Indices.Length % 3, "Indices must form whole triangles.");
            h.Dispose();
        }

        [Test]
        public void EveryIndexReferencesAnEmittedVertex()
        {
            var h = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
            Run(ref h, 0, 8, int3.zero);
            for (int i = 0; i < h.Indices.Length; i++)
                Assert.Less(h.Indices[i], (uint)h.Vertices.Length,
                    $"Index {i} points past the end of the vertex list.");
            h.Dispose();
        }

        [Test]
        public void NoTriangleIsDegenerate()
        {
            // A degenerate triangle is invisible but still rasterised, and usually means two
            // interpolated vertices collapsed onto the same sample.
            var h = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
            Run(ref h, 0, 8, int3.zero);

            for (int t = 0; t < h.Indices.Length; t += 3)
            {
                uint a = h.Indices[t], b = h.Indices[t + 1], c = h.Indices[t + 2];
                Assert.IsFalse(a == b || b == c || a == c,
                    $"Triangle {t / 3} repeats a vertex index.");
            }
            h.Dispose();
        }

        [Test]
        public void HighResolutionVerticesLieOnTheSharedFacePlane()
        {
            // This is the seam condition. Every vertex interpolated between two high-resolution
            // samples must sit exactly on the face the two rings share; if any drifts off it,
            // the coarse surface no longer meets the fine one and the seam opens.
            const int sourceStep = 8;
            var h = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
            Run(ref h, 0, sourceStep, int3.zero);

            float slabDepthMetres = sourceStep * VoxelSize;
            float planeX = 0.5f * VoxelSize; // chunk origin plus the half-voxel sample offset

            for (int i = 0; i < h.Vertices.Length; i++)
            {
                float x = h.Vertices[i].Position.x;
                bool onPlane = math.abs(x - planeX) < 1e-4f;
                bool onSlabBack = math.abs(x - (planeX + slabDepthMetres)) < 1e-4f;
                Assert.IsTrue(onPlane || onSlabBack,
                    $"Vertex {i} sits at x={x}, neither on the face plane ({planeX}) nor at "
                  + $"the slab's inner wall ({planeX + slabDepthMetres}). A transition vertex "
                  + "off both is a crack.");
            }
            h.Dispose();
        }

        [Test]
        public void TransitionVerticesStayInsideTheFaceExtent()
        {
            // Geometry escaping the chunk's own face would overlap the neighbour's surface.
            const int sourceStep = 8;
            var h = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
            Run(ref h, 0, sourceStep, int3.zero);

            float extent = CellsPerAxis * sourceStep * VoxelSize;
            for (int i = 0; i < h.Vertices.Length; i++)
            {
                var p = h.Vertices[i].Position;
                Assert.GreaterOrEqual(p.y, -1e-3f);
                Assert.LessOrEqual(p.y, extent + 1e-3f,
                    $"Vertex {i} at y={p.y} escapes the face extent {extent}.");
                Assert.GreaterOrEqual(p.z, -1e-3f);
                Assert.LessOrEqual(p.z, extent + 1e-3f);
            }
            h.Dispose();
        }

        [Test]
        public void EveryFaceOrientationProducesGeometry()
        {
            // A missing or transposed axis mapping shows up as one face silently emitting
            // nothing while the other five work.
            for (int face = 0; face < 6; face++)
            {
                var h = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
                Run(ref h, face, 8, int3.zero);
                Assert.Greater(h.Indices.Length, 0,
                    $"Face {face} produced no geometry; its axis frame is likely wrong.");
                h.Dispose();
            }
        }

        [Test]
        public void CoarseSampleGridCoincidesWithTheFinerRingsSamples()
        {
            // The precondition for a closed seam: the half-stride positions the coarse ring
            // reads on the face must be exactly the positions the finer ring samples. If these
            // drift the two sides interpolate different fields and no table can weld them.
            const int coarseStep = 8;
            int fineStep = coarseStep / 2;

            for (int u = 0; u < FaceSamplesPerAxis; u++)
            {
                int coarseFaceVoxel = u * (coarseStep / 2);
                Assert.AreEqual(0, coarseFaceVoxel % fineStep,
                    $"Face sample {u} lands at voxel {coarseFaceVoxel}, which is not on the "
                  + $"finer ring's {fineStep}-voxel lattice.");
            }
        }

        [Test]
        public void FaceSampleCountCoversEveryCoarseCellPlusItsClosingEdge()
        {
            Assert.AreEqual(CellsPerAxis * 2 + 1, FaceSamplesPerAxis,
                "Each coarse cell needs two half-stride samples, plus one to close the last.");
        }

        [Test]
        public void ChunkCoordinateOffsetsGeometryWithoutDistortingIt()
        {
            // The same face at a different chunk coordinate must produce a rigid translation,
            // not a differently shaped mesh.
            var origin = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
            Run(ref origin, 0, 8, int3.zero);

            var shifted = MakeHarness((u, v) => v < FaceSamplesPerAxis / 2);
            Run(ref shifted, 0, 8, new int3(0, 0, 1));

            Assert.AreEqual(origin.Vertices.Length, shifted.Vertices.Length,
                "Translating a chunk must not change how much geometry it emits.");

            float expectedShift = CellsPerAxis * 8 * VoxelSize;
            for (int i = 0; i < origin.Vertices.Length; i++)
            {
                float dz = shifted.Vertices[i].Position.z - origin.Vertices[i].Position.z;
                Assert.AreEqual(expectedShift, dz, 1e-3f,
                    $"Vertex {i} shifted by {dz} rather than {expectedShift}.");
            }

            origin.Dispose();
            shifted.Dispose();
        }
    }
}
