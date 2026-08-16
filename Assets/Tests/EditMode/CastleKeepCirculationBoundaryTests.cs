using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCirculationBoundaryTests
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
        public void KeepStageThreeDelegatesOnlyCirculationGeometry()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string circulation = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepCirculationRealizer.cs"));

            StringAssert.Contains("CastleKeepCirculationRealizer.Build(", keep);
            StringAssert.DoesNotContain("private static void BuildCirculation", keep,
                "KeepRealizer must not retain a second circulation implementation.");

            StringAssert.Contains("brush.Arch(", circulation);
            StringAssert.Contains("brush.Stairs(", circulation);
            StringAssert.Contains("brush.SpiralStair(", circulation);
            StringAssert.DoesNotContain("CastleRoomFurnisher", circulation);
            StringAssert.DoesNotContain("LitWindow", circulation);
            StringAssert.DoesNotContain("Mat.Cloth", circulation);
            StringAssert.DoesNotContain("new Random(", circulation);
            StringAssert.DoesNotContain("CastleSeedPartition", circulation);
        }
    }
}
