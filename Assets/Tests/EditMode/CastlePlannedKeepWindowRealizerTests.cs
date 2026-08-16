using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedKeepWindowRealizerTests
    {
        [Test]
        public void PlannedWindowRealizerDoesNotChooseApertureTopology()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets",
                "VoxelEngine",
                "Structures",
                "Runtime",
                "CastlePlannedKeepWindowRealizer.cs"));

            StringAssert.Contains("CastleKeepWindowSpec[] windows", source);
            StringAssert.Contains("window.HasLitGlazing", source);
            StringAssert.DoesNotContain("mainEntrance", source);
            StringAssert.DoesNotContain("CastleKeepWindowPlanner.Create", source);
            StringAssert.DoesNotContain("CastleSeedPartition", source);
            StringAssert.DoesNotContain("Random", source);
        }

        [Test]
        public void PlannedWindowsCarveRearAndGlazeFrontFromFrozenSpecs()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                CastlePlan plan = CastlePlanner.Create(new int3(700, 200, 700), 31u);
                CastleKeepWindowSpec[] windows =
                    CastleKeepWindowPlanner.Create(in plan).SnapshotWindows();

                CastleKeepWindowSpec front = default;
                CastleKeepWindowSpec rear = default;
                bool foundFront = false;
                bool foundRear = false;
                for (int i = 0; i < windows.Length; i++)
                {
                    if (!foundFront && windows[i].Face == CastleKeepWindowFace.Front &&
                        windows[i].HasLitGlazing)
                    {
                        front = windows[i];
                        foundFront = true;
                    }
                    if (!foundRear && windows[i].Face == CastleKeepWindowFace.Rear)
                    {
                        rear = windows[i];
                        foundRear = true;
                    }
                }
                Assert.IsTrue(foundFront && foundRear);

                int keepCentreZ = plan.Centre.z + CastleLayout.LegacyKeepCentreZOffset;
                var worldKeepCentre = new int2(plan.Centre.x, keepCentreZ);
                int baseY = plan.Centre.y + plan.PlateauHeight;
                int rearX = worldKeepCentre.x + rear.LocalOrigin.x;
                int rearY = baseY + rear.BaseYOffset;
                int rearZ = worldKeepCentre.y + rear.LocalOrigin.y;
                brush.Box(
                    new int3(rearX, rearY, rearZ),
                    new int3(rear.Width, rear.Height, rear.Depth),
                    Mat.Stone);

                CastlePlannedKeepWindowRealizer.BuildAll(
                    ref brush, in plan, windows);

                int frontX = worldKeepCentre.x + front.LocalOrigin.x;
                int frontY = baseY + front.BaseYOffset;
                int frontZ = worldKeepCentre.y + front.LocalOrigin.y;
                Assert.AreEqual(
                    Mat.LitWindow,
                    brush.Get(frontX + 4, frontY + 6, frontZ + 2),
                    "Planned front glazing was not realized at the frozen aperture.");
                Assert.AreEqual(
                    Mat.DarkStone,
                    brush.Get(frontX + front.Width / 2, frontY + 6, frontZ + 2),
                    "Planned front glazing lost its vertical mullion.");
                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(rearX + rear.Width / 2, rearY + 6, rearZ + 2),
                    "Planned rear aperture was not carved from the existing wall patch.");
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
