using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class StoragePropPresetTests
    {
        private const uint SceneId = 0x53544731u; // STG1

        [Test]
        public void StorageFamiliesAreWellFormedAndUseExpectedMounts()
        {
            DecorationContext context = Context(17u, DecorationWealthTier.Wealthy);
            DecorationPropDescriptor chest = StorageFurniturePresets.Chest(in context, SceneId, 1u);
            DecorationPropDescriptor shelf = StorageFurniturePresets.Shelf(in context, SceneId, 2u);
            DecorationPropDescriptor bookcase = StorageFurniturePresets.Bookcase(in context, SceneId, 3u);
            DecorationPropDescriptor crate = StorageContainerPresets.Crate(in context, SceneId, 4u);
            DecorationPropDescriptor barrel = StorageContainerPresets.Barrel(in context, SceneId, 5u);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(chest.IsWellFormed);
                Assert.IsTrue(shelf.IsWellFormed);
                Assert.IsTrue(bookcase.IsWellFormed);
                Assert.IsTrue(crate.IsWellFormed);
                Assert.IsTrue(barrel.IsWellFormed);
                Assert.AreEqual(DecorationMountMode.FloorAgainstWall, chest.MountMode);
                Assert.AreEqual(DecorationMountMode.Wall, shelf.MountMode);
                Assert.AreEqual(DecorationMountMode.FloorAgainstWall, bookcase.MountMode);
                Assert.AreEqual(DecorationMountMode.Floor, crate.MountMode);
                Assert.AreEqual(DecorationMountMode.Floor, barrel.MountMode);
            });
        }

        [Test]
        public void ContainerFamiliesCarryPersistenceRelevantInteractionFlags()
        {
            DecorationContext context = Context(29u, DecorationWealthTier.Modest);
            DecorationPropDescriptor[] containers =
            {
                StorageFurniturePresets.Chest(in context, SceneId, 1u),
                StorageContainerPresets.Crate(in context, SceneId, 2u),
                StorageContainerPresets.Barrel(in context, SceneId, 3u),
            };

            for (int i = 0; i < containers.Length; i++)
            {
                DecorationInteractionFlags flags = containers[i].Interaction;
                Assert.Multiple(() =>
                {
                    Assert.AreNotEqual(DecorationInteractionFlags.None,
                        flags & DecorationInteractionFlags.Container);
                    Assert.AreNotEqual(DecorationInteractionFlags.None,
                        flags & DecorationInteractionFlags.Lootable);
                    Assert.AreNotEqual(DecorationInteractionFlags.None,
                        flags & DecorationInteractionFlags.Movable);
                    Assert.AreNotEqual(DecorationInteractionFlags.None,
                        flags & DecorationInteractionFlags.Destructible);
                });
            }
        }

        [Test]
        public void WealthProducesControlledStorageFurnitureScaleVariation()
        {
            DecorationContext poor = Context(41u, DecorationWealthTier.Poor);
            DecorationContext noble = Context(41u, DecorationWealthTier.Noble);
            DecorationPropDescriptor poorChest = StorageFurniturePresets.Chest(in poor, SceneId, 1u);
            DecorationPropDescriptor nobleChest = StorageFurniturePresets.Chest(in noble, SceneId, 1u);
            DecorationPropDescriptor poorBookcase = StorageFurniturePresets.Bookcase(in poor, SceneId, 2u);
            DecorationPropDescriptor nobleBookcase = StorageFurniturePresets.Bookcase(in noble, SceneId, 2u);

            Assert.Multiple(() =>
            {
                Assert.Greater(nobleChest.Size.x, poorChest.Size.x);
                Assert.Greater(nobleBookcase.Size.x, poorBookcase.Size.x);
                Assert.Greater(nobleBookcase.Size.y, poorBookcase.Size.y);
                Assert.AreNotEqual(poorChest.Variant, nobleChest.Variant);
            });
        }

        [Test]
        public void StorageVariantsAreStablePerSceneAndSlot()
        {
            DecorationContext context = Context(0xCAFEu, DecorationWealthTier.Comfortable);
            DecorationPropDescriptor first = StorageFurniturePresets.Bookcase(in context, SceneId, 7u);
            DecorationPropDescriptor again = StorageFurniturePresets.Bookcase(in context, SceneId, 7u);
            DecorationPropDescriptor otherSlot = StorageFurniturePresets.Bookcase(in context, SceneId, 8u);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(first.Size, again.Size);
                Assert.AreEqual(first.Variant, again.Variant);
                Assert.AreNotEqual(first.Variant, otherSlot.Variant);
            });
        }

        [Test]
        public void BookcaseAndCrateUseExistingCorePlacementResolver()
        {
            DecorationContext context = Context(73u, DecorationWealthTier.Comfortable);
            DecorationSpace space = Space();
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            DecorationExclusion[] exclusions = DoorExclusion(in space);
            var occupied = new DecorationPlacement[2];

            DecorationPropDescriptor bookcase = StorageFurniturePresets.Bookcase(in context, SceneId, 1u);
            Assert.IsTrue(DecorationPlacementResolver.TryPlace(
                in space, in context, SceneId, 1u, in bookcase,
                sockets, exclusions, occupied, 0, out occupied[0]));

            DecorationPropDescriptor crate = StorageContainerPresets.Crate(in context, SceneId, 2u);
            Assert.IsTrue(DecorationPlacementResolver.TryPlace(
                in space, in context, SceneId, 2u, in crate,
                sockets, exclusions, occupied, 1, out occupied[1]));

            Assert.Multiple(() =>
            {
                Assert.IsTrue(space.Bounds.Contains(in occupied[0].Bounds));
                Assert.IsTrue(space.Bounds.Contains(in occupied[1].Bounds));
                Assert.IsFalse(occupied[0].Bounds.Overlaps(in occupied[1].Bounds));
            });
        }

        private static DecorationContext Context(uint seed, DecorationWealthTier wealth) =>
            new DecorationContext
            {
                WorldSeed = seed,
                StructureId = 0x5702A6Eu,
                SpaceId = 0x5702A600u,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, seed),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.Storage,
                Wealth = wealth,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0x5702A600u,
            Kind = DecorationSpaceKind.Storage,
            Bounds = new DecorationBounds
            {
                Min = new int3(-60, 10, -50),
                MaxExclusive = new int3(60, 52, 50),
            },
        };

        private static DecorationExclusion[] DoorExclusion(in DecorationSpace space) => new[]
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
    }
}
