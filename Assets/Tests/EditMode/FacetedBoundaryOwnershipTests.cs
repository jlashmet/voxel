using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FacetedBoundaryOwnershipTests
    {
        [Test]
        public void PlanarBackingFaceSurvivesUnrelatedRoundedVeneerHalo()
        {
            const int gridSize = 3;
            var materials = new NativeArray<byte>(gridSize * gridSize * gridSize, Allocator.Temp);
            var surfaces = new NativeArray<uint>(materials.Length, Allocator.Temp);
            var boundaries = new NativeArray<byte>(materials.Length, Allocator.Temp);
            var masks = new NativeArray<uint>(6, Allocator.Temp);
            try
            {
                int backing = Index(1, 1, 1, gridSize);
                int emptyAbove = Index(1, 2, 1, gridSize);
                materials[backing] = 1;
                surfaces[backing] = SurfaceStyles.MasonryJoint;

                // A rounded veneer beside the backing can leave its in-plane halo on this empty
                // cell. That halo describes the veneer, not the backing's exact planar top face.
                boundaries[emptyAbove] =
                    VoxelBoundarySample.FromSignedQ4(-8, extrusionAxis: 2).Packed;

                var job = new FacetedMaskJob
                {
                    Materials = materials,
                    SurfaceSemantics = surfaces,
                    BoundarySamples = boundaries,
                    Catalogue = SurfaceCatalogueView.CreateBuiltIns(),
                    Coatings = default,
                    CellsPerAxis = 1,
                    GridSize = gridSize,
                    Padding = 1,
                    FaceMasks = masks,
                };
                job.Execute(0);

                const int positiveYFace = 3;
                Assert.That(masks[positiveYFace], Is.Not.Zero,
                    "An empty neighbour's unrelated curved halo must not steal ownership of "
                  + "the occupied planar cell's exact exposed face.");
            }
            finally
            {
                masks.Dispose();
                boundaries.Dispose();
                surfaces.Dispose();
                materials.Dispose();
            }
        }

        private static int Index(int x, int y, int z, int size) => x + size * (y + size * z);
    }
}
