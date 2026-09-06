using System;
using System.Linq;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Structures.Tests
{
    public sealed class PropShowcasePresentationQualityTests
    {
        [Test]
        public void MerchantSign_UsesSharedFramedThinSurfaceDetail()
        {
            DecorationContext context = Context();
            DecorationPlacement sign = FindContent(DecorationContentKind.MerchantSign, in context);
            Assert.That(sign.Backend, Is.EqualTo(DecorationRenderBackend.ThinSurface));
            Assert.That(sign.Family, Is.EqualTo(DecorationPropFamily.Painting));
            Assert.That(DecorationThinSurfaceBatchBuilder.TryBuild(new[] { sign }, 0.1f, 0.003f, out DecorationThinSurfaceBatch baseSurface), Is.True);
            Assert.That(baseSurface.SurfaceCount, Is.EqualTo(1));
            Assert.That(DecorationThinSurfaceDetailGeometry.TryBuild(new[] { sign }, in context, out DecorationProceduralGeometry detail), Is.True);
            Assert.That(detail.IsWellFormed, Is.True);
            Assert.That(detail.Positions.Length, Is.GreaterThanOrEqualTo(48), "A framed sign needs more construction than one flat quad.");
            Assert.That(detail.Indices.Length, Is.GreaterThanOrEqualTo(72));
        }

        [Test]
        public void NonPaintingThinSurface_DoesNotAcquireSignFrameDetail()
        {
            DecorationContext context = Context();
            DecorationPlacement canopy = FindContent(DecorationContentKind.FabricCanopy, in context);
            Assert.That(canopy.Backend, Is.EqualTo(DecorationRenderBackend.ThinSurface));
            Assert.That(canopy.Family, Is.Not.EqualTo(DecorationPropFamily.Painting));
            Assert.That(DecorationThinSurfaceDetailGeometry.TryBuild(new[] { canopy }, in context, out _), Is.False);
        }

        [TestCase(WorldObjectKind.Door, false)]
        [TestCase(WorldObjectKind.SecretDoor, false)]
        [TestCase(WorldObjectKind.Trapdoor, true)]
        public void DoorMechanisms_UseDetailedNormalizedPanelGeometry(WorldObjectKind kind, bool horizontal)
        {
            Assert.That(WorldObjectProxyGeometry.TryBuild(kind, out WorldObjectProxyGeometryData geometry), Is.True);
            Assert.That(geometry.IsWellFormed, Is.True);
            Assert.That(geometry.Positions.Length, Is.GreaterThanOrEqualTo(48), "Detailed panels must not collapse to one cube.");

            Vector3 min = geometry.Positions[0];
            Vector3 max = geometry.Positions[0];
            for (int i = 1; i < geometry.Positions.Length; i++)
            {
                min = Vector3.Min(min, geometry.Positions[i]);
                max = Vector3.Max(max, geometry.Positions[i]);
            }
            Vector3 size = max - min;
            if (horizontal)
            {
                Assert.That(size.y, Is.LessThan(size.x));
                Assert.That(size.y, Is.LessThan(size.z));
            }
            else
            {
                Assert.That(size.z, Is.LessThan(size.x));
                Assert.That(size.z, Is.LessThan(size.y));
            }
        }

        [Test]
        public void ForgeHearth_ProducesBothSharedPresentationEffects()
        {
            DecorationContext context = Context();
            DecorationPlacement hearth = FindContent(DecorationContentKind.ForgeHearth, in context);
            DecorationEffectHook[] hooks = DecorationEffectHookPlanner.Collect(new[] { hearth }, in context);
            Assert.That(hooks.Length, Is.EqualTo(2));
            Assert.That(hooks.Count(h => h.Kind == DecorationEffectKind.Light), Is.EqualTo(1));
            Assert.That(hooks.Count(h => h.Kind == DecorationEffectKind.Particles), Is.EqualTo(1));
        }

        private static DecorationPlacement FindContent(DecorationContentKind kind, in DecorationContext context)
        {
            foreach (DecorationShowcaseEntry entry in DecorationShowcaseCatalog.CreateEntries())
            {
                if (entry.Source != DecorationShowcaseEntrySource.RegisteredDecoration)
                    continue;
                if (!DecorationShowcaseRealizer.TryCreate(in entry, in context, out DecorationShowcaseRealization realization) ||
                    realization.Kind != DecorationShowcaseRealizationKind.Decoration)
                    continue;
                if (DecorationContentVariants.KindOf(realization.Decoration.Variant) == kind)
                    return realization.Decoration;
            }
            Assert.Fail($"Canonical catalogue did not expose {kind}.");
            return default;
        }

        private static DecorationContext Context() => new DecorationContext
        {
            WorldSeed = 0x50525031u,
            StructureId = 0x50525032u,
            SpaceId = 0x50525033u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, 17u),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };
    }
}
