using System;
using System.IO;
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

        [Test]
        public void MixedContinuousChunksUseSnapshotOccupancyForFacetedFaces()
        {
            string cache = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets",
                "VoxelEngine",
                "Rendering",
                "Runtime",
                "SurfaceExtraction",
                "CpuTransvoxelChunkCache.cs"));

            const string topologySchedule =
                "ScheduleTopologyJob(voxelSize, _densityJobHandle);";
            int topology = cache.IndexOf(topologySchedule, StringComparison.Ordinal);
            Assert.That(topology, Is.GreaterThanOrEqualTo(0),
                "The mixed continuous extraction path must still schedule topology from density.");

            int length = Math.Min(320, cache.Length - topology);
            string mixedScheduling = cache.Substring(topology, length);
            StringAssert.Contains("ScheduleSnapshotFacetedMaskJob();", mixedScheduling,
                "Mixed Smooth/Rounded + Planar chunks must decide exact faceted exposure from "
              + "authoritative snapshot occupancy, not the presentation-material density lattice.");
            StringAssert.DoesNotContain("ScheduleFacetedMaskJob(_densityJobHandle);", mixedScheduling,
                "Presentation material may be carried onto an air-centered density sample and "
              + "must never suppress an authoritative Planar occupancy face.");
        }

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;
                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        private static int Index(int x, int y, int z, int size) => x + size * (y + size * z);
    }
}
