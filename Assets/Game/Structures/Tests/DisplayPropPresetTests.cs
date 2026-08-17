using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DisplayPropPresetTests
    {
        private const uint SceneId = 0x44535031u;

        [Test]
        public void DisplayRecipesAreWellFormedAndSubtypeStable()
        {
            DecorationContext c = Context(17u);
            DecorationPropDescriptor banner = TextileDisplayPresets.Banner(in c, SceneId, 1u);
            DecorationPropDescriptor curtain = TextileDisplayPresets.Curtain(in c, SceneId, 2u);
            DecorationPropDescriptor shield = MartialDisplayPresets.ShieldDisplay(in c, SceneId, 3u);
            DecorationPropDescriptor weapons = MartialDisplayPresets.WeaponRack(in c, SceneId, 4u);
            DecorationPropDescriptor armor = MartialDisplayPresets.ArmorDisplay(in c, SceneId, 5u);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(banner.IsWellFormed);
                Assert.IsTrue(curtain.IsWellFormed);
                Assert.IsTrue(shield.IsWellFormed);
                Assert.IsTrue(weapons.IsWellFormed);
                Assert.IsTrue(armor.IsWellFormed);
                Assert.AreEqual(DecorationRenderBackend.ThinSurface, banner.Backend);
                Assert.AreEqual(DecorationRenderBackend.ThinSurface, curtain.Backend);
                Assert.AreEqual(MartialDisplayKind.Shield, MartialDisplayVariants.KindOf(shield.Variant));
                Assert.AreEqual(MartialDisplayKind.Weapons, MartialDisplayVariants.KindOf(weapons.Variant));
                Assert.AreEqual(MartialDisplayKind.Armor, MartialDisplayVariants.KindOf(armor.Variant));
            });
        }

        [Test]
        public void TextileDisplaysFeedTrueThinSurfaceBatch()
        {
            DecorationContext c = Context(41u);
            DecorationSpace space = Space();
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var placements = new DecorationPlacement[2];
            DecorationPropDescriptor banner = TextileDisplayPresets.Banner(in c, SceneId, 1u);
            DecorationPropDescriptor curtain = TextileDisplayPresets.Curtain(in c, SceneId, 2u);

            Assert.IsTrue(DecorationPlacementResolver.TryPlace(
                in space, in c, SceneId, 1u, in banner, sockets, null, placements, 0, out placements[0]));
            Assert.IsTrue(DecorationPlacementResolver.TryPlace(
                in space, in c, SceneId, 2u, in curtain, sockets, null, placements, 1, out placements[1]));
            Assert.IsTrue(DecorationThinSurfaceBatchBuilder.TryBuild(
                placements, 0.1f, 0.005f, out DecorationThinSurfaceBatch batch));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(2, batch.SurfaceCount);
                Assert.IsTrue(batch.IsWellFormed);
                Assert.AreEqual(DecorationPropFamily.Banner, batch.Ranges[0].Family);
                Assert.AreEqual(DecorationPropFamily.Curtain, batch.Ranges[1].Family);
            });
        }

        [Test]
        public void MartialDisplaysUseCoreWallAndFloorPlacement()
        {
            DecorationContext c = Context(73u);
            DecorationSpace space = Space();
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            DecorationPropDescriptor shield = MartialDisplayPresets.ShieldDisplay(in c, SceneId, 1u);
            DecorationPropDescriptor weapons = MartialDisplayPresets.WeaponRack(in c, SceneId, 2u);
            DecorationPropDescriptor armor = MartialDisplayPresets.ArmorDisplay(in c, SceneId, 3u);

            Assert.IsTrue(DecorationPlacementResolver.TryPlace(
                in space, in c, SceneId, 1u, in shield, sockets, null, null, 0, out DecorationPlacement p0));
            Assert.IsTrue(DecorationPlacementResolver.TryPlace(
                in space, in c, SceneId, 2u, in weapons, sockets, null, null, 0, out DecorationPlacement p1));
            Assert.IsTrue(DecorationPlacementResolver.TryPlace(
                in space, in c, SceneId, 3u, in armor, sockets, null, null, 0, out DecorationPlacement p2));

            Assert.Multiple(() =>
            {
                Assert.IsTrue(space.Bounds.Contains(in p0.Bounds));
                Assert.IsTrue(space.Bounds.Contains(in p1.Bounds));
                Assert.IsTrue(space.Bounds.Contains(in p2.Bounds));
            });
        }

        private static DecorationContext Context(uint seed) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xD15A1A6u,
            SpaceId = 0xD15A100u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Martial, seed),
            StructureKind = DecorationStructureKind.Castle,
            SpaceKind = DecorationSpaceKind.GuardPost,
            Wealth = DecorationWealthTier.Wealthy,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Military,
        };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xD15A100u,
            Kind = DecorationSpaceKind.GuardPost,
            Bounds = new DecorationBounds
            {
                Min = new int3(-70, 10, -55),
                MaxExclusive = new int3(70, 58, 55),
            },
        };
    }
}
