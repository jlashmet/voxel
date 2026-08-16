using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedCaveDecoratorTests
    {
        [Test]
        public void PlannedCaveDecorationAddsAuthoredMaterialsWithoutBlockingEntryCirculation()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(16384, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var rock = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                rock.FillBulk(new int3(8, 8, 8), new int3(240, 180, 240), Mat.Stone);

                var constraints = new CavePlanningConstraints
                {
                    Entrance = new int3(120, 70, 120),
                    EntranceToMainOffset = new int3(0, 27, 0),
                    MainRadii = new int3(82, 36, 90),
                    SecondaryChamberCount = 3,
                    SecondaryMinRadii = new int3(30, 22, 32),
                    SecondaryMaxRadii = new int3(46, 31, 52),
                    MinimumHorizontalSpread = 55,
                    MaximumHorizontalSpread = 95,
                    VerticalSpread = 12,
                    PassageWidth = 20,
                    PassageHeight = 30,
                };
                CavePlan plan = CavePlanner.Create(97u, in constraints);

                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                CaveRealizer.Build(ref brush, plan);
                CastlePlannedCaveDecorator.Build(ref brush, plan);

                CaveChamberPlan entry = plan.Chambers[plan.EntryChamberId];
                int floor = entry.Centre.y - entry.Radii.y + 1;
                int poolRadius = math.max(8, math.min(entry.Radii.x, entry.Radii.z) / 2);

                Assert.AreEqual(Mat.DarkStone,
                    brush.Get(entry.Centre.x, floor, entry.Centre.z),
                    "The planned cave should retain a dry causeway through its entry pool.");
                Assert.AreEqual(Mat.Empty,
                    brush.Get(entry.Centre.x, floor + 3, entry.Centre.z),
                    "Decoration must not block the designed entry circulation above the causeway.");

                int waterSamples = 0;
                for (int dz = -poolRadius; dz <= poolRadius; dz += 4)
                for (int dx = -poolRadius; dx <= poolRadius; dx += 4)
                {
                    if (dx * dx + dz * dz > poolRadius * poolRadius) continue;
                    if (brush.Get(entry.Centre.x + dx, floor + 1, entry.Centre.z + dz) == Mat.Water)
                        waterSamples++;
                }
                Assert.Greater(waterSamples, 4,
                    "The entry chamber should retain visible planned-cave water around the causeway.");

                int side = -1;
                int lightX = entry.Centre.x + side * math.max(8, entry.Radii.x / 4);
                int lightZ = entry.Centre.z + math.max(6, entry.Radii.z / 6);
                int lightY = entry.Centre.y - math.max(1, entry.Radii.y / 4);
                Assert.AreEqual(Mat.Glass, brush.Get(lightX, lightY, lightZ),
                    "The deterministic chamber light marker should be present after decoration.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
