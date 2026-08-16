using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastlePosternInteractionTests
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
        public void ShowcaseAndRuntimeShareThePlannedPosternLeafGeometry()
        {
            string showcase = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleWallDoorRealizer.cs"));
            string geometry = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleWallDoorGeometry.cs"));

            StringAssert.Contains("CanOpenCastlePostern", showcase);
            StringAssert.Contains("TryOpenCastlePostern", showcase);
            StringAssert.Contains("topology.PosternDoor", showcase,
                "Interaction must consume the frozen production recipe, not a historical default.");
            StringAssert.Contains("CastleWallDoorGeometryResolver.Resolve", showcase);
            StringAssert.Contains("geometry.LeafVoxels()", showcase);

            StringAssert.Contains("CastleWallDoorGeometryResolver.Resolve", realizer);
            StringAssert.Contains("CastleWallDoorGeometry.TryGetArchRowSpan", realizer);
            StringAssert.Contains("CastleSegmentFootprint.Contains", geometry);
        }
    }
}
