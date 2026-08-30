using Game.Materials.Api;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WaterBrickMeshBatchJobTests
    {
        [Test]
        public void Execute_PreservesWaterMaterialIdentityAtNegativeCoordinates()
        {
            using var brickBases = new NativeArray<int3>(1, Allocator.Temp);
            using var snapshots = new NativeArray<byte>(WaterBrickMeshBatchJob.SnapshotStride, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(64, Allocator.Temp);
            using var indices = new NativeList<uint>(96, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            brickBases[0] = new int3(-8, -8, -8);
            snapshots[0] = GameMaterialIds.Water;

            Execute(brickBases, snapshots, scratch, vertices, indices, overflow,
                1u << GameMaterialIds.Water);

            Assert.That(overflow[0], Is.Zero);
            Assert.That(vertices.Length, Is.EqualTo(24), "An isolated voxel must expose all six faces.");
            Assert.That(indices.Length, Is.EqualTo(36));
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(vertices[i].Material, Is.EqualTo(GameMaterialIds.Water));
                Assert.That(vertices[i].Position.x, Is.InRange(-8f, -7f));
                Assert.That(vertices[i].Position.y, Is.InRange(-8f, -7f));
                Assert.That(vertices[i].Position.z, Is.InRange(-8f, -7f));
            }
        }

        [Test]
        public void Execute_ReciprocalBoundarySnapshotsSuppressInternalSeamAndKeepProfilesDistinct()
        {
            using var brickBases = new NativeArray<int3>(2, Allocator.Temp);
            using var snapshots = new NativeArray<byte>(2 * WaterBrickMeshBatchJob.SnapshotStride, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(128, Allocator.Temp);
            using var indices = new NativeList<uint>(192, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            brickBases[0] = new int3(-8, 0, 0);
            brickBases[1] = new int3(0, 0, 0);

            int leftLocal = 7; // x=7,y=0,z=0 -> world x=-1
            int rightBase = WaterBrickMeshBatchJob.SnapshotStride;
            snapshots[leftLocal] = GameMaterialIds.RiverWater;
            snapshots[rightBase] = GameMaterialIds.Cascade;

            int leftPositiveXFace = WaterBrickMeshBatchJob.VoxelsPerBrick + WaterBrickMeshBatchJob.FaceArea;
            int rightNegativeXFace = rightBase + WaterBrickMeshBatchJob.VoxelsPerBrick;
            snapshots[leftPositiveXFace] = GameMaterialIds.Cascade;
            snapshots[rightNegativeXFace] = GameMaterialIds.RiverWater;

            uint waterMask = (1u << GameMaterialIds.Water)
                           | (1u << GameMaterialIds.RiverWater)
                           | (1u << GameMaterialIds.Cascade);
            Execute(brickBases, snapshots, scratch, vertices, indices, overflow, waterMask);

            Assert.That(overflow[0], Is.Zero);
            Assert.That(vertices.Length, Is.EqualTo(40),
                "Two adjacent boundary voxels must emit five faces each, with no reciprocal seam quads.");
            Assert.That(indices.Length, Is.EqualTo(60));

            int riverVertices = 0;
            int cascadeVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].Material == GameMaterialIds.RiverWater) riverVertices++;
                if (vertices[i].Material == GameMaterialIds.Cascade) cascadeVertices++;
            }

            Assert.That(riverVertices, Is.EqualTo(20));
            Assert.That(cascadeVertices, Is.EqualTo(20));
        }

        [Test]
        public void Execute_MaterialOutsideInstalledWaterMaskDoesNotRenderAsWater()
        {
            using var brickBases = new NativeArray<int3>(1, Allocator.Temp);
            using var snapshots = new NativeArray<byte>(WaterBrickMeshBatchJob.SnapshotStride, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(32, Allocator.Temp);
            using var indices = new NativeList<uint>(48, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            snapshots[0] = GameMaterialIds.Stone;
            Execute(brickBases, snapshots, scratch, vertices, indices, overflow,
                1u << GameMaterialIds.Water);

            Assert.That(vertices.Length, Is.Zero);
            Assert.That(indices.Length, Is.Zero);
        }

        private static void Execute(
            NativeArray<int3> brickBases,
            NativeArray<byte> snapshots,
            NativeArray<byte> scratch,
            NativeList<SmoothSurfaceVertex> vertices,
            NativeList<uint> indices,
            NativeArray<int> overflow,
            uint waterMask)
        {
            var job = new WaterBrickMeshBatchJob
            {
                BrickBaseVoxels = brickBases,
                SnapshotMaterials = snapshots,
                WaterMaterialMask = waterMask,
                BatchCount = brickBases.Length,
                VoxelSize = 1f,
                MaskScratch = scratch,
                Vertices = vertices,
                Indices = indices,
                Overflow = overflow,
            };
            job.Execute();
        }
    }
}
