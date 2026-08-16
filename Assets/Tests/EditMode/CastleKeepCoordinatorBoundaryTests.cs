using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCoordinatorBoundaryTests
    {
        [Test]
        public void CompatibilityKeepCoordinatorDelegatesAllVoxelAuthoring()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("CastleKeepShellRealizer.Build(", source);
            StringAssert.Contains("CastleKeepTurretRealizer.Build(", source);
            StringAssert.Contains("CastleKeepFloorRealizer.BuildCompatibility(", source);
            StringAssert.Contains("CastleKeepCompatibilityCirculationRealizer.Build(", source);
            StringAssert.Contains("CastleKeepFenestrationRealizer.Build(", source);
            StringAssert.Contains("CastleKeepFacadeRealizer.Build(", source);
            StringAssert.Contains("CastleRearOrielRealizer.Build(", source);

            StringAssert.DoesNotContain("brush.", source,
                "The keep coordinator must not author voxels directly.");
            StringAssert.DoesNotContain("VoxelWallRasterizer", source);
            StringAssert.DoesNotContain("new Random(", source);
            StringAssert.DoesNotContain("CastleSeedPartition", source);
            StringAssert.DoesNotContain("BuildWindows(", source);
            StringAssert.DoesNotContain("GrandStair(", source);
            StringAssert.DoesNotContain("SpiralStair(", source);
            StringAssert.DoesNotContain("CourtyardEntrance(", source);
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
