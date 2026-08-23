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

        [Test]
        public void BayLowerOpeningCarveSpansExactlyBetweenStructuralPiers()
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
            var bay = new ArchBayFeatureDefinition
            {
                Arch = arch,
                ShoulderWidth = 10,
                TopMargin = 8,
                FaceRecess = 1,
                PlinthHeight = 4,
                ImpostHeight = 3,
                Damage = ArchRuinDamage.Intact,
                DamageSeed = 0x2222u,
                DamageScale = 2,
            };
            var primitives = new NativeList<Primitive>(bay.Metadata.MaxPrimitives, Allocator.Temp);
            try
            {
                Assert.True(bay.Emit(int3.zero, primitives));

                int archOriginX = bay.ShoulderWidth;
                int expectedMinX = archOriginX + arch.RingThickness;
                int expectedMaxX = archOriginX + arch.Width - arch.RingThickness - 1;
                Primitive opening = default;
                bool found = false;
                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    if (primitive.Shape != PrimitiveShape.Box || primitive.Mode != PrimitiveMode.Carve)
                        continue;
                    if (primitive.A.y != 0 || primitive.B.y != arch.PierHeight)
                        continue;
                    if (primitive.B.z - primitive.A.z + 1 != arch.Depth)
                        continue;

                    Assert.False(found, "Arch bay should emit exactly one full-height lower opening box carve.");
                    opening = primitive;
                    found = true;
                }

                Assert.True(found, "Arch bay must carve the lower clear opening before restoring the structural arch.");
                Assert.AreEqual(expectedMinX, opening.A.x,
                    "lower opening must begin immediately inside the left structural pier");
                Assert.AreEqual(expectedMaxX, opening.B.x,
                    "lower opening must end immediately inside the right structural pier");
                Assert.AreEqual(arch.Width - arch.RingThickness * 2, opening.B.x - opening.A.x + 1,
                    "lower opening carve must span the complete pier-to-pier gap without leaving one-cell strips");
            }
            finally
            {
                primitives.Dispose();
            }
        }
    }
}
