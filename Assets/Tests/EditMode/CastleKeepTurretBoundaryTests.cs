using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretBoundaryTests
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
        public void SpatialKeepTurretsConsumeFrozenPlanInsteadOfLegacyTurretRecipe()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string pipeline = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleBuildPipeline.cs"));
            string plannedTurrets = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastlePlannedKeepTurretRealizer.cs"));
            string readiness = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialBuildReadiness.cs"));

            StringAssert.Contains("_keepTurrets = topology.KeepTurrets.Snapshot();", pipeline,
                "Runtime must snapshot planner-owned keep turret variation at admission.");
            StringAssert.Contains("CastlePlannedKeepTurretRealizer.BuildAll(", pipeline,
                "Spatial keep substage 2 must realize the frozen turret plan.");
            StringAssert.Contains("ref _brush, in keepPlan, _keepTurrets", pipeline);

            StringAssert.Contains("turret.HasRoof", plannedTurrets,
                "Planned turret realization must consume the frozen roof choice.");
            StringAssert.DoesNotContain("Random", plannedTurrets);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive", plannedTurrets);

            StringAssert.Contains("MissingKeepTurretPlan", readiness,
                "Runtime-ready admission must reject spatial castles without turret planning.");
            StringAssert.Contains("CastleKeepTurretPlanValidator.TryValidate(", readiness);
        }

        [Test]
        public void RuntimeReadySpatialGatehouseHasNoLegacyFallback()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string readiness = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialBuildReadiness.cs"));

            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", pipeline);
            StringAssert.DoesNotContain("CastlePerimeterRealizer.Gatehouse(", pipeline,
                "Runtime-ready preflight makes a legacy spatial gatehouse fallback unreachable.");
            StringAssert.Contains("MissingGatehousePlan", readiness);
            StringAssert.Contains("InvalidGatehousePlan", readiness);
        }
    }
}
