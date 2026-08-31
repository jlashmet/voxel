using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WaterBrickMeshBatchJobTests
    {
        [Test]
        public void FlatWaterTop_EmitsPerVoxelTopQuads_ForWaveDeformation()
        {
            var brickBases = new NativeArray<int3>(1, Allocator.TempJob);
            var materials = new NativeArray<byte>(
                WaterBrickMeshBatchJob.SnapshotStride, Allocator.TempJob,
                NativeArrayOptions.ClearMemory);
            var mask = new NativeArray<byte>(
                WaterBrickMeshBatchJob.FaceArea, Allocator.TempJob,
                NativeArrayOptions.ClearMemory);
            var vertices = new NativeList<SmoothSurfaceVertex>(4096, Allocator.TempJob);
            var indices = new NativeList<uint>(8192, Allocator.TempJob);
            var overflow = new NativeArray<int>(1, Allocator.TempJob,
                NativeArrayOptions.ClearMemory);

            try
            {
                brickBases[0] = int3.zero;
                const byte water = 11;
                for (int z = 0; z < WaterBrickMeshBatchJob.Edge; z++)
                for (int x = 0; x < WaterBrickMeshBatchJob.Edge; x++)
                    materials[x + z * WaterBrickMeshBatchJob.Edge * WaterBrickMeshBatchJob.Edge] = water;

                var job = new WaterBrickMeshBatchJob
                {
                    BrickBaseVoxels = brickBases,
                    SnapshotMaterials = materials,
                    BatchCount = 1,
                    VoxelSize = 1f,
                    MaskScratch = mask,
                    Vertices = vertices,
                    Indices = indices,
                    Overflow = overflow,
                };

                job.Execute();

                Assert.That(overflow[0], Is.Zero, "The minimal flat-water repro must fit the mesh buffers.");

                int upwardVertexCount = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].Normal.y > 0.99f)
                        upwardVertexCount++;
                }

                int expectedTopQuads = WaterBrickMeshBatchJob.Edge * WaterBrickMeshBatchJob.Edge;
                Assert.That(upwardVertexCount, Is.EqualTo(expectedTopQuads * 4),
                    "A flat water brick needs one top quad per voxel so vertex-stage waves have interior geometry; " +
                    "greedily collapsing the entire top to four corner vertices reproduces the planar-slab defect.");
            }
            finally
            {
                overflow.Dispose();
                indices.Dispose();
                vertices.Dispose();
                mask.Dispose();
                materials.Dispose();
                brickBases.Dispose();
            }
        }
    }
}
