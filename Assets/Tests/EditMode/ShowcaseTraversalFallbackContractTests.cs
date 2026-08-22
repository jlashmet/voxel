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
        }
    }
}
