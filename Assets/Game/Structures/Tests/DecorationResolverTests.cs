using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationResolverTests
    {
        [Test]
        public void FoundationContextDescriptorsAndSceneAreWellFormed()
        {
            DecorationContext context = Context(0x12345678u);
            DecorationSceneSlot[] slots = BedroomSceneDefinition.CreateSlots();

            Assert.Multiple(() =>
            {
                Assert.IsTrue(context.IsWellFormed);
                Assert.IsTrue(DecorationPropPresets.Bed(in context).IsWellFormed);
                Assert.IsTrue(DecorationPropPresets.Dresser(in context).IsWellFormed);
                Assert.IsTrue(DecorationPropPresets.Rug(in context).IsWellFormed);
                Assert.IsTrue(DecorationPropPresets.Painting(in context).IsWellFormed);
                Assert.IsTrue(DecorationPropPresets.WallTorch(in context).IsWellFormed);
                Assert.IsTrue(DecorationValidation.ValidateScene(slots, out uint errorSlot),
                    $"Scene validation failed at slot {errorSlot}.");
            });
        }

        [Test]
        public void SceneValidationRejectsMissingAnchorsAndCycles()
        {
            var missing = new[]
            {
                Slot(1, DecorationPropFamily.Bed, 999),
            };
            var cycle = new[]
            {
                Slot(1, DecorationPropFamily.Bed, 2),
                Slot(2, DecorationPropFamily.Dresser, 1),
            };

            Assert.Multiple(() =>
            {
                Assert.IsFalse(DecorationValidation.ValidateScene(missing, out uint missingError));
                Assert.AreEqual(1u, missingError);
                Assert.IsFalse(DecorationValidation.ValidateScene(cycle, out uint cycleError));
                Assert.AreNotEqual(0u, cycleError);
            });
        }

        [Test]
        public void GeneratedPropIdentityIsStableAndIndependentPerSlot()
        {
            DecorationContext context = Context(0xCAFEBABEu);
            GeneratedPropId first = GeneratedPropIds.Create(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.BedSlot);
            GeneratedPropId again = GeneratedPropIds.Create(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.BedSlot);
            GeneratedPropId otherSlot = GeneratedPropIds.Create(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.DresserSlot);

            DecorationContext changed = context;
            changed.WorldSeed++;
            GeneratedPropId otherSeed = GeneratedPropIds.Create(in changed, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.BedSlot);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(first, again);
                Assert.AreNotEqual(first, otherSlot);
                Assert.AreNotEqual(first, otherSeed);
                Assert.AreNotEqual(0UL, first.Value);
            });
        }

        [Test]
        public void RectangularAnalyzerExposesFloorWallsCornersAndCeiling()
        {
            DecorationSpace space = BedroomSpace();
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);

            int floor = Count(sockets, DecorationSocketKind.Floor);
            int walls = Count(sockets, DecorationSocketKind.Wall);
            int corners = Count(sockets, DecorationSocketKind.Corner);
            int ceiling = Count(sockets, DecorationSocketKind.Ceiling);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(10, sockets.Length);
                Assert.AreEqual(1, floor);
                Assert.AreEqual(4, walls);
                Assert.AreEqual(4, corners);
                Assert.AreEqual(1, ceiling);
                for (int i = 0; i < sockets.Length; i++)
                    Assert.IsTrue(sockets[i].IsWellFormed, $"Socket {i} was malformed.");
            });
        }

        [Test]
        public void PlacementFailsWhenExclusionConsumesTheUsableSpace()
        {
            DecorationContext context = Context(81u);
            DecorationSpace space = BedroomSpace();
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var descriptor = new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Table,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation,
                Size = new int3(10, 8, 10),
                Clearance = new int3(2, 0, 2),
                Variant = 1,
            };
            var exclusions = new[]
            {
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Gameplay,
                    Bounds = space.Bounds,
                },
            };

            bool placed = DecorationPlacementResolver.TryPlace(in space, in context, 77u, 1u,
                in descriptor, sockets, exclusions, null, 0, out _);

            Assert.IsFalse(placed);
        }

        [Test]
        public void BedroomSceneResolvesFiveDeterministicRelationalPlacements()
        {
            DecorationContext context = Context(0xBEEFu);
            DecorationSpace space = BedroomSpace();
            DecorationExclusion[] exclusions = DoorExclusions(in space);

            Assert.IsTrue(BedroomSceneResolver.TryResolve(in space, in context, exclusions, out DecorationPlacement[] first));
            Assert.IsTrue(BedroomSceneResolver.TryResolve(in space, in context, exclusions, out DecorationPlacement[] second));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(BedroomSceneResolver.PlacementCount, first.Length);
                Assert.AreEqual(DecorationPropFamily.Bed, first[0].Family);
                Assert.AreEqual(DecorationPropFamily.Rug, first[1].Family);
                Assert.AreEqual(DecorationPropFamily.Dresser, first[2].Family);
                Assert.AreEqual(DecorationPropFamily.Painting, first[3].Family);
                Assert.AreEqual(DecorationPropFamily.WallTorch, first[4].Family);
                Assert.AreEqual(BedroomSceneDefinition.BedSlot, first[1].AnchorSlotId);
                Assert.AreEqual(BedroomSceneDefinition.DresserSlot, first[3].AnchorSlotId);

                for (int i = 0; i < first.Length; i++)
                {
                    Assert.IsTrue(first[i].IsWellFormed, $"Placement {i} was malformed.");
                    Assert.IsTrue(space.Bounds.Contains(in first[i].Bounds), $"Placement {i} escaped the room.");
                    Assert.AreEqual(first[i].Id, second[i].Id, $"Placement {i} ID changed for identical input.");
                    Assert.AreEqual(first[i].Bounds.Min, second[i].Bounds.Min, $"Placement {i} position changed for identical input.");
                    Assert.AreEqual(first[i].Bounds.MaxExclusive, second[i].Bounds.MaxExclusive,
                        $"Placement {i} bounds changed for identical input.");
                    Assert.AreEqual(first[i].Variant, second[i].Variant, $"Placement {i} variant changed for identical input.");
                }
            });
        }

        [Test]
        public void BedroomSeedAndWealthProduceControlledVariation()
        {
            DecorationSpace space = BedroomSpace();
            DecorationContext modest = Context(111u);
            modest.Wealth = DecorationWealthTier.Modest;
            DecorationContext noble = Context(222u);
            noble.Wealth = DecorationWealthTier.Noble;

            Assert.IsTrue(BedroomSceneResolver.TryResolve(in space, in modest, null, out DecorationPlacement[] modestPlacements));
            Assert.IsTrue(BedroomSceneResolver.TryResolve(in space, in noble, null, out DecorationPlacement[] noblePlacements));

            DecorationPropDescriptor modestBed = DecorationPropPresets.Bed(in modest);
            DecorationPropDescriptor nobleBed = DecorationPropPresets.Bed(in noble);

            Assert.Multiple(() =>
            {
                Assert.AreNotEqual(modestPlacements[0].Id, noblePlacements[0].Id);
                Assert.Greater(nobleBed.Size.x, modestBed.Size.x);
                Assert.Greater(nobleBed.Size.y, modestBed.Size.y);
                Assert.AreNotEqual(modestBed.Variant, nobleBed.Variant);
            });
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xCA571Eu,
            SpaceId = 0xBED001u,
            StyleId = 7u,
            StructureKind = DecorationStructureKind.Castle,
            SpaceKind = DecorationSpaceKind.Bedroom,
            Wealth = DecorationWealthTier.Wealthy,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
        };

        private static DecorationSpace BedroomSpace() => new DecorationSpace
        {
            SpaceId = 0xBED001u,
            Kind = DecorationSpaceKind.Bedroom,
            Bounds = new DecorationBounds
            {
                Min = new int3(-60, 10, -50),
                MaxExclusive = new int3(60, 58, 50),
            },
        };

        private static DecorationExclusion[] DoorExclusions(in DecorationSpace space) => new[]
        {
            new DecorationExclusion
            {
                Kind = DecorationExclusionKind.Door | DecorationExclusionKind.Navigation,
                Bounds = new DecorationBounds
                {
                    Min = new int3(-8, space.Bounds.Min.y, space.Bounds.Min.z),
                    MaxExclusive = new int3(8, space.Bounds.Min.y + 24, space.Bounds.Min.z + 14),
                },
            },
        };

        private static DecorationSceneSlot Slot(uint id, DecorationPropFamily family, uint anchor) =>
            new DecorationSceneSlot
            {
                SlotId = id,
                Family = family,
                RequestedSocket = DecorationSocketKind.Wall,
                AnchorSlotId = anchor,
                Weight = 1,
                Required = true,
            };

        private static int Count(DecorationSocket[] sockets, DecorationSocketKind kind)
        {
            int count = 0;
            for (int i = 0; i < sockets.Length; i++)
                if (sockets[i].Kind == kind)
                    count++;
            return count;
        }
    }
}
