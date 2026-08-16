using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedLandscapeRealizerTests
    {
        [Test]
        public void PlannedLandscapeRealizerContainsNoPlanningRandomness()
        {
            string file = Path.Combine(
                RepoRoot,
                "Assets",
                "VoxelEngine",
                "Structures",
                "Runtime",
                "CastlePlannedLandscapeRealizer.cs");
            string source = File.ReadAllText(file);

            StringAssert.Contains("CastleLandscapePlan landscape", source);
            StringAssert.Contains("landscape.Decorations", source);
            StringAssert.Contains("CastleLandscapePlanValidator.TryValidate", source);
            StringAssert.DoesNotContain("Random", source);
            StringAssert.DoesNotContain("CastleSeedPartition", source);
            StringAssert.DoesNotContain("NextInt", source);
            StringAssert.DoesNotContain("NextFloat", source);
        }

        [Test]
        public void PlannedShrubUsesFrozenHorizontalPlacementAndMaterialIntent()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                CastlePlan plan = CastlePlanner.Create(new int3(700, 220, 700), 47u);
                int2[] perimeter =
                {
                    new int2(-plan.BaileyHalfX, -plan.BaileyHalfZ),
                    new int2( plan.BaileyHalfX, -plan.BaileyHalfZ),
                    new int2( plan.BaileyHalfX,  plan.BaileyHalfZ),
                    new int2(-plan.BaileyHalfX,  plan.BaileyHalfZ),
                };
                var gate = new CastleGatePlacementSpec
                {
                    EdgeIndex = 0,
                    Centre = new int2(0, -plan.BaileyHalfZ),
                    Outward = new float2(0f, -1f),
                };
                CastleApproachFrame approach = CastleApproachFrame.FromGate(in gate);
                CastleLandscapePlan landscape = CastleLandscapePlanner.Create(
                    in plan, perimeter, in approach);

                CastleLandscapeDecorationSpec first = landscape.Decorations[0];
                Assert.AreEqual(CastleLandscapeDecorationKind.PerimeterMossShrub, first.Kind);
                int worldX = plan.Centre.x + first.Centre.x;
                int worldZ = plan.Centre.z + first.Centre.y;
                int top = plan.Centre.y + plan.PlateauHeight;
                brush.FillColumnBulk(worldX, top, top + 1, worldZ, Mat.Stone);

                CastlePlannedLandscapeRealizer.Build(ref brush, in plan, landscape);

                Assert.AreEqual(Mat.Moss, brush.Get(worldX, top + 1, worldZ));
                Assert.AreEqual(Mat.Stone, brush.Get(worldX, top, worldZ),
                    "Terrain surface under the planned decoration must remain intact.");
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
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
