using NUnit.Framework;
using UnityEngine;
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

        [Test]
        public void AutomatedHeadingSurfaceAppliesSemanticHeadingWithoutMotorExposure()
        {
            var root = new GameObject("automated-heading-contract");
            root.SetActive(false);
            var showcase = root.AddComponent<VoxelShowcase>();

            try
            {
                showcase.SetAutomatedHeading(63f, -12f);

                Assert.That(Mathf.Abs(Mathf.DeltaAngle(showcase.transform.eulerAngles.y, 63f)),
                            Is.LessThan(0.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(showcase.transform.eulerAngles.x, -12f)),
                            Is.LessThan(0.01f));
                Assert.That(showcase.PlayerGrounded, Is.False,
                            "An uninitialized driver must not fabricate grounded production state.");

                showcase.PlayerWalkSpeedMetresPerSecond = 9.5f;
                Assert.That(showcase.PlayerWalkSpeedMetresPerSecond, Is.EqualTo(9.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
