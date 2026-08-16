using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepShellBoundaryTests
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
        public void KeepStageZeroDelegatesOnlyStructuralShellGeometry()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string shell = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepShellRealizer.cs"));

            StringAssert.Contains("CastleKeepShellRealizer.Build(ref brush, min, size, baseY)", keep);
            StringAssert.DoesNotContain("private static void BuildShell", keep,
                "KeepRealizer must not retain a second shell implementation.");

            StringAssert.Contains("brush.HollowBox(", shell);
            StringAssert.Contains("brush.FillBulk(", shell);
            StringAssert.DoesNotContain("CastleRoomFurnisher", shell);
            StringAssert.DoesNotContain("SpiralStair", shell);
            StringAssert.DoesNotContain("CastleTowerRealizer", shell);
            StringAssert.DoesNotContain("new Random(", shell);
            StringAssert.DoesNotContain("CastleSeedPartition", shell);
        }
    }
}
