using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretClueAnomalyPlannerTests
    {
        [Test]
        public void SameInputsProduceSameAnomalyPlan()
        {
            var context = new SecretClueLocalContext(72, 80, 35, 65, 10);

            SecretClueAnomalyPlan first = SecretClueAnomalyPlanner.Resolve(
                164351,
                "secret/clue/approach",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Environmental,
                in context);
            SecretClueAnomalyPlan second = SecretClueAnomalyPlanner.Resolve(
                164351,
                "secret/clue/approach",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Environmental,
                in context);

            Assert.Multiple(() =>
            {
                Assert.That(second.Motif, Is.EqualTo(first.Motif));
                Assert.That(second.PrimaryContrast, Is.EqualTo(first.PrimaryContrast));
                Assert.That(second.SecondaryContrast, Is.EqualTo(first.SecondaryContrast));
                Assert.That(second.ActionIntent, Is.EqualTo(first.ActionIntent));
                Assert.That(second.StrengthPercent, Is.EqualTo(first.StrengthPercent));
            });
        }

        [Test]
        public void BreakableAndNaturalRoutesUseDifferentCompatibleLanguages()
        {
            var context = new SecretClueLocalContext(75, 90, 85, 70, 5);

            SecretClueAnomalyPlan breakable = SecretClueAnomalyPlanner.Resolve(
                77,
                "secret/clue/wall",
                SecretRouteKind.BreakableBarrier,
                SecretClueChannel.Visual,
                in context);
            SecretClueAnomalyPlan natural = SecretClueAnomalyPlanner.Resolve(
                77,
                "secret/clue/trail",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Environmental,
                in context);

            Assert.Multiple(() =>
            {
                Assert.That(SecretClueAnomalyPlanner.IsCompatible(SecretRouteKind.BreakableBarrier, breakable.Motif), Is.True);
                Assert.That(SecretClueAnomalyPlanner.IsCompatible(SecretRouteKind.NaturalTraversal, natural.Motif), Is.True);
                Assert.That(breakable.ActionIntent, Is.EqualTo(SecretClueActionIntent.BreakBarrier));
                Assert.That(natural.ActionIntent, Is.EqualTo(SecretClueActionIntent.TraverseTerrain));
                Assert.That(breakable.Motif, Is.Not.EqualTo(natural.Motif));
            });
        }

        [Test]
        public void LocalNormalityChangesWhichNaturalAnomalyReadsStrongest()
        {
            var denseForest = new SecretClueLocalContext(95, 55, 15, 45, 10);
            var occludedRock = new SecretClueLocalContext(10, 25, 20, 100, 65);

            SecretClueAnomalyPlan forest = SecretClueAnomalyPlanner.Resolve(
                10,
                "secret/clue/context",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Environmental,
                in denseForest);
            SecretClueAnomalyPlan rock = SecretClueAnomalyPlanner.Resolve(
                10,
                "secret/clue/context",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Navigation,
                in occludedRock);

            Assert.Multiple(() =>
            {
                Assert.That(forest.Motif, Is.EqualTo(SecretClueMotifFamily.VegetationDiscontinuity),
                    "A dense forest should make a vegetation-density break the strongest local anomaly.");
                Assert.That(rock.Motif, Is.EqualTo(SecretClueMotifFamily.SightlineGap),
                    "A highly occluded rocky approach should make negative space/sightline the strongest anomaly.");
            });
        }

        [Test]
        public void NearbyRepetitionPenalizesRecentlyUsedMotif()
        {
            var context = new SecretClueLocalContext(95, 55, 15, 45, 10);

            SecretClueAnomalyPlan baseline = SecretClueAnomalyPlanner.Resolve(
                10,
                "secret/clue/repetition",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Environmental,
                in context);
            var nearby = new[] { baseline.Motif, baseline.Motif };
            SecretClueAnomalyPlan varied = SecretClueAnomalyPlanner.Resolve(
                10,
                "secret/clue/repetition",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Environmental,
                in context,
                nearby);

            Assert.That(varied.Motif, Is.Not.EqualTo(baseline.Motif),
                "Repeated nearby motif use should push deterministic selection toward a compatible alternative.");
            Assert.That(SecretClueAnomalyPlanner.IsCompatible(SecretRouteKind.NaturalTraversal, varied.Motif), Is.True);
        }

        [Test]
        public void MechanismRoutesSuggestOperatingRatherThanGenericInvestigation()
        {
            var context = new SecretClueLocalContext(5, 90, 95, 15, 20);

            SecretClueAnomalyPlan leverLike = SecretClueAnomalyPlanner.Resolve(
                91,
                "secret/clue/mechanism",
                SecretRouteKind.PressurePlateMechanism,
                SecretClueChannel.Mechanical,
                in context);

            Assert.Multiple(() =>
            {
                Assert.That(leverLike.ActionIntent, Is.EqualTo(SecretClueActionIntent.OperateMechanism));
                Assert.That(leverLike.Motif, Is.EqualTo(SecretClueMotifFamily.MechanicalTrace));
                Assert.That(leverLike.PrimaryContrast, Is.EqualTo(SecretClueContrastAxis.Alignment));
            });
        }
    }
}
