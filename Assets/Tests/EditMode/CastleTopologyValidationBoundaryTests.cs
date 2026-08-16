using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTopologyValidationBoundaryTests
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
        public void SpatialValidationRejectsTopologyBeforeGeometryAndPreflightUsesIt()
        {
            string api = Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Structures", "Api");
            string spatialValidator = File.ReadAllText(Path.Combine(
                api, "CastleSpatialPlanValidator.cs"));
            string preflight = File.ReadAllText(Path.Combine(api, "CastleBuildPreflight.cs"));
            string layoutPlanner = File.ReadAllText(Path.Combine(api, "CastleLayoutPlanner.cs"));

            int topologyValidation = spatialValidator.IndexOf(
                "CastleTopologyPlanValidator.TryValidate");
            int geometryValidation = spatialValidator.IndexOf("int2[] outer =");

            Assert.GreaterOrEqual(topologyValidation, 0,
                "Spatial validation must re-check embedded semantic topology.");
            Assert.Greater(geometryValidation, topologyValidation,
                "Topology grammar must fail before geometry-specific validation.");
            StringAssert.Contains("CastleSpatialPlanIssue.InvalidTopology", spatialValidator);
            StringAssert.Contains("CastleSpatialPlanValidator.TryValidate(", preflight,
                "Runtime-ready preflight must inherit the spatial topology trust boundary.");
            StringAssert.Contains("CastleTopologyPlanValidator.TryValidate(", layoutPlanner,
                "Generated topology must validate before spatial planning begins.");
        }
    }
}
