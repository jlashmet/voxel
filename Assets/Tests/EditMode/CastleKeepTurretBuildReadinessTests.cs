using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretBuildReadinessTests
    {
        [Test]
        public void GeneratedTopologyCarriesRuntimeReadyKeepTurrets()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);

                Assert.IsTrue(
                    CastleKeepTurretBuildReadiness.TryValidate(
                        in topology, out CastleKeepTurretBuildReadinessIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void MissingKeepTurretPlanIsNotRuntimeReady()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(271u);
            topology.KeepTurrets = null;

            Assert.IsFalse(
                CastleKeepTurretBuildReadiness.TryValidate(
                    in topology, out CastleKeepTurretBuildReadinessIssue issue));
            Assert.AreEqual(CastleKeepTurretBuildReadinessIssue.MissingPlan, issue);
        }

        [Test]
        public void InvalidKeepTurretPlanIsNotRuntimeReady()
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(277u);
            CastleKeepTurretSpec[] turrets = topology.KeepTurrets.Snapshot();
            turrets[1].Corner = turrets[0].Corner;
            topology.KeepTurrets = new CastleKeepTurretPlan(turrets);

            Assert.IsFalse(
                CastleKeepTurretBuildReadiness.TryValidate(
                    in topology, out CastleKeepTurretBuildReadinessIssue issue));
            Assert.AreEqual(CastleKeepTurretBuildReadinessIssue.InvalidPlan, issue);
        }
    }
}
