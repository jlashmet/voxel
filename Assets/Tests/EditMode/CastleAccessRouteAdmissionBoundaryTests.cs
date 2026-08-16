using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleAccessRouteAdmissionBoundaryTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;

                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        [Test]
        public void SpatialValidationOwnsAccessRouteAdmissionBeforeRuntime()
        {
            string validator = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanValidator.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("CastleAccessRoute.Create(", validator);
            StringAssert.Contains("CastleAccessRouteValidator.TryValidate(", validator);
            StringAssert.Contains("CastleSpatialPlanIssue.InvalidAccessRoute", validator);
            StringAssert.Contains("CastleBuildPreflight.EvaluateRuntimeReady(", pipeline);
            StringAssert.DoesNotContain("CastleAccessRouteValidator.TryValidate(", pipeline,
                "Runtime must consume the single preflight result rather than grow a second route admission path.");
            StringAssert.DoesNotContain("CastleKeepAnnexBuildReadiness.TryValidate(", pipeline,
                "Runtime must not repeat annex admission after EvaluateRuntimeReady.");
            StringAssert.DoesNotContain("CastleKeepTurretPlanValidator.TryValidate(", pipeline,
                "Runtime must not repeat keep-turret admission after EvaluateRuntimeReady.");
            StringAssert.DoesNotContain("CastleWallPlanValidator.TryValidate(", pipeline,
                "Runtime must not repeat wall admission after spatial/preflight validation.");
        }
    }
}
