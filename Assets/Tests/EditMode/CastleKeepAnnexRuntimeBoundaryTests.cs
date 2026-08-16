using System.IO;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepAnnexRuntimeBoundaryTests
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
        public void TopologyAnnexReadinessRejectsMissingOrInvalidPlans()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(17u);
            Assert.IsTrue(
                CastleKeepAnnexBuildReadiness.TryValidate(
                    in topology, out CastleKeepAnnexBuildReadinessIssue issue));
            Assert.AreEqual(CastleKeepAnnexBuildReadinessIssue.None, issue);

            topology.HasKeepAnnexPlan = false;
            Assert.IsFalse(
                CastleKeepAnnexBuildReadiness.TryValidate(in topology, out issue));
            Assert.AreEqual(CastleKeepAnnexBuildReadinessIssue.MissingPlan, issue);

            topology.HasKeepAnnexPlan = true;
            topology.KeepAnnexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: false,
                hasBellTower: true);
            Assert.IsFalse(
                CastleKeepAnnexBuildReadiness.TryValidate(in topology, out issue));
            Assert.AreEqual(CastleKeepAnnexBuildReadinessIssue.InvalidPlan, issue);
        }

        [Test]
        public void SpatialStageSixConsumesFrozenAnnexPlan()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepAnnexRealizer.cs"));
            string coreKeep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string oriel = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRearOrielRealizer.cs"));

            StringAssert.Contains("CastleKeepAnnexBuildReadiness.TryValidate(", pipeline);
            StringAssert.Contains("_keepAnnexes = topology.KeepAnnexes", pipeline);
            StringAssert.Contains("CastlePlannedKeepAnnexRealizer.Build(", pipeline);
            StringAssert.Contains("CastleKeepAnnexRealizer.Build(ref _brush, in keepPlan)", pipeline,
                "Compatibility builds must retain the historical annex recipe.");
            StringAssert.Contains("CastleKeepAnnexPlanValidator.RequireValid(", planned);
            StringAssert.Contains("annexes.HasRearOriel", planned);
            StringAssert.Contains("CastleRearOrielRealizer.Build(", planned);
            StringAssert.Contains("if (roomPlans == null)", coreKeep,
                "Only the compatibility keep path may realize the historical unconditional oriel.");
            StringAssert.DoesNotContain("private static void BuildRearOriel", coreKeep,
                "Rear-oriel voxel geometry must have a single realizer implementation.");
            StringAssert.Contains("internal static class CastleRearOrielRealizer", oriel);
            StringAssert.DoesNotContain("CastleKeepAnnexPlanner.Create(", planned,
                "Runtime must consume the frozen annex plan rather than plan annexes itself.");
        }
    }
}
