using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepShellRealizerTests
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
        public void KeepStageZeroDelegatesStructuralShellWithoutDuplicatingIt()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string keep = File.ReadAllText(Path.Combine(runtime, "CastleKeepRealizer.cs"));
            string shell = File.ReadAllText(Path.Combine(runtime, "CastleKeepShellRealizer.cs"));

            StringAssert.Contains(
                "CastleKeepShellRealizer.Build(ref brush, min, size, baseY);", keep);
            StringAssert.DoesNotContain("private static void BuildShell", keep);

            StringAssert.Contains("brush.HollowBox(", shell);
            StringAssert.Contains("brush.FillBulk(", shell);
            StringAssert.DoesNotContain("Random", shell,
                "The structural shell must remain a deterministic geometry-only component.");
            StringAssert.DoesNotContain("CastleSpatialPlanner", shell);
        }
    }
}
