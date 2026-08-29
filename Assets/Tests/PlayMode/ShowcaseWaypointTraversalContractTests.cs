using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseWaypointTraversalContractTests
    {
        [Test]
        public void AnchoredVerticalBandRejectsFlatOrAirborneFalseArrival()
        {
            const float anchorY = 23.8f;
            const float expectedRise = 9.2f;
            const float tolerance = 0.75f;

            Assert.That(
                ShowcaseWaypointTraversalContract.Matches(
                    anchorY + expectedRise + 0.2f,
                    grounded: true,
                    requireGrounded: true,
                    hasVerticalAnchor: true,
                    verticalAnchorY: anchorY,
                    expectedYOffset: expectedRise,
                    yTolerance: tolerance),
                Is.True,
                "A grounded production-motor feet position inside the authored vertical band must arrive.");

            Assert.That(
                ShowcaseWaypointTraversalContract.Matches(
                    anchorY,
                    grounded: true,
                    requireGrounded: true,
                    hasVerticalAnchor: true,
                    verticalAnchorY: anchorY,
                    expectedYOffset: expectedRise,
                    yTolerance: tolerance),
                Is.False,
                "Matching X/Z while remaining on the base elevation must not certify ascent.");

            Assert.That(
                ShowcaseWaypointTraversalContract.Matches(
                    anchorY + expectedRise,
                    grounded: false,
                    requireGrounded: true,
                    hasVerticalAnchor: true,
                    verticalAnchorY: anchorY,
                    expectedYOffset: expectedRise,
                    yTolerance: tolerance),
                Is.False,
                "An airborne fly/jump pass through the expected Y band must not certify grounded traversal.");
        }
    }
}
