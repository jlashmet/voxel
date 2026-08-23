using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchProfileStitchTests
    {
        [Test]
        public void RetainedProfileRadiiMatchStructuralAnnulusZeroes()
        {
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 32,
                PierHeight = 40,
                RingThickness = 7,
                Depth = 12,
                VoussoirCount = 13,
                StoneMaterial = 9,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            var primitives = new NativeList<Primitive>(32, Allocator.Temp);
            var blocks = new ProfileBlockStore();
            try
            {
                Assert.True(arch.Emit(int3.zero, primitives, blocks));
                Assert.AreEqual(arch.VoussoirCount, blocks.Count);

                int expectedInnerQ4 = (arch.ClearSpan / 2) * 16;
                int expectedOuterQ4 = arch.OuterRadius * 16;
                for (int i = 0; i < blocks.Count; i++)
                {
                    ProfileBlock block = blocks[i];
                    Assert.AreEqual(expectedInnerQ4, block.InnerRadiusQ4,
                        $"retained intrados for voussoir {i} must stitch to the structural annulus zero");
                    Assert.AreEqual(expectedOuterQ4, block.OuterRadiusQ4,
                        $"retained extrados for voussoir {i} must stitch to the structural annulus zero");
                }
            }
            finally
            {
                primitives.Dispose();
            }
        }
    }
}
