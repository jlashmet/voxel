using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialGatehouseAdmissionTests
    {
        [Test]
        public void SpatialPipelineHasNoUnplannedGatehouseFallback()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets", "VoxelEngine", "Structures", "Runtime", "CastleBuildPipeline.cs"));

            StringAssert.Contains("CastleBuildPreflight.EvaluateRuntimeReady(", source,
                "Spatial admission must prove all required sub-plans before mutation.");
            StringAssert.Contains("CastleGatehousePlan gatehouse = topology.Gatehouse;", source);
            StringAssert.Contains("CastleGatehousePlanValidator.RequireValid(in gatehouse);", source);
            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", source);
            StringAssert.DoesNotContain("_hasPlannedGatehouse", source,
                "Runtime-ready spatial builds cannot make gatehouse availability optional.");
            StringAssert.DoesNotContain("CastlePerimeterRealizer.Gatehouse(", source,
                "A spatial castle may not fall back to runtime-authored gatehouse geometry.");
        }

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
