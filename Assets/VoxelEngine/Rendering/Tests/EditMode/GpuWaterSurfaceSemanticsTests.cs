using Game.Materials.Api;
using VoxelEngine.Rendering.Tests.RuntimeSupport;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuWaterSurfaceSemanticsTests
    {
        [Test]
        public void Execute_PreservesWaterMaterialIdentityAtNegativeCoordinates()
        {
            using var brickBases = new NativeArray<int3>(new[] { new int3(-8, -8, -8) }, Allocator.Temp);
            var snapshotData = new byte[GpuWaterExtractionFixture.SnapshotStride];
            snapshotData[0] = GameMaterialIds.Water;
            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(256, Allocator.Temp);
            using var indices = new NativeList<uint>(384, Allocator.Temp);

            Execute(brickBases, snapshots, vertices, indices,
                1u << GameMaterialIds.Water);

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
            var snapshotData = new byte[GpuWaterExtractionFixture.SnapshotStride];
            for (int y = 0; y < 4; y++)
                snapshotData[y * GpuWaterExtractionFixture.Edge] = GameMaterialIds.Cascade;
            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(256, Allocator.Temp);
            using var indices = new NativeList<uint>(384, Allocator.Temp);

            Execute(brickBases, snapshots, vertices, indices,
                1u << GameMaterialIds.Cascade);


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
            var snapshotData = new byte[2 * GpuWaterExtractionFixture.SnapshotStride];

            int leftLocal = 7; // x=7,y=0,z=0 -> world x=-1
            int rightBase = GpuWaterExtractionFixture.SnapshotStride;
            snapshotData[leftLocal] = GameMaterialIds.RiverWater;
            snapshotData[rightBase] = GameMaterialIds.Cascade;

            int leftPositiveXFace = GpuWaterExtractionFixture.VoxelsPerBrick + GpuWaterExtractionFixture.FaceArea;
            int rightNegativeXFace = rightBase + GpuWaterExtractionFixture.VoxelsPerBrick;
            snapshotData[leftPositiveXFace] = GameMaterialIds.Cascade;
            snapshotData[rightNegativeXFace] = GameMaterialIds.RiverWater;

            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(512, Allocator.Temp);
            using var indices = new NativeList<uint>(768, Allocator.Temp);

            uint waterMask = (1u << GameMaterialIds.Water)
                           | (1u << GameMaterialIds.RiverWater)
                           | (1u << GameMaterialIds.Cascade);
            Execute(brickBases, snapshots, vertices, indices, waterMask);


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
            var snapshotData = new byte[GpuWaterExtractionFixture.SnapshotStride];
            snapshotData[0] = GameMaterialIds.Stone;
            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(32, Allocator.Temp);
            using var indices = new NativeList<uint>(48, Allocator.Temp);

            Execute(brickBases, snapshots, vertices, indices,
                1u << GameMaterialIds.Water);

            Assert.That(vertices.Length, Is.Zero);
            Assert.That(indices.Length, Is.Zero);
        }

        private static void Execute(NativeArray<int3> brickBases, NativeArray<byte> snapshots,
            NativeList<SmoothSurfaceVertex> vertices, NativeList<uint> indices, uint waterMask) =>
            GpuWaterExtractionFixture.Extract(brickBases, snapshots, waterMask, 1f, vertices, indices);
    }
}
