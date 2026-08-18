using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureAuthoringFoundationTests
    {
        [Test]
        public void GenerationContextCarriesStableIdentityBoundsTerrainPaletteAndAnchors()
        {
            var anchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            try
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

                const uint worldSeed = 0x12345678u;
                const uint terrainSeed = 0x87654321u;
                const int definitionId = 17;
                int3 origin = new(100, 200, 300);
                int3 footprint = new(40, 50, 60);

                StructureGenerationContext context = StructureGenerationContext.ForFeature(
                    worldSeed,
                    terrainSeed,
                    definitionId,
                    origin,
                    orientation: 5,
                    footprint,
                    in palette,
                    anchors);

                ulong expectedIdentity = FeatureHash.Cell(worldSeed, definitionId, origin);
                Assert.AreEqual(expectedIdentity, context.InstanceId);
                Assert.AreEqual(expectedIdentity, context.InstanceSeed);
                Assert.AreEqual(worldSeed, context.WorldSeed);
                Assert.AreEqual(definitionId, context.DefinitionId);
                Assert.AreEqual(origin, context.Origin);
                Assert.AreEqual(1, context.Orientation, "orientation must normalize to four cardinal values");
                Assert.AreEqual(origin, context.Bounds.Min);
                Assert.AreEqual(
                    origin + new int3(footprint.z, footprint.y, footprint.x),
                    context.Bounds.MaxExclusive,
                    "odd cardinal rotations must swap X/Z footprint extents");
                Assert.IsTrue(context.Bounds.Contains(origin));
                Assert.IsFalse(context.Bounds.Contains(context.Bounds.MaxExclusive),
                    "max bounds are exclusive");
                Assert.AreEqual(terrainSeed, context.Terrain.Seed);
                Assert.AreEqual(context.Terrain.HeightAt(111, 333), context.Terrain.HeightAt(111, 333));
                Assert.AreEqual(5, context.Material(StructureMaterialRole.Roof));
                Assert.AreEqual(9, context.Material(StructureMaterialRole.Underground));

                var name = new FixedString32Bytes("MainEntrance");
                Assert.IsTrue(context.TryAddResolvedAnchor(
                    in name, new int3(120, 201, 300), Facing.South));
                Assert.AreEqual(1, context.AnchorCount);
                Assert.AreEqual(1, anchors.Length);
                Assert.AreEqual(name, anchors[0].Name);
                Assert.AreEqual(new int3(120, 201, 300), anchors[0].Position);
                Assert.AreEqual(Facing.South, anchors[0].Facing);
            }
            finally
            {
                anchors.Dispose();
            }
        }

        [Test]
        public void SemanticChildSeedsDoNotDependOnUnrelatedEvaluationOrder()
        {
            var palette = new StructureMaterialPalette();
            var bounds = new StructureGenerationBounds(int3.zero, new int3(64));
            var terrain = new StructureTerrainAccess(42u);
            var context = new StructureGenerationContext(
                instanceId: 99ul,
                worldSeed: 7u,
                definitionId: 3,
                instanceSeed: 0xDEADBEEFCAFEBABEul,
                origin: int3.zero,
                orientation: 0,
                in bounds,
                in terrain,
                in palette,
                default);

            var roof = new FixedString64Bytes("roof");
            var windows = new FixedString64Bytes("windows.north");
            var unrelated = new FixedString64Bytes("porch-detail");

            ulong roofBefore = context.ChildSeed(in roof);
            _ = context.ChildSeed(in unrelated);
            ulong roofAfter = context.ChildSeed(in roof);

            Assert.AreEqual(roofBefore, roofAfter);
            Assert.AreNotEqual(roofBefore, context.ChildSeed(in windows));
            Assert.AreNotEqual(roofBefore, context.ChildSeed(in roof, 1));
            Assert.AreEqual(StructureSeed.Child(context.InstanceSeed, in roof), roofBefore);
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

        [Test]
        public void FootprintConfigSupportsRectangularAndComposedFootprints()
        {
            var config = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(new int2(0, 0), new int2(12, 8)),
                FoundationStyle = StructureFoundationStyle.Slab,
                FoundationDepth = 2,
                FoundationMaterial = StructureMaterialRole.Foundation,
            };
            config.AdditionalRects.Add(new StructureFootprintRect(new int2(8, 8), new int2(4, 6)));

            Assert.IsTrue(config.IsWellFormed);
            Assert.IsTrue(config.IsComposed);
            Assert.AreEqual(2, config.PartCount);
            Assert.AreEqual(new int2(0, 0), config.PartAt(0).Min);
            Assert.AreEqual(new int2(8, 8), config.PartAt(1).Min);

            Assert.IsTrue(config.Primary.Contains(new int2(0, 0)));
            Assert.IsTrue(config.Primary.Contains(new int2(11, 7)));
            Assert.IsFalse(config.Primary.Contains(new int2(12, 7)), "rectangles are max-exclusive");
            Assert.IsFalse(config.Primary.Contains(new int2(11, 8)), "rectangles are max-exclusive");
        }

        [Test]
        public void FootprintConfigRejectsInvalidFoundationInvariants()
        {
            var missingDepth = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(new int2(0, 0), new int2(8, 8)),
                FoundationStyle = StructureFoundationStyle.Slab,
                FoundationDepth = 0,
            };
            Assert.IsFalse(missingDepth.IsWellFormed);

            var missingTerraceStep = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(new int2(0, 0), new int2(8, 8)),
                FoundationStyle = StructureFoundationStyle.Terraced,
                FoundationDepth = 2,
                MaxTerraceStep = 0,
            };
            Assert.IsFalse(missingTerraceStep.IsWellFormed);

            var noFoundation = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(new int2(0, 0), new int2(8, 8)),
                FoundationStyle = StructureFoundationStyle.None,
                FoundationDepth = 0,
            };
            Assert.IsTrue(noFoundation.IsWellFormed);
        }
    }
}
