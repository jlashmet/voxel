using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRoomAccentRealizerTests
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
        public void PlannedAccentRealizerContainsNoRandomDecisions()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRoomAccentRealizer.cs"));

            StringAssert.Contains("CastleRoomAccentSpec[] accents", source);
            StringAssert.Contains("accent.LocalX", source);
            StringAssert.Contains("accent.LocalZ", source);
            StringAssert.Contains("accent.Radius", source);
            StringAssert.Contains("accent.Height", source);
            StringAssert.DoesNotContain("Random", source);
            StringAssert.DoesNotContain("NextInt", source);
            StringAssert.DoesNotContain("NextBool", source);
        }

        [Test]
        public void FrozenAccentSpecControlsPlacementAndDimensions()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(2048, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 200_000);
                var min = new int3(64, 0, 80);
                const int floorY = 24;
                var accents = new[]
                {
                    new CastleRoomAccentSpec(0, 22, 31, 4, 9),
                };

                CastleRoomAccentRealizer.Build(ref brush, min, floorY, accents);

                int x = min.x + accents[0].LocalX;
                int z = min.z + accents[0].LocalZ;
                Assert.AreEqual(Mat.Wood, brush.Get(x, floorY + 3, z),
                    "Accent column did not start at the frozen local placement.");
                Assert.AreEqual(Mat.Wood, brush.Get(x, floorY + 11, z),
                    "Accent height did not reach the planned top voxel.");
                Assert.AreEqual(Mat.Gold,
                    brush.Get(x - accents[0].Radius, floorY + 7,
                              z - accents[0].Radius - 1),
                    "Accent shelf did not use the frozen radius.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
