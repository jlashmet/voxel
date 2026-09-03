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
            using var brickBases = new NativeArray<int3>(new[] { new int3(-8, -8, -8) }, Allocator.Temp);
            var snapshotData = new byte[WaterBrickMeshBatchJob.SnapshotStride];
            snapshotData[0] = GameMaterialIds.Water;
            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(256, Allocator.Temp);
            using var indices = new NativeList<uint>(384, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            Execute(brickBases, snapshots, scratch, vertices, indices, overflow,
                1u << GameMaterialIds.Water);

            Assert.That(overflow[0], Is.Zero);
            int canonicalVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                SmoothSurfaceVertex vertex = vertices[i];
                Assert.That(vertex.Material & SmoothSurfaceVertex.BaseMaterialMask,
                    Is.EqualTo((uint)GameMaterialIds.Water));
                if ((vertex.Material & SmoothSurfaceVertex.WaterSprayFlag) != 0)
                    continue;

                canonicalVertices++;
                Assert.That(vertex.Position.x, Is.InRange(-8f, -7f));
                Assert.That(vertex.Position.y, Is.InRange(-8f, -7f));
                Assert.That(vertex.Position.z, Is.InRange(-8f, -7f));
            }

            Assert.That(canonicalVertices, Is.EqualTo(24),
                "An isolated voxel must retain all six canonical faces; optional impact spray is supplemental geometry.");
        }

        [Test]
        public void Execute_VerticalCascadeColumnEmitsReusableFallingSheetFaces()
        {
            using var brickBases = new NativeArray<int3>(new[] { int3.zero }, Allocator.Temp);
            var snapshotData = new byte[WaterBrickMeshBatchJob.SnapshotStride];
            for (int y = 0; y < 4; y++)
                snapshotData[y * WaterBrickMeshBatchJob.Edge] = GameMaterialIds.Cascade;
            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(256, Allocator.Temp);
            using var indices = new NativeList<uint>(384, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            Execute(brickBases, snapshots, scratch, vertices, indices, overflow,
                1u << GameMaterialIds.Cascade);

            Assert.That(overflow[0], Is.Zero);

            int canonicalVertices = 0;
            int verticalVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                SmoothSurfaceVertex vertex = vertices[i];
                Assert.That(vertex.Material & SmoothSurfaceVertex.BaseMaterialMask,
                    Is.EqualTo((uint)GameMaterialIds.Cascade));
                if ((vertex.Material & SmoothSurfaceVertex.WaterSprayFlag) != 0)
                    continue;

                canonicalVertices++;
                if (math.abs(vertex.Normal.y) < 0.5f)
                    verticalVertices++;
            }

            Assert.That(canonicalVertices, Is.EqualTo(24),
                "A vertical cascade column must retain four canonical vertical sheet quads plus top/bottom faces.");
            Assert.That(verticalVertices, Is.EqualTo(16),
                "Canonical extraction must expose both sides of a vertical waterfall sheet for the shared shader.");
        }

        [Test]
        public void Execute_ReciprocalBoundarySnapshotsSuppressInternalSeamAndKeepProfilesDistinct()
        {
            using var brickBases = new NativeArray<int3>(new[]
            {
                new int3(-8, 0, 0),
                new int3(0, 0, 0),
            }, Allocator.Temp);
            var snapshotData = new byte[2 * WaterBrickMeshBatchJob.SnapshotStride];

            int leftLocal = 7; // x=7,y=0,z=0 -> world x=-1
            int rightBase = WaterBrickMeshBatchJob.SnapshotStride;
            snapshotData[leftLocal] = GameMaterialIds.RiverWater;
            snapshotData[rightBase] = GameMaterialIds.Cascade;

            int leftPositiveXFace = WaterBrickMeshBatchJob.VoxelsPerBrick + WaterBrickMeshBatchJob.FaceArea;
            int rightNegativeXFace = rightBase + WaterBrickMeshBatchJob.VoxelsPerBrick;
            snapshotData[leftPositiveXFace] = GameMaterialIds.Cascade;
            snapshotData[rightNegativeXFace] = GameMaterialIds.RiverWater;

            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(512, Allocator.Temp);
            using var indices = new NativeList<uint>(768, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            uint waterMask = (1u << GameMaterialIds.Water)
                           | (1u << GameMaterialIds.RiverWater)
                           | (1u << GameMaterialIds.Cascade);
            Execute(brickBases, snapshots, scratch, vertices, indices, overflow, waterMask);

            Assert.That(overflow[0], Is.Zero);

            int canonicalVertices = 0;
            int riverVertices = 0;
            int cascadeVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                SmoothSurfaceVertex vertex = vertices[i];
                if ((vertex.Material & SmoothSurfaceVertex.WaterSprayFlag) != 0)
                    continue;

                canonicalVertices++;
                uint material = vertex.Material & SmoothSurfaceVertex.BaseMaterialMask;
                if (material == GameMaterialIds.RiverWater) riverVertices++;
                if (material == GameMaterialIds.Cascade) cascadeVertices++;
            }

            Assert.That(canonicalVertices, Is.EqualTo(40),
                "Two adjacent boundary voxels must retain five canonical faces each, with no reciprocal seam quads; impact spray is supplemental.");
            Assert.That(riverVertices, Is.EqualTo(20));
            Assert.That(cascadeVertices, Is.EqualTo(20));
        }

        [Test]
        public void Execute_MaterialOutsideInstalledWaterMaskDoesNotRenderAsWater()
        {
            using var brickBases = new NativeArray<int3>(1, Allocator.Temp);
            var snapshotData = new byte[WaterBrickMeshBatchJob.SnapshotStride];
            snapshotData[0] = GameMaterialIds.Stone;
            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(32, Allocator.Temp);
            using var indices = new NativeList<uint>(48, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

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
