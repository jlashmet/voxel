using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingModelBoundaryTests
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
        public void CourtyardBuildingsUseOneWallRelativeSemanticModelEndToEnd()
        {
            string api = Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Structures", "Api");
            string runtime = Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");

            string planner = File.ReadAllText(Path.Combine(api, "CastleCourtyardBuildingPlanner.cs"));
            string adapter = File.ReadAllText(Path.Combine(api, "CastleCourtyardBuildingPlacementGeometry.cs"));
            string validator = File.ReadAllText(Path.Combine(api, "CastleSpatialPlanValidator.cs"));
            string pipeline = File.ReadAllText(Path.Combine(runtime, "CastleBuildPipeline.cs"));
            string realizer = File.ReadAllText(Path.Combine(runtime, "CastleCourtyardBuildingRealizer.cs"));

            StringAssert.Contains("CastleCourtyardBuildingPurpose", planner);
            StringAssert.Contains("float2 Tangent", planner);
            StringAssert.Contains("float2 Inward", planner);
            StringAssert.Contains("int Width", planner);
            StringAssert.Contains("int Depth", planner);
            StringAssert.Contains("int Height", planner);
            StringAssert.Contains("DoorCentre", planner);

            StringAssert.Contains("CastleCourtyardBuildingPlanner.Create(in plan, spatial)", adapter);
            StringAssert.DoesNotContain("CastleCourtyardBuildingPlacementGeometry.Plan(", planner,
                "The canonical planner must not call its compatibility adapter and create a recursion cycle.");

            StringAssert.Contains("CourtyardBuildingCountMismatch", validator);
            StringAssert.Contains("CourtyardBuildingIdMismatch", validator);
            StringAssert.Contains("CourtyardBuildingPlacementMismatch", validator);
            StringAssert.Contains("CastleCourtyardBuildingPlacementGeometry.Plan(", validator);

            StringAssert.Contains("CourtyardBuildings", pipeline);
            StringAssert.Contains("CastleCourtyardBuildingSpec[]", pipeline);

            StringAssert.Contains("spec.Tangent", realizer);
            StringAssert.Contains("spec.Inward", realizer);
            StringAssert.Contains("spec.DoorCentre", realizer);
            StringAssert.Contains("spec.Purpose", realizer);

            string combined = planner + "\n" + adapter + "\n" + realizer;
            StringAssert.DoesNotContain("HalfExtents", combined);
            StringAssert.DoesNotContain("EntranceDirection", combined);
            StringAssert.DoesNotContain("RoofRidgeAlongX", combined);
            StringAssert.DoesNotContain("CastleCourtyardBuildingRole", combined);
        }
    }
}
