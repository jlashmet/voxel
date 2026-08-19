using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ExactSnapshotClassificationTests
    {
        /// <summary>Owned solid, continuous topology, and GPU-unsupported content. The job writes
        /// every one of them, so a caller that sizes this array to the flags it happens to read
        /// indexes past the end.</summary>
        private const int ClassificationFlagCount = 3;
        private const int BrickEdge = 3;
        private const int CoreBrickIndex = 1 + BrickEdge * (1 + BrickEdge);
        private const int OffLatticeVoxelIndex = 1 | (1 << 3) | (1 << 6);

        [Test]
        public void OffLatticeSolidInsideOwnedCoreSetsOwnedSolidFlag()
        {
            var bricks = new NativeArray<TransvoxelDensityBrick>(
                BrickEdge * BrickEdge * BrickEdge, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var voxels = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var surfaces = new NativeArray<ushort>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var boundaries = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var flags = new NativeArray<byte>(ClassificationFlagCount, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            try
            {
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
                Assert.AreEqual(1, flags[2],
                    "Authored profile blocks are not represented by the compute mesher, so a chunk "
                  + "carrying them must stay on the CPU path.");
            }
            finally
            {
                flags.Dispose();
                boundaries.Dispose();
                surfaces.Dispose();
                voxels.Dispose();
                bricks.Dispose();
            }
        }

        [Test]
        public void OffLatticeSolidInPaddingHaloDoesNotClaimCoreOwnership()
        {
            var bricks = new NativeArray<TransvoxelDensityBrick>(
                BrickEdge * BrickEdge * BrickEdge, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var voxels = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var surfaces = new NativeArray<ushort>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var boundaries = new NativeArray<byte>(512, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            var flags = new NativeArray<byte>(ClassificationFlagCount, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            try
            {
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
            finally
            {
                flags.Dispose();
                boundaries.Dispose();
                surfaces.Dispose();
                voxels.Dispose();
                bricks.Dispose();
            }
        }

        private static void RunClassification(
            NativeArray<TransvoxelDensityBrick> bricks,
            NativeArray<byte> voxels,
            NativeArray<ushort> surfaces,
            NativeArray<byte> boundaries,
            NativeArray<byte> flags)
        {
            // HasProfiles=true pre-sets the continuous-topology and GPU-unsupported bits, so this
            // regression stays isolated to core ownership. The profile bit must not manufacture or
            // suppress the owned-solid flag.
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