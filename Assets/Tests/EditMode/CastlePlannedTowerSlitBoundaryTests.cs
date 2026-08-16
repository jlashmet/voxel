using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedTowerSlitBoundaryTests
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
        public void EveryPlannedCastleTowerPathConsumesFrozenSlitPlan()
        {
            AssertConsumesPlannedSlits("CastlePlannedTowerRealizer.cs", "tower.Slits");
            AssertConsumesPlannedSlits("CastleInnerWardTowerRealizer.cs", "tower.Slits");
            AssertConsumesPlannedSlits("CastlePlannedKeepTurretRealizer.cs", "turret.Slits");
        }

        private static void AssertConsumesPlannedSlits(string fileName, string slitExpression)
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", fileName));

            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", source,
                $"{fileName} must realize the slit phases frozen by planning.");
            StringAssert.Contains(slitExpression, source,
                $"{fileName} must hand its own planned slit spec to the tower realizer.");
            StringAssert.DoesNotContain("CastleTowerRealizer.Build(", source,
                $"{fileName} must not fall back to the legacy runtime slit RNG path.");
        }
    }
}
