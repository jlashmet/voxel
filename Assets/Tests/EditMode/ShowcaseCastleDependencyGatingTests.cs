using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastleDependencyGatingTests
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
        public void SpatialCastleQueuesAndPinsEveryVerticalDependencyLayer()
        {
            string spatial = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string world = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.cs"));

            StringAssert.Contains(
                "ShowcaseCastleDependencyRegionRange.FromCastleBounds(in bounds)", spatial);
            StringAssert.Contains(
                "for (int ry = regionRange.Min.y; ry <= regionRange.MaxInclusive.y; ry++)",
                spatial,
                "The dependency prism must enumerate negative and upper Y layers, not only ground Y.");
            StringAssert.Contains(
                "for (int i = 0; i < _castleRegions.Count; i++)", world);
            StringAssert.Contains("_pendingLoads.Add(required);", world,
                "Castle dependency regions must bypass ordinary camera/surface residency.");
            StringAssert.Contains(
                "if (_castleTerrainQueued && !_hasCastlePlan && _castleRegions.Contains(rc))",
                world,
                "Uncommitted castle dependency regions must be pinned against eviction.");
        }
    }
}
