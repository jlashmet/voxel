using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretSlitPlanningTests
    {
        [Test]
        public void RuntimeReadyCastleFreezesHistoricalKeepTurretSlitPhases()
        {
            PlannedCastleBuild build = StructuresComposition.PlanCastleBuild(
                new int3(320, 44, 680), 157u, 0x81B5u);
            CastlePlan dimensions = build.Dimensions;
            CastleSpatialPlan spatial = build.Spatial;
            CastleTopologyPlan topology = spatial.Topology;
            CastleSpatialProjection projection = CastleSpatialProjection.Create(
                in dimensions, spatial);
            CastlePlan keepPlan = projection.KeepPlan;
            CastleKeepTurretPlan turretPlan = topology.KeepTurrets;

            Assert.IsTrue(CastleKeepTurretPlanValidator.TryValidateSlits(
                in keepPlan, turretPlan, out CastleKeepTurretPlanIssue issue),
                issue.ToString());

            int minX = keepPlan.Centre.x - keepPlan.KeepHalfX;
            int minZ = keepPlan.Centre.z - keepPlan.KeepHalfZ + 60;
            int width = keepPlan.KeepHalfX * 2;
            int depth = keepPlan.KeepHalfZ * 2;
            int height = keepPlan.KeepHeight + 30;
            CastleKeepTurretSpec[] turrets = turretPlan.Snapshot();

            for (int i = 0; i < turrets.Length; i++)
            {
                int2 centre = turrets[i].Corner switch
                {
                    CastleKeepTurretCorner.MinXMinZ => new int2(minX, minZ),
                    CastleKeepTurretCorner.MaxXMinZ => new int2(minX + width, minZ),
                    CastleKeepTurretCorner.MinXMaxZ => new int2(minX, minZ + depth),
                    CastleKeepTurretCorner.MaxXMaxZ => new int2(minX + width, minZ + depth),
                    _ => default,
                };
                CastleTowerSlitPlan expected = CastleTowerSlitPlanner.Create(
                    centre, height, keepPlan.FloorHeight);

                Assert.NotNull(turrets[i].Slits, $"turret {i} has no slit plan");
                Assert.AreEqual(expected.FloorCount, turrets[i].Slits.FloorCount);
                for (int floor = 0; floor < expected.FloorCount; floor++)
                {
                    Assert.AreEqual(
                        expected.PhaseRadiansAt(floor),
                        turrets[i].Slits.PhaseRadiansAt(floor),
                        $"turret {i}, floor {floor}: slit phase changed");
                }
            }
        }

        [Test]
        public void MissingKeepTurretSlitsAreNotRuntimeReady()
        {
            CastleKeepTurretPlan basePlan = CastleKeepTurretPlanner.Create(19u);
            CastleKeepTurretSpec[] turrets = basePlan.Snapshot();
            Assert.IsNull(turrets[0].Slits);

            CastlePlan keepPlan = CastlePlanner.Create(int3.zero, 19u);
            Assert.IsFalse(CastleKeepTurretPlanValidator.TryValidateSlits(
                in keepPlan,
                basePlan,
                out CastleKeepTurretPlanIssue issue));
            Assert.AreEqual(CastleKeepTurretPlanIssue.MissingSlitPlan, issue);
        }

        [Test]
        public void PlannedKeepTurretRuntimeConsumesCompletedSlitPlans()
        {
            string root = RepoRoot;
            string terrainPlanning = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Composition", "CastleTerrainPlanning.cs"));
            string readiness = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialBuildReadiness.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepTurretRealizer.cs"));

            StringAssert.Contains("CastleKeepTurretPlanCompletion.Attach", terrainPlanning);
            StringAssert.Contains("CastleKeepTurretPlanValidator.TryValidateSlits", readiness);
            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", realizer);
            StringAssert.Contains("turret.Slits", realizer);
            StringAssert.DoesNotContain("CastleTowerRealizer.Build(\n", realizer);
            StringAssert.DoesNotContain("new Random(", realizer);
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
