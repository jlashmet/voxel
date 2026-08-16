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

        [Test]
        public void PlannedKeepTurretsConsumeSharedProjectedBounds()
        {
            string turrets = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepTurretRealizer.cs"));

            StringAssert.Contains("CastleSpatialProjection.KeepMinimum(", turrets);
            StringAssert.Contains("CastleSpatialProjection.KeepSize(", turrets);
            StringAssert.DoesNotContain("CastleSpatialProjection.ActualKeepCentre(", turrets,
                "Planned turret placement should consume the projected volume instead of re-deriving its centre.");
            StringAssert.DoesNotContain("keepPlan.KeepHalfX * 2", turrets,
                "Planned turret placement must not rebuild keep width locally.");
            StringAssert.DoesNotContain("keepPlan.KeepHalfZ * 2", turrets,
                "Planned turret placement must not rebuild keep depth locally.");
        }
    }
}
