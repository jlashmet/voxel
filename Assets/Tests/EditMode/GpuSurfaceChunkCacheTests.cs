using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuSurfaceChunkCacheTests
    {
        [Test]
        public void DensityBrickInvalidationMapsNegativeCoordinatesAndSharedPairs()
        {
            using var cache = new GpuSurfaceChunkCache
            {
                MaxBuildsPerFrame = 16,
                MaxResidentChunks = 16
            };
            cache.InvalidateDensityBricks(new List<int3>
            {
                new(0, 0, 0), new(1, 0, 0), new(4, 0, 0),
                new(-1, 0, 0), new(-4, 0, 0), new(-5, 0, 0)
            });

            cache.Prepare(Vector3.zero, 0.1f, 1);
            Assert.AreEqual(4, cache.KnownCount,
                "Four world bricks share one 32-voxel mid-field chunk on either side of zero.");
            Assert.AreEqual(4, cache.Scheduled.Count);

            var coordinates = new HashSet<int3>();
            foreach (var entry in cache.Scheduled) coordinates.Add(entry.Coordinate);
            CollectionAssert.AreEquivalent(new[]
            {
                new int3(-2, 0, 0), new int3(-1, 0, 0),
                new int3(0, 0, 0), new int3(1, 0, 0)
            }, coordinates);
        }

        [Test]
        public void FullCacheKeepsNearestSetWithoutDistantChunkThrashing()
        {
            using var cache = new GpuSurfaceChunkCache
            {
                MaxBuildsPerFrame = 8,
                MaxResidentChunks = 2
            };
            cache.InvalidateDensityBricks(new List<int3>
            {
                new(0, 0, 0), new(4, 0, 0), new(40, 0, 0)
            });

            cache.Prepare(Vector3.zero, 0.1f, 1);
            Assert.AreEqual(2, cache.ResidentCount);
            Assert.AreEqual(2, cache.Scheduled.Count);

            cache.Prepare(Vector3.zero, 0.1f, 2);
            Assert.Zero(cache.Scheduled.Count,
                "A farther pending chunk must not replace a nearer resident every frame.");

            cache.Prepare(new Vector3(32f, 0f, 0f), 0.1f, 3);
            Assert.AreEqual(1, cache.Scheduled.Count,
                "Moving the camera should admit the now-near pending chunk.");
            Assert.AreEqual(new int3(10, 0, 0), cache.Scheduled[0].Coordinate);
        }

        [Test]
        public void AdjacentChunksOwnConsecutiveLatticeEdgeRanges()
        {
            using var cache = new GpuSurfaceChunkCache
            {
                MaxBuildsPerFrame = 4,
                MaxResidentChunks = 4
            };
            cache.InvalidateDensityBricks(new List<int3>
            {
                new(0, 0, 0), new(4, 0, 0)
            });
            cache.Prepare(Vector3.zero, 0.1f, 1);

            GpuSurfaceChunkCache.Entry left = null;
            GpuSurfaceChunkCache.Entry right = null;
            foreach (var entry in cache.Scheduled)
            {
                if (entry.Coordinate.x == 0) left = entry;
                if (entry.Coordinate.x == 1) right = entry;
            }
            Assert.NotNull(left);
            Assert.NotNull(right);
            Assert.AreEqual(32, right.SampleOrigin.x - left.SampleOrigin.x);
            Assert.AreEqual(18, cache.GridSamplesPerAxis);

            // With a -2 voxel halo and p in [1, 16], the left owns sampled lattice edges
            // [0, 30]; the right begins at 32. Both sample the shared dual cell data.
            int leftFirstOwnedEdge = left.SampleOrigin.x + cache.SourceStep;
            int leftLastOwnedEdge = left.SampleOrigin.x
                                  + 16 * cache.SourceStep;
            int rightFirstOwnedEdge = right.SampleOrigin.x + cache.SourceStep;
            Assert.AreEqual(leftLastOwnedEdge + cache.SourceStep,
                            rightFirstOwnedEdge);
        }

        /// <summary>
        /// Exactly one level must own each patch of ground. Deciding independently at both levels
        /// left the transition band drawing a coarse chunk and the fine chunks inside it at once,
        /// at mismatched resolutions, z-fighting along every shared face.
        /// </summary>
        [Test]
        public void ExactlyOneLevelDrawsEachChunkOfGround()
        {
            // Bricks 0..3 on each axis are one 32-voxel coarse chunk and eight 16-voxel fine ones.
            var bricks = new List<int3>();
            for (int z = 0; z < 4; z++)
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                        bricks.Add(new int3(x, y, z));

            using var fine = new GpuSurfaceChunkCache(2, 1)
            {
                MaxBuildsPerFrame = 4,
                MaxResidentChunks = 16
            };
            using var coarse = new GpuSurfaceChunkCache
            {
                MaxBuildsPerFrame = 4,
                MaxResidentChunks = 16,
                Finer = fine
            };
            fine.Coarser = coarse;

            coarse.InvalidateDensityBricks(bricks);
            fine.InvalidateDensityBricks(bricks);
            coarse.Prepare(Vector3.zero, VoxelSize, 1);

            var cameraObject = new GameObject("partition-test-camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(1.6f, 1.6f, -10f);
                camera.transform.LookAt(new Vector3(1.6f, 1.6f, 1.6f));

                Assert.AreEqual(1, coarse.CollectVisible(camera, VoxelSize, 1).Count,
                    "With no fine chunks ready the coarse level is the only surface there is.");
                Assert.Zero(fine.CollectVisible(camera, VoxelSize, 1).Count,
                    "The finer level must not draw where its parent did not retire.");

                // Partial coverage keeps the coarse chunk, or the half the fine level has not
                // reached becomes a hole straight through the world.
                fine.Prepare(Vector3.zero, VoxelSize, 1);
                Assert.AreEqual(4, fine.ResidentCount);
                Assert.AreEqual(1, coarse.CollectVisible(camera, VoxelSize, 2).Count,
                    "Partial finer coverage must not retire the coarse chunk.");
                Assert.Zero(fine.CollectVisible(camera, VoxelSize, 2).Count);

                fine.Prepare(Vector3.zero, VoxelSize, 2);
                Assert.AreEqual(8, fine.ResidentCount);
                Assert.Zero(coarse.CollectVisible(camera, VoxelSize, 3).Count,
                    "Once every child can take over, the coarse chunk must stand down.");
                Assert.AreEqual(8, fine.CollectVisible(camera, VoxelSize, 3).Count,
                    "The finer level takes over exactly the ground the coarse chunk gave up.");

                // Out of the finer level's range there is nothing to hand over to, so the coarse
                // chunk must keep drawing and the finer level must stay silent. Residency alone
                // retiring the parent is what punched holes across the terrain in motion.
                fine.MaxDistance = 0.01f;
                Assert.AreEqual(1, coarse.CollectVisible(camera, VoxelSize, 4).Count,
                    "A finer level that cannot draw must not retire the coarse chunk.");
                Assert.Zero(fine.CollectVisible(camera, VoxelSize, 4).Count);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void CoverageSuppressionIsInertWithoutAFinerLevel()
        {
            using var cache = new GpuSurfaceChunkCache
            {
                MaxBuildsPerFrame = 4,
                MaxResidentChunks = 4
            };
            cache.InvalidateDensityBricks(new List<int3> { new(0, 0, 0) });
            cache.Prepare(Vector3.zero, VoxelSize, 1);

            var cameraObject = new GameObject("no-finer-test-camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(1.6f, 1.6f, -10f);
                camera.transform.LookAt(new Vector3(1.6f, 1.6f, 1.6f));
                Assert.AreEqual(1, cache.CollectVisible(camera, VoxelSize, 1).Count);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        private const float VoxelSize = 0.1f;
    }
}
