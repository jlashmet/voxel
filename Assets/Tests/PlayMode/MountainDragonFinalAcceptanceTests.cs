using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Exact single-filter entry point for the SceneIssue CI transport. Keep the focused structural,
    /// semantic bake, and real-traversal predicates together so one targeted request proves the
    /// naturalized landform contract before the workflow launches the exact built-scene replay.
    /// </summary>
    public sealed class MountainDragonFinalAcceptanceTests
    {
        [Test]
        public void NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay()
        {
            var startup = new MountainDragonStartupBakeAcceptanceTests();
            startup.MountainLandformProgramUsesMultipleAsymmetricMasses();

            var support = new MountainDragonNaturalSupportProgramTests();
            support.MountainPathSupportUsesTaperedMassesWithoutTallRetainingWallBoxes();
            support.OfflineBakeFarFieldSuppressionIsScopedAndRestored();

            var headroom = new MountainDragonPathHeadroomBakeTests();
            headroom.PreparedStartupBakeKeepsPlayerClearAirAboveEveryMountainPathTier();

            var traversal = new ShowcaseWaypointTraversalContractTests();
            traversal.AnchoredVerticalBandRejectsFlatOrAirborneFalseArrival();

            startup.PreparedStartupBakeContainsMountainPathAndSupportedDragonAndExportsEvidence();
        }
    }
}
