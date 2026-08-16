using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRearOrielRealizerTests
    {
        [Test]
        public void RearOrielRecipeBuildsSupportsTimberAndGlazingFromPlacedKeepPlan()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(2048, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                var plan = new CastlePlan
                {
                    Centre = new int3(128, 100, 128),
                    PlateauHeight = 10,
                    KeepHalfZ = 60,
                    FloorHeight = 46,
                };

                CastleRearOrielRealizer.Build(ref brush, in plan);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int wallZ = plan.Centre.z - plan.KeepHalfZ + 60 + plan.KeepHalfZ * 2;
                int firstFloorY = baseY + plan.FloorHeight * 2;
                int minX = plan.Centre.x + 18;

                Assert.AreEqual(Mat.DarkStone,
                    brush.Get(minX + 3, firstFloorY - 12, wallZ + 3),
                    "planned oriel should retain its masonry support brackets");
                Assert.AreEqual(Mat.Wood,
                    brush.Get(minX, firstFloorY, wallZ),
                    "planned oriel should retain its timber floor/shell");
                Assert.AreEqual(Mat.LitWindow,
                    brush.Get(minX + 5, firstFloorY + 9, wallZ + 18),
                    "planned oriel should retain its glazed rear bays");
                Assert.AreEqual(Mat.DarkStone,
                    brush.Get(minX - 3, firstFloorY + plan.FloorHeight, wallZ),
                    "planned oriel should retain the intermediate masonry course");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void RearOrielRealizerContainsNoExistencePolicyOrRandomness()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRearOrielRealizer.cs"));

            StringAssert.DoesNotContain("HasRearOriel", source,
                "the planner/annex handoff decides whether this component runs");
            StringAssert.DoesNotContain("Random", source);
            StringAssert.DoesNotContain("CastleSeedPartition", source);
            StringAssert.Contains("internal static void Build", source);
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
