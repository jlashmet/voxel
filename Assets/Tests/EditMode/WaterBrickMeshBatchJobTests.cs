using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

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
            using var vertices = new NativeList<SmoothSurfaceVertex>(64, Allocator.Temp);
            using var indices = new NativeList<uint>(96, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

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
        public void Execute_VerticalCascadeColumnEmitsReusableFallingSheetFaces()
        {
            using var brickBases = new NativeArray<int3>(new[] { int3.zero }, Allocator.Temp);
            var snapshotData = new byte[WaterBrickMeshBatchJob.SnapshotStride];
            for (int y = 0; y < 4; y++)
                snapshotData[y * WaterBrickMeshBatchJob.Edge] = GameMaterialIds.Cascade;
            using var snapshots = new NativeArray<byte>(snapshotData, Allocator.Temp);
            using var scratch = new NativeArray<byte>(WaterBrickMeshBatchJob.FaceArea, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(64, Allocator.Temp);
            using var indices = new NativeList<uint>(96, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            Execute(brickBases, snapshots, scratch, vertices, indices, overflow,
                1u << GameMaterialIds.Cascade);

            Assert.That(overflow[0], Is.Zero);
            Assert.That(vertices.Length, Is.EqualTo(24),
                "A vertical cascade column must greedily retain four vertical sheet quads plus top/bottom faces.");
            Assert.That(indices.Length, Is.EqualTo(36));

            int verticalVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(vertices[i].Material, Is.EqualTo(GameMaterialIds.Cascade));
                if (math.abs(vertices[i].Normal.y) < 0.5f)
                    verticalVertices++;
            }
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
            using var vertices = new NativeList<SmoothSurfaceVertex>(128, Allocator.Temp);
            using var indices = new NativeList<uint>(192, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

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

        [Test]
        public void SolidClassification_UsesInstalledPresentationMaskForOpaqueWaterId()
        {
            const byte opaqueWaterId = 23;
            const byte opaqueSolidId = 24;
            var water = new WaterPresentationDefinition(
                WaterPresentationProfile.Waterfall,
                shallow: new float4(0.2f, 0.7f, 0.9f, 0.7f),
                deep: new float4(0.02f, 0.15f, 0.3f, 4f),
                flowDirection: new float2(0f, 1f),
                flowSpeed: 1f,
                waveScale: 1f,
                normalStrength: 1f,
                refractionStrength: 0.1f,
                smoothness: 0.8f,
                surfaceFoam: 0.5f,
                contactFoam: 0.5f,
                foamScale: 1f,
                foamSpeed: 1f,
                turbulence: 0.6f,
                edgeFoam: 0.7f,
                impactFoam: 0.8f,
                mist: 0.9f);

            try
            {
                VoxelMaterialPresentationInstaller.Apply(new[]
                {
                    new MaterialPresentationDefinition(
                        opaqueWaterId, new float4(0.1f, 0.4f, 0.7f, 1f), water: water),
                    new MaterialPresentationDefinition(
                        opaqueSolidId, new float4(0.5f, 0.5f, 0.5f, 1f)),
                });

                Assert.That(VoxelPresentationCatalogue.IsWaterMaterial(opaqueWaterId), Is.True);
                Assert.That(SolidMaterialClassification.IsSolid(opaqueWaterId), Is.False,
                    "Solid extraction must derive water exclusion from installed presentation, not game IDs.");
                Assert.That(TransvoxelDensityJob.IsSolidSample(opaqueWaterId), Is.False,
                    "Burst density classification must consume the same presentation-driven water mask.");
                Assert.That(SolidMaterialClassification.IsSolid(opaqueSolidId), Is.True);
                Assert.That(TransvoxelDensityJob.IsSolidSample(opaqueSolidId), Is.True);
            }
            finally
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
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
