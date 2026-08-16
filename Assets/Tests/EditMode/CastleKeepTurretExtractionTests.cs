using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretExtractionTests
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
        public void CompatibilityKeepDelegatesCornerTurretPlacement()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string keep = File.ReadAllText(Path.Combine(runtime, "CastleKeepRealizer.cs"));
            string turrets = File.ReadAllText(Path.Combine(runtime, "CastleKeepTurretRealizer.cs"));

            StringAssert.Contains("CastleKeepTurretRealizer.Build(ref brush, in plan, min, size, baseY)", keep);
            StringAssert.DoesNotContain("private static void BuildCornerTurrets", keep);
            StringAssert.Contains("CastleTowerRealizer.Build(", turrets);
            StringAssert.Contains("for (int i = 0; i < 4; i++)", turrets);
            StringAssert.DoesNotContain("CastleRoomFurnisher", turrets);
            StringAssert.DoesNotContain("SpiralStair", turrets);
            StringAssert.DoesNotContain("new Random(", turrets);
        }
    }
}
