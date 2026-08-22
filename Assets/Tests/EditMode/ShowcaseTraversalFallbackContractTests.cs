using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseTraversalFallbackContractTests
    {
        [Test]
        public void TraversalStartsFromFallbackSafeVisibilityInsteadOfFullNearConvergence()
        {
            string source = File.ReadAllText(
                "Assets/Tests/PlayMode/ShowcaseTraversalPerformanceTests.cs");
            string workflow = File.ReadAllText(".github/workflows/tests-single.yml");

            StringAssert.Contains(
                "WaitForFallbackSafeVisibleCoverage(camera, far, 1200)", source);
            StringAssert.Contains(
                "bool nearIncomplete = NearCoverageIsIncomplete(in last);", source);
            StringAssert.Contains(
                "bool fallbackSafe = !nearIncomplete || far.HoleRadiusMetres <= 0.05f;", source);
            StringAssert.Contains(
                "bool ready = last.VisibleSolidChunks > 0 && fallbackSafe;", source);
            StringAssert.DoesNotContain(
                "WaitForVisibleCoverage(camera, 1800)", source,
                "The traversal gate must exercise far fallback while near coverage streams rather than waiting for Editor-only full convergence.");
            StringAssert.Contains(
                "steps.request.outputs.test != 'VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap'",
                workflow,
                "The exact full traversal must use the refreshed checked-in VoxelShowcase bake so PlayMode plus the visible standalone capture fits the five-minute CI budget.");
        }

        [Test]
        public void SingleFrameHitchAndCoverageAcceptanceBelongToTheRealPlayer()
        {
            string source = File.ReadAllText(
                "Assets/Tests/PlayMode/ShowcaseTraversalPerformanceTests.cs");
            string capture = File.ReadAllText("tools/showcase-player-capture.sh");
            string validator = File.ReadAllText("tools/validate-showcase-traversal.py");

            StringAssert.Contains(
                "frameTimesMs, \"continuous traversal\", enforceSingleFrameMax: false",
                source,
                "The Editor loop must retain percentile/correctness guards but must not own the production single-frame hitch threshold.");
            StringAssert.Contains(
                "AssertMovingFrameTimes(frameTimesMs, \"repeated LOD-boundary traversal\");",
                source,
                "The independent LOD sweep must retain its existing Editor single-frame guard.");
            StringAssert.Contains(
                "python3 tools/validate-showcase-traversal.py", capture,
                "The exact visible traversal profile must execute the production-player acceptance validator.");
            StringAssert.Contains("MAX_SINGLE_FRAME_MS = 33.34", validator);
            StringAssert.Contains("if worst_max >= MAX_SINGLE_FRAME_MS:", validator);
            StringAssert.Contains("if worst_missing != 0:", validator);
            StringAssert.Contains("if worst_reappeared != 0:", validator);
            StringAssert.Contains("if worst_lease_fail != 0:", validator);
            StringAssert.Contains("if hole > 0.05:", validator,
                "The real-player gate must fail if the far hole opens while near coverage is incomplete.");
        }
    }
}
