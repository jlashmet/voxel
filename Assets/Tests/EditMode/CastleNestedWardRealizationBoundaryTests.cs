using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleNestedWardRealizationBoundaryTests
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
        public void InnerWardUsesDoorwayInsteadOfFullHeightWallGap()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string postern = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePosternRealizer.cs"));

            StringAssert.Contains(
                "CastlePerimeterRealizer.Walls(\n                ref _brush,\n                in _plan,\n                _innerWardVertices);",
                pipeline,
                "The inner curtain should be realized intact before its doorway is carved.");
            StringAssert.Contains("CastleWallDoorRealizer.CarveArchedOpening(", pipeline);
            StringAssert.Contains("CastleWallDoorRealizer.BuildArchedDoor(", pipeline);
            StringAssert.DoesNotContain("int innerGateWidth", pipeline,
                "A full-height wall split would leave a missing wall section above the inner gate.");
            StringAssert.Contains("CastleWallDoorRealizer.CarveArchedOpening(", postern,
                "Postern and inner gate should share one configurable wall-door primitive.");
        }
    }
}
