using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationContextProfileTests
    {
        [Test]
        public void StyleFamilyRoundTripsAndChangesMaterialAndSilhouettePolicy()
        {
            const uint variation = 0x00ABCDEFu;
            uint courtlyId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, variation);
            uint martialId = DecorationStyleIds.Compose(DecorationStyleFamily.Martial, variation);
            DecorationContext courtly = Context(101u, courtlyId, DecorationWealthTier.Comfortable,
                DecorationConditionTier.Maintained);
            DecorationContext martial = Context(101u, martialId, DecorationWealthTier.Comfortable,
                DecorationConditionTier.Maintained);

            DecorationPresentationProfile courtlyPresentation =
                DecorationContextProfiles.ResolvePresentation(in courtly);
            DecorationPresentationProfile martialPresentation =
                DecorationContextProfiles.ResolvePresentation(in martial);
            DecorationPropDescriptor baseBed = DecorationPropPresets.Bed(in courtly);
            DecorationPropDescriptor courtlyBed = DecorationContextProfiles.ApplySilhouette(in baseBed, in courtly);
            DecorationPropDescriptor martialBed = DecorationContextProfiles.ApplySilhouette(in baseBed, in martial);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(DecorationStyleFamily.Courtly, DecorationStyleIds.FamilyOf(courtlyId));
                Assert.AreEqual(DecorationStyleFamily.Martial, DecorationStyleIds.FamilyOf(martialId));
                Assert.AreEqual(variation, DecorationStyleIds.VariationOf(courtlyId));
                Assert.AreEqual(GameMaterialIds.Wood, courtlyPresentation.PrimaryMaterial);
                Assert.AreEqual(GameMaterialIds.DarkStone, martialPresentation.PrimaryMaterial);
                Assert.Greater(courtlyPresentation.Ornamentation, martialPresentation.Ornamentation);
                Assert.AreNotEqual(courtlyBed.Size.x, martialBed.Size.x);
                Assert.AreNotEqual(courtlyBed.Variant, martialBed.Variant);
            });
        }

        [Test]
        public void WealthControlsOrnamentationAndOptionalBedroomDensity()
        {
            DecorationSpace space = BedroomSpace();
            uint styleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, 42u);
            DecorationContext poor = Context(222u, styleId, DecorationWealthTier.Poor,
                DecorationConditionTier.Maintained);
            DecorationContext noble = Context(222u, styleId, DecorationWealthTier.Noble,
                DecorationConditionTier.Maintained);

            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in poor, null, out DecorationPlacement[] poorBaseline));
            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in noble, null, out DecorationPlacement[] nobleBaseline));
            Assert.IsTrue(BedroomSceneContextVariation.TryApply(
                in space, in poor, null, poorBaseline, out DecorationPlacement[] poorPlacements));
            Assert.IsTrue(BedroomSceneContextVariation.TryApply(
                in space, in noble, null, nobleBaseline, out DecorationPlacement[] noblePlacements));

            DecorationPresentationProfile poorPresentation = DecorationContextProfiles.ResolvePresentation(in poor);
            DecorationPresentationProfile noblePresentation = DecorationContextProfiles.ResolvePresentation(in noble);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(0, DecorationContextProfiles.OptionalSceneBudget(in poor));
                Assert.AreEqual(1, DecorationContextProfiles.OptionalSceneBudget(in noble));
                Assert.Greater(noblePresentation.Ornamentation, poorPresentation.Ornamentation);
                Assert.AreEqual(BedroomSceneResolver.PlacementCount, poorPlacements.Length);
                Assert.AreEqual(BedroomSceneContextVariation.MaximumPlacementCount, noblePlacements.Length,
                    "A spacious noble courtly bedroom should resolve its optional accent torch.");
                Assert.AreEqual(BedroomSceneContextVariation.AccentTorchSlot,
                    noblePlacements[noblePlacements.Length - 1].SlotId);
            });

            AssertPlacementsAreValid(in space, poorPlacements);
            AssertPlacementsAreValid(in space, noblePlacements);
        }

        [Test]
        public void ConditionChangesDamagePresentationWithoutBreakingPlacementInvariants()
        {
            DecorationSpace space = BedroomSpace();
            uint styleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, 99u);
            DecorationContext maintained = Context(333u, styleId, DecorationWealthTier.Comfortable,
                DecorationConditionTier.Maintained);
            DecorationContext ruined = Context(333u, styleId, DecorationWealthTier.Comfortable,
                DecorationConditionTier.Ruined);

            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in maintained, null, out DecorationPlacement[] maintainedPlacements));
            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in ruined, null, out DecorationPlacement[] ruinedPlacements));

            DecorationPresentationProfile maintainedPresentation =
                DecorationContextProfiles.ResolvePresentation(in maintained);
            DecorationPresentationProfile ruinedPresentation =
                DecorationContextProfiles.ResolvePresentation(in ruined);

            Assert.Multiple(() =>
            {
                Assert.Less(maintainedPresentation.DamageLevel, ruinedPresentation.DamageLevel);
                Assert.IsTrue(maintainedPresentation.EmitsLight);
                Assert.IsFalse(ruinedPresentation.EmitsLight);
                Assert.AreEqual(GameMaterialIds.Cloth, maintainedPresentation.SoftMaterial);
                Assert.AreEqual(GameMaterialIds.Dirt, ruinedPresentation.SoftMaterial);
                Assert.Greater(maintainedPresentation.Ornamentation, ruinedPresentation.Ornamentation);
                Assert.AreEqual(maintainedPlacements.Length, ruinedPlacements.Length);
            });

            AssertPlacementsAreValid(in space, maintainedPlacements);
            AssertPlacementsAreValid(in space, ruinedPlacements);
        }

        private static DecorationContext Context(
            uint seed,
            uint styleId,
            DecorationWealthTier wealth,
            DecorationConditionTier condition) => new DecorationContext
        {
            WorldSeed = seed,
            StructureId = 0xCA571Eu,
            SpaceId = 0xBED001u,
            StyleId = styleId,
            StructureKind = DecorationStructureKind.Castle,
            SpaceKind = DecorationSpaceKind.Bedroom,
            Wealth = wealth,
            Condition = condition,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
        };

        private static DecorationSpace BedroomSpace() => new DecorationSpace
        {
            SpaceId = 0xBED001u,
            Kind = DecorationSpaceKind.Bedroom,
            Bounds = new DecorationBounds
            {
                Min = new int3(-100, 10, -80),
                MaxExclusive = new int3(100, 70, 80),
            },
        };

        private static void AssertPlacementsAreValid(
            in DecorationSpace space,
            DecorationPlacement[] placements)
        {
            Assert.IsNotNull(placements);
            for (int i = 0; i < placements.Length; i++)
            {
                Assert.IsTrue(placements[i].IsWellFormed, $"Placement {i} was malformed.");
                Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds),
                    $"Placement {i} escaped its room bounds.");
            }
        }
    }
}
