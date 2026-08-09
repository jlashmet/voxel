using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// A two-brick proving ground for the smooth render density. It avoids terrain generation,
    /// cameras, URP, and the castle, so failures identify the cache rather than the showcase.
    /// </summary>
    public sealed class GpuDensityFieldTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Shaders/BrickRaymarch.compute";

        [Test]
        public void ThinFeatureSmoothsAcrossBrickBoundaryAndEditInvalidatesNeighbour()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("This graphics device does not support compute shaders.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Missing compute shader at {ShaderPath}");
            int kernel = shader.FindKernel("CSBuildDensity");

            var pool = new BrickPool(8, Allocator.Persistent);
            var table = new RegionTable(1, Allocator.Persistent);
            using var buffers = new VoxelGpuBuffers();

            try
            {
                Region region = table.LoadRegion(int3.zero);
                int leftPool = pool.Allocate();
                int rightPool = pool.Allocate();
                region.SetBrick(0, 0, 0, BrickRef.FromPoolIndex(leftPool));
                region.SetBrick(1, 0, 0, BrickRef.FromPoolIndex(rightPool));

                // One occupied voxel at the right edge is deliberately the hardest useful case:
                // it must survive the 0.5 isolevel and influence the adjacent brick's halo.
                int leftEdgeVoxel = VoxelIndex(7, 4, 4);
                int rightEdgeNeighbour = VoxelIndex(0, 4, 4);
                pool.SetVoxel(leftPool, leftEdgeVoxel, 2);
                table.CommitRegion(region);

                var refresh = new HashSet<int3> { int3.zero };
                buffers.Sync(ref table, ref pool, int3.zero, refresh);
                Assert.AreEqual(2, buffers.DensityJobCount,
                    "Only the two mapped mixed bricks should be rebuilt.");

                DispatchDensity(shader, kernel, buffers);
                var density = ReadDensity(buffers, pool.Capacity);
                Assert.Greater(ReadByte(density, leftPool, leftEdgeVoxel), 127,
                    "A one-voxel authored feature must remain inside the rendered isosurface.");
                Assert.That(ReadByte(density, rightPool, rightEdgeNeighbour), Is.InRange(1, 32),
                    "The adjacent brick should receive a small, non-binary halo value.");
                Assert.Zero(ReadByte(density, rightPool, VoxelIndex(4, 4, 4)),
                    "Density must stay local rather than bleeding through the brick.");

                // Simulate destruction. The right brick did not change authoritatively, but its
                // cached halo did; the uploader must therefore schedule both bricks again.
                pool.SetVoxel(leftPool, leftEdgeVoxel, 0);
                buffers.Sync(ref table, ref pool, int3.zero, new HashSet<int3>());
                Assert.AreEqual(2, buffers.DensityJobCount,
                    "An edge edit must invalidate the neighbouring density brick.");

                DispatchDensity(shader, kernel, buffers);
                density = ReadDensity(buffers, pool.Capacity);
                Assert.Zero(ReadByte(density, leftPool, leftEdgeVoxel));
                Assert.Zero(ReadByte(density, rightPool, rightEdgeNeighbour),
                    "The neighbour halo must not remain stale after destruction.");

                // Terrain intentionally favours the wider field over self-preservation. A lone
                // terrain voxel is visual noise after destruction and should fall below the
                // isolevel, while the authored timber voxel above remained visible.
                pool.SetVoxel(leftPool, leftEdgeVoxel, 5);
                buffers.Sync(ref table, ref pool, int3.zero, new HashSet<int3>());
                DispatchDensity(shader, kernel, buffers);
                density = ReadDensity(buffers, pool.Capacity);
                Assert.That(ReadByte(density, leftPool, leftEdgeVoxel), Is.InRange(1, 126),
                    "An isolated terrain speck should not survive the smooth isolevel.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static void DispatchDensity(ComputeShader shader, int kernel,
                                            VoxelGpuBuffers buffers)
        {
            shader.SetBuffer(kernel, "g_RegionWindow", buffers.WindowBuffer);
            shader.SetBuffer(kernel, "g_BrickRefs", buffers.BrickRefBuffer);
            shader.SetBuffer(kernel, "g_BrickVoxels", buffers.VoxelBuffer);
            shader.SetBuffer(kernel, "g_BrickDensity", buffers.DensityBuffer);
            shader.SetBuffer(kernel, "g_DensityJobs", buffers.DensityJobBuffer);
            shader.SetInt("g_DensityJobCount", buffers.DensityJobCount);
            shader.SetInt("g_WindowX", VoxelGpuBuffers.WindowX);
            shader.SetInt("g_WindowY", VoxelGpuBuffers.WindowY);
            shader.SetInt("g_WindowZ", VoxelGpuBuffers.WindowZ);
            int3 origin = buffers.WindowOrigin;
            shader.SetVector("g_WindowOrigin", new Vector4(origin.x, origin.y, origin.z, 0));
            shader.Dispatch(kernel, buffers.DensityJobCount, 1, 1);
        }

        private static uint[] ReadDensity(VoxelGpuBuffers buffers, int poolCapacity)
        {
            var result = new uint[poolCapacity * 128];
            buffers.DensityBuffer.GetData(result);
            return result;
        }

        private static int VoxelIndex(int x, int y, int z) => x | (y << 3) | (z << 6);

        private static byte ReadByte(uint[] packed, int poolIndex, int voxelIndex)
        {
            uint word = packed[poolIndex * 128 + (voxelIndex >> 2)];
            return (byte)((word >> ((voxelIndex & 3) * 8)) & 0xFFu);
        }
    }
}
