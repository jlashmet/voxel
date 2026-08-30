using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StructurePlacementInputRouterTests
    {
        [Test]
        public void InactiveMode_ConsumesNothingAndLeavesOrdinaryControlsAvailable()
        {
            var selection = new StructurePlacementSelection(new[] { "Dragon", "Tower" });
            var router = new StructurePlacementInputRouter(selection);
            int commits = 0;

            StructurePlacementInputResult result = router.Route(
                scrollDelta: 1,
                commitPressed: true,
                commit: _ => { commits++; return true; });

            Assert.That(result.ConsumeScroll, Is.False);
            Assert.That(result.ConsumeCommitControl, Is.False);
            Assert.That(result.PlacementCommitted, Is.False);
            Assert.That(commits, Is.Zero);
            Assert.That(router.Active, Is.False);
        }

        [Test]
        public void ActiveMode_ConsumesScrollAndCommitEvenWhenPlacementCannotComplete()
        {
            var selection = new StructurePlacementSelection(new[] { "Dragon", "Tower" });
            var router = new StructurePlacementInputRouter(selection);
            router.Begin();

            StructurePlacementInputResult result = router.Route(
                scrollDelta: 1,
                commitPressed: true,
                commit: _ => false);

            Assert.That(result.ConsumeScroll, Is.True,
                "Active structure selection owns wheel input instead of brush radius.");
            Assert.That(result.ConsumeCommitControl, Is.True,
                "Active structure selection owns the Space/commit edge even when placement fails.");
            Assert.That(result.PlacementCommitted, Is.False);
            Assert.That(result.SelectedIndex, Is.EqualTo(1));
            Assert.That(router.Active, Is.True,
                "A rejected placement remains selectable for a later valid commit.");
        }

        [Test]
        public void SuccessfulCommit_IsOneShotAndFollowingFrameNoLongerConsumesOrdinaryControls()
        {
            var selection = new StructurePlacementSelection(new[] { "Dragon" });
            var router = new StructurePlacementInputRouter(selection);
            router.Begin();
            int commits = 0;

            StructurePlacementInputResult first = router.Route(
                scrollDelta: 0,
                commitPressed: true,
                commit: _ => { commits++; return true; });
            StructurePlacementInputResult second = router.Route(
                scrollDelta: -1,
                commitPressed: true,
                commit: _ => { commits++; return true; });

            Assert.That(first.PlacementCommitted, Is.True);
            Assert.That(first.ConsumeCommitControl, Is.True);
            Assert.That(second.ConsumeScroll, Is.False);
            Assert.That(second.ConsumeCommitControl, Is.False);
            Assert.That(second.PlacementCommitted, Is.False);
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(router.Active, Is.False);
        }
    }
}
