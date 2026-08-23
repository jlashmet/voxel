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

        [Test]
        public void BayDeepOpeningCarveDoesNotAuthorSpringHalfPlane()
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

                Primitive deepOpening = default;
                bool found = false;
                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    if (primitive.Shape != PrimitiveShape.Annulus
                        || primitive.Mode != PrimitiveMode.Carve
                        || primitive.Radius != arch.ClearSpan / 2
                        || primitive.B.z - primitive.A.z + 1 != arch.Depth)
                        continue;

                    Assert.False(found, "Arch bay should emit exactly one deep circular clear-opening carve.");
                    deepOpening = primitive;
                    found = true;
                }

                Assert.True(found, "Arch bay must emit the deep circular clear-opening carve.");
                Assert.AreEqual(PrismProfile.Gable, deepOpening.Profile,
                    "deep opening must be a full disk: the lower rectangular carve already owns the lower half, so an Arch profile would add a false spring-plane boundary");
            }
            finally
            {
                primitives.Dispose();
            }
        }

        [Test]
        public void BayImpostsDoNotIntrudeIntoClearOpeningBelowSpring()
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

            int3 origin = int3.zero;
            var primitives = new NativeList<Primitive>(bay.Metadata.MaxPrimitives, Allocator.Temp);
            var table = new RegionTable(8, Allocator.Temp);
            var pool = new BrickPool(24_000, Allocator.Temp);
            try
            {
                Assert.True(bay.Emit(origin, primitives));
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), origin, origin + bay.Metadata.Footprint,
                    reads, mutations);

                int archOriginX = bay.ShoulderWidth;
                int leftClearX = archOriginX + arch.RingThickness;
                int rightClearX = archOriginX + arch.Width - arch.RingThickness - 1;
                int sampleY = arch.PierHeight - 1;
                int sampleZ = 1 + arch.Depth / 2;

                VoxelCell left = VoxelAccess.GetCell(
                    ref table, in pool, new int3(leftClearX, sampleY, sampleZ));
                VoxelCell right = VoxelAccess.GetCell(
                    ref table, in pool, new int3(rightClearX, sampleY, sampleZ));

                Assert.False(left.IsSolid,
                    "left impost may project into the shoulder but must not create a shelf inside the clear opening");
                Assert.False(right.IsSolid,
                    "right impost may project into the shoulder but must not create a shelf inside the clear opening");
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                primitives.Dispose();
            }
        }
    }
}
