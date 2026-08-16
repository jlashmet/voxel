using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCirculationFacingTests
    {
        [Test]
        public void EastFacingCirculationCutsEastEntranceAndTurnsGrandStairInward()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(4096, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations);
                var plan = new CastlePlan
                {
                    Centre = new int3(256, 2, 256),
                    PlateauHeight = 4,
                    KeepHalfX = 100,
                    KeepHalfZ = 80,
                    FloorHeight = 46,
                    Floors = 5,
                };
                int baseY = plan.Centre.y + plan.PlateauHeight;
                var keepCentre = new int2(plan.Centre.x, plan.Centre.z);

                // Give the realizer actual masonry to cut on both the intended east facade and
                // the historical south facade. Only the planned facade should receive a doorway.
                brush.Box(
                    new int3(keepCentre.x + plan.KeepHalfX - 8, baseY,
                             keepCentre.y - 36),
                    new int3(12, 42, 72),
                    Mat.Stone);
                brush.Box(
                    new int3(keepCentre.x - 36, baseY,
                             keepCentre.y - plan.KeepHalfZ - 2),
                    new int3(72, 42, 12),
                    Mat.Stone);

                CastleKeepCirculationPlan circulation =
                    CastleKeepCirculationPlanner.Create(in plan, CastleKeepFace.East);
                CastleKeepCirculationRealizer.Build(
                    ref brush, in plan, keepCentre, in circulation);

                int eastX = keepCentre.x + plan.KeepHalfX;
                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(eastX, baseY + 10, keepCentre.y),
                    "The east-facing entrance should cut the east keep wall.");
                Assert.AreEqual(
                    Mat.Stone,
                    brush.Get(eastX, baseY + 10, keepCentre.y + 22),
                    "The entrance must not erase masonry outside its planned width.");

                int southZ = keepCentre.y - plan.KeepHalfZ;
                Assert.AreEqual(
                    Mat.Stone,
                    brush.Get(keepCentre.x, baseY + 10, southZ),
                    "Rotating the entrance must not retain the historical south-wall opening.");

                int2 grand = keepCentre + circulation.GrandStairOrigin;
                CastleKeepFacadeFrame frame = CastleKeepFacadeFrame.For(CastleKeepFace.East);
                const int step = 5;
                int2 tread = grand + frame.Inward * (step * circulation.GrandStairRun);
                Assert.AreEqual(
                    Mat.Wood,
                    brush.Get(
                        tread.x,
                        baseY + 1 + step * circulation.GrandStairRise,
                        tread.y),
                    "The grand stair should climb inward from the selected facade.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void CompletionOwnsFacadeChoiceBeforeRuntime()
        {
            string root = RepoRoot;
            string completion = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));
            string runtime = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepCirculationRealizer.cs"));

            StringAssert.Contains("CastleKeepFacadePlanner.FacingPrimaryGate(", completion);
            StringAssert.Contains("CastleKeepCirculationPlanner.Create(", completion);
            StringAssert.Contains("circulation.EntranceFace != expectedEntranceFace", completion);

            StringAssert.Contains("CastleKeepFacadeFrame.For(circulation.EntranceFace)", runtime);
            StringAssert.DoesNotContain("CastleKeepFacadePlanner.FacingPrimaryGate(", runtime,
                "Runtime must realize the planned facade rather than choose it.");
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
