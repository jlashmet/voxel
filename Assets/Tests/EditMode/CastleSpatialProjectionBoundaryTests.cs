using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialProjectionBoundaryTests
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
        public void SpatialCastleHasOneCompatibilityProjectionSeam()
        {
            string apiDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api");
            string[] projectionFiles = Directory.GetFiles(
                apiDirectory, "CastleSpatial*Projection.cs", SearchOption.TopDirectoryOnly);

            Assert.AreEqual(1, projectionFiles.Length,
                "Keep/gate compatibility projection must have exactly one source of truth.");
            Assert.AreEqual(
                "CastleSpatialProjection.cs",
                Path.GetFileName(projectionFiles[0]));

            string projection = File.ReadAllText(projectionFiles[0]);
            string layout = File.ReadAllText(Path.Combine(apiDirectory, "CastlePlan.cs"));
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);
            StringAssert.Contains("LegacyKeepCentreZOffset = 60", layout);
            StringAssert.Contains("CastleGateGeometryResolver.Resolve", projection);
            StringAssert.Contains("CastleApproachFrame.FromGate", projection);
        }

        [Test]
        public void RuntimeReadinessUsesPureKeepProjectionWithoutReenteringFullProjection()
        {
            string readiness = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialBuildReadiness.cs"));

            StringAssert.Contains("CastleSpatialProjection.ProjectKeepPlan(", readiness,
                "Runtime readiness may derive keep-local geometry from the pure keep projection.");
            StringAssert.DoesNotContain("CastleSpatialProjection.Create(", readiness,
                "Runtime readiness must not re-enter the validating full projection while admitting the plan.");
        }
    }
}
