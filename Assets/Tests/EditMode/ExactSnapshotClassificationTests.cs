using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ExactSnapshotClassificationTests
    {
        private const int BrickEdge = 3;
        private const int CoreBrickIndex = 1 + BrickEdge * (1 + BrickEdge);
        private const int OffLatticeVoxelIndex = 1 | (1 << 3) | (1 << 6);

        [Test]
        public void OffLatticeSolidInsideOwnedCoreSetsOwnedSolidFlag()
        {
            using var bricks = new NativeArray<TransvoxelDensityBrick>(
                BrickEdge * BrickEdge * BrickEdge, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var voxels = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var surfaces = new NativeArray<ushort>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var boundaries = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var flags = new NativeArray<byte>(2, Allocator.Temp,
                NativeArrayOptions.ClearMemory);

            bricks[CoreBrickIndex] = new TransvoxelDensityBrick
            {
                Kind = 2,
                MixedOffset = 0,
            };
            // This voxel is deliberately between every four-voxel step-4 lattice sample.
            // Exact ownership classification must still see it because routing is based on the
            // immutable core payload, not on the later geometry sampling lattice.
            voxels[OffLatticeVoxelIndex] = 7;

            RunClassification(bricks, voxels, surfaces, boundaries, flags);

            Assert.AreEqual(1, flags[0],
                "An off-lattice solid inside the owned core was lost before step-4 geometry.");
        }

        [Test]
        public void OffLatticeSolidInPaddingHaloDoesNotClaimCoreOwnership()
        {
            using var bricks = new NativeArray<TransvoxelDensityBrick>(
                BrickEdge * BrickEdge * BrickEdge, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var voxels = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var surfaces = new NativeArray<ushort>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var boundaries = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            using var flags = new NativeArray<byte>(2, Allocator.Temp,
                NativeArrayOptions.ClearMemory);

            // Index zero is padding in all three axes for a one-brick core with one-brick halo.
            bricks[0] = new TransvoxelDensityBrick
            {
                Kind = 2,
                MixedOffset = 0,
            };
            voxels[OffLatticeVoxelIndex] = 7;

            RunClassification(bricks, voxels, surfaces, boundaries, flags);

            Assert.AreEqual(0, flags[0],
                "A halo-only solid incorrectly claimed geometry ownership for the core chunk.");
        }

        private static void RunClassification(
            NativeArray<TransvoxelDensityBrick> bricks,
            NativeArray<byte> voxels,
            NativeArray<ushort> surfaces,
            NativeArray<byte> boundaries,
            NativeArray<byte> flags)
        {
            // HasProfiles=true deliberately bypasses surface-style lookup so this regression is
            // isolated to core ownership. The profile bit affects only continuous-topology routing;
            // it must not manufacture or suppress the owned-solid flag.
            var job = new ExactSnapshotClassificationJob
            {
                Bricks = bricks,
                MixedVoxels = voxels,
                MixedSurfaceSemantics = surfaces,
                MixedBoundarySamples = boundaries,
                BrickCacheEdge = BrickEdge,
                BricksPerAxis = 1,
                BrickCachePadding = 1,
                HasProfiles = true,
                Flags = flags,
            };
            job.Execute();
        }
    }
}
