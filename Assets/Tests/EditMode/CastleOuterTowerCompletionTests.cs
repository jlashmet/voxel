using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleOuterTowerCompletionTests
    {
        [Test]
        public void CompletionPreservesAlreadyPlannedOuterTowerAppearance()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 137u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(137u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            CastleTowerPlacementSpec planned = spatial.Towers[0];
            planned.HeightVariation = planned.HeightVariation == 8 ? 9 : 8;
            planned.HasRoof = !planned.HasRoof;
            spatial.Towers[0] = planned;

            CastleSpatialPlan completed =
                CastleSpatialPlanCompletion.AttachTowerVariation(in plan, spatial);

            Assert.AreEqual(planned.HeightVariation, completed.Towers[0].HeightVariation,
                "Completion redrew a height choice that spatial planning had already materialized.");
            Assert.AreEqual(planned.HasRoof, completed.Towers[0].HasRoof,
                "Completion redrew a roof choice that spatial planning had already materialized.");
        }

        [Test]
        public void TowerVariationCompletionDoesNotOwnOuterTowerSeedPolicy()
        {
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot(), "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));

            StringAssert.DoesNotContain("0x2000", completion,
                "Completion must preserve outer-tower appearance rather than redraw planner seed choices.");
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                dir = dir.Parent;

            Assert.NotNull(dir, "Could not locate project root containing Assets/.");
            return dir.FullName;
        }
    }
}
