using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureAuthoringFoundationTests
    {
        [Test]
        public void GenerationContextCarriesStableInstanceInputsAndOutputs()
        {
            var anchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            var palette = new StructureMaterialPalette
            {
                Foundation = 1,
                PrimaryWall = 2,
                SecondaryWall = 3,
                Trim = 4,
                Roof = 5,
                Floor = 6,
                Column = 7,
                Accent = 8,
                Underground = 9,
                Opening = 0,
                Glass = 10,
                Detail = 11,
            };

            var context = new StructureGenerationContext(
                1234ul,
                55u,
                7,
                987654321ul,
                new int3(100, 20, 300),
                5,
                new int3(90, 10, 290),
                new int3(170, 100, 370),
                77u,
                in palette,
                anchors);

            Assert.AreEqual(1234ul, context.InstanceId);
            Assert.AreEqual(55u, context.WorldSeed);
            Assert.AreEqual(7, context.DefinitionId);
            Assert.AreEqual(987654321ul, context.InstanceSeed);
            Assert.AreEqual(new int3(100, 20, 300), context.Origin);
            Assert.AreEqual(1, context.Orientation, "orientation must normalize to four cardinal values");
            Assert.IsTrue(context.ContainsWorld(new int3(90, 10, 290)));
            Assert.IsFalse(context.ContainsWorld(new int3(170, 10, 290)), "max bounds are exclusive");
            Assert.AreEqual(5, context.Material(StructureMaterialRole.Roof));
            Assert.AreEqual(context.SampleGround(3, 4), context.SampleGround(3, 4));

            var name = new FixedString32Bytes("MainEntrance");
            Assert.IsTrue(context.TryAddResolvedAnchor(in name, new int3(101, 21, 301), Facing.South));
            Assert.AreEqual(1, anchors.Length);
            Assert.AreEqual(name, anchors[0].Name);
            Assert.AreEqual(new int3(101, 21, 301), anchors[0].Position);
            Assert.AreEqual(Facing.South, anchors[0].Facing);

            anchors.Dispose();
        }

        [Test]
        public void SemanticChildSeedsDoNotDependOnUnrelatedDrawOrder()
        {
            const ulong parent = 0x1122334455667788ul;
            var roof = new FixedString64Bytes("roof");
            var windows = new FixedString64Bytes("windows");
            var unrelated = new FixedString64Bytes("porch-detail");

            ulong roofBefore = StructureSeed.Child(parent, in roof);
            _ = StructureSeed.Child(parent, in unrelated);
            ulong roofAfter = StructureSeed.Child(parent, in roof);

            Assert.AreEqual(roofBefore, roofAfter);
            Assert.AreNotEqual(roofBefore, StructureSeed.Child(parent, in windows));
            Assert.AreNotEqual(roofBefore, StructureSeed.Child(parent, in roof, 1));
        }

        [Test]
        public void DimensionValidationRejectsByDefaultAndClampsOnlyWhenRequested()
        {
            var rejected = StructureConfigValidation.Dimension(
                3, 4, 12, StructureValidationPolicy.Reject, out int rejectedValue);
            Assert.AreEqual(StructureValidationResult.Rejected, rejected);
            Assert.AreEqual(3, rejectedValue);

            var clamped = StructureConfigValidation.Dimension(
                3, 4, 12, StructureValidationPolicy.Clamp, out int clampedValue);
            Assert.AreEqual(StructureValidationResult.Clamped, clamped);
            Assert.AreEqual(4, clampedValue);

            var valid = StructureConfigValidation.Dimension(
                8, 4, 12, StructureValidationPolicy.Reject, out int validValue);
            Assert.AreEqual(StructureValidationResult.Valid, valid);
            Assert.AreEqual(8, validValue);
        }

        [Test]
        public void RangeValidationPreservesOrderAndAllowedBounds()
        {
            var clamped = StructureConfigValidation.OrderedRange(
                2, 20, 4, 16, StructureValidationPolicy.Clamp,
                out int minimum, out int maximum);

            Assert.AreEqual(StructureValidationResult.Clamped, clamped);
            Assert.AreEqual(4, minimum);
            Assert.AreEqual(16, maximum);

            var invalidOrder = StructureConfigValidation.OrderedRange(
                12, 4, 1, 20, StructureValidationPolicy.Clamp,
                out _, out _);
            Assert.AreEqual(StructureValidationResult.Rejected, invalidOrder);
        }

        [Test]
        public void SemanticPaletteMapsEverySharedRoleToOpaqueMaterialId()
        {
            var palette = new StructureMaterialPalette
            {
                Foundation = 1,
                PrimaryWall = 2,
                SecondaryWall = 3,
                Trim = 4,
                Roof = 5,
                Floor = 6,
                Column = 7,
                Accent = 8,
                Underground = 9,
                Opening = 10,
                Glass = 11,
                Detail = 12,
            };

            Assert.AreEqual(1, palette.Resolve(StructureMaterialRole.Foundation));
            Assert.AreEqual(2, palette.Resolve(StructureMaterialRole.PrimaryWall));
            Assert.AreEqual(3, palette.Resolve(StructureMaterialRole.SecondaryWall));
            Assert.AreEqual(4, palette.Resolve(StructureMaterialRole.Trim));
            Assert.AreEqual(5, palette.Resolve(StructureMaterialRole.Roof));
            Assert.AreEqual(6, palette.Resolve(StructureMaterialRole.Floor));
            Assert.AreEqual(7, palette.Resolve(StructureMaterialRole.Column));
            Assert.AreEqual(8, palette.Resolve(StructureMaterialRole.Accent));
            Assert.AreEqual(9, palette.Resolve(StructureMaterialRole.Underground));
            Assert.AreEqual(10, palette.Resolve(StructureMaterialRole.Opening));
            Assert.AreEqual(11, palette.Resolve(StructureMaterialRole.Glass));
            Assert.AreEqual(12, palette.Resolve(StructureMaterialRole.Detail));
        }
    }
}
