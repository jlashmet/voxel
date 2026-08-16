using System.IO;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSelectiveKeepAnnexBoundaryTests
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
        public void PlannedRealizerHonorsIndependentAnnexFlags()
        {
            string annex = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepAnnexRealizer.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepAnnexRealizer.cs"));

            StringAssert.Contains("internal static void BuildPlanned(", annex);
            StringAssert.Contains("if (annexes.HasGreatHallWing)", annex);
            StringAssert.Contains("if (annexes.HasChapelWing)", annex);
            StringAssert.Contains("annexes.HasBellTower", annex);
            StringAssert.Contains("if (buildBellTower)", annex);

            StringAssert.Contains("CastleKeepAnnexPlanValidator.RequireValid(", planned);
            StringAssert.Contains("CastleKeepAnnexRealizer.BuildPlanned(", planned);
            StringAssert.DoesNotContain("supports only the behavior-preserving", planned);
        }

        [Test]
        public void ChapelWithoutBellTowerIsAValidPlannedCombination()
        {
            var annexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: false,
                hasChapelWing: true,
                hasBellTower: false);

            Assert.IsTrue(
                CastleKeepAnnexPlanValidator.TryValidate(
                    in annexes, out CastleKeepAnnexPlanIssue issue),
                issue.ToString());
        }

        [Test]
        public void BellTowerStillRequiresChapel()
        {
            var annexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: false,
                hasBellTower: true);

            Assert.IsFalse(
                CastleKeepAnnexPlanValidator.TryValidate(
                    in annexes, out CastleKeepAnnexPlanIssue issue));
            Assert.AreEqual(CastleKeepAnnexPlanIssue.BellTowerWithoutChapel, issue);
        }
    }
}
