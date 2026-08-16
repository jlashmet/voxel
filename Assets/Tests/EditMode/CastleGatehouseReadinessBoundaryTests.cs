using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehouseReadinessBoundaryTests
    {
        [Test]
        public void RuntimeReadySpatialCastleRequiresPlannedGatehouse()
        {
            string readiness = ReadApi("CastleSpatialBuildReadiness.cs");
            string preflight = ReadApi("CastleBuildPreflight.cs");
            string pipeline = ReadRuntime("CastleBuildPipeline.cs");
            string realizer = ReadRuntime("CastlePlannedGatehouseRealizer.cs");

            StringAssert.Contains("!topology.HasGatehousePlan", readiness);
            StringAssert.Contains("CastleSpatialBuildReadinessIssue.MissingGatehousePlan", readiness);
            StringAssert.Contains("CastleGatehousePlanValidator.TryValidate", readiness);
            StringAssert.Contains("CastleSpatialBuildReadinessIssue.InvalidGatehousePlan", readiness);

            StringAssert.Contains("MissingGatehousePlan", preflight);
            StringAssert.Contains("InvalidGatehousePlan", preflight);

            StringAssert.Contains("_hasPlannedGatehouse = topology.HasGatehousePlan", pipeline);
            StringAssert.Contains("CastleGatehousePlanValidator.RequireValid", pipeline);
            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", pipeline);

            StringAssert.DoesNotContain("CastleSeedPartition", realizer,
                "Planned gatehouse realization must not derive authored variation from a seed.");
            StringAssert.DoesNotContain("Unity.Mathematics.Random", realizer,
                "Planned gatehouse realization must consume only frozen plan data.");
        }

        private static string ReadApi(string file) => File.ReadAllText(Path.Combine(
            RepoRoot, "Assets", "VoxelEngine", "Structures", "Api", file));

        private static string ReadRuntime(string file) => File.ReadAllText(Path.Combine(
            RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", file));

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
    }
}
