using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepWindowPlannerTests
    {
        [Test]
        public void PlannerPreservesExistingThreeBayFacadeAcrossSeeds()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleKeepWindowPlan windows = CastleKeepWindowPlanner.Create(in plan);

                Assert.AreEqual(plan.Floors * 6 - 1, windows.Count,
                    $"seed {seed}: unexpected aperture count");
                Assert.IsTrue(CastleKeepWindowPlanner.TryValidate(in plan, windows, out string error),
                    $"seed {seed}: {error}");

                int entranceFrontCount = 0;
                int expectedFront = plan.Floors * 3 - 1;
                int expectedRear = plan.Floors * 3;
                int front = 0;
                int rear = 0;
                for (int i = 0; i < windows.Count; i++)
                {
                    CastleKeepWindowSpec window = windows.Window(i);
                    if (window.Face == CastleKeepWindowFace.Front)
                    {
                        front++;
                        Assert.IsTrue(window.HasLitGlazing,
                            $"seed {seed}, window {i}: front glazing drifted");
                        if (window.FloorIndex == 0 && window.LocalOrigin.x == -8)
                            entranceFrontCount++;
                    }
                    else
                    {
                        rear++;
                        Assert.IsFalse(window.HasLitGlazing,
                            $"seed {seed}, window {i}: rear glazing drifted");
                    }
                }

                Assert.AreEqual(expectedFront, front, $"seed {seed}: front count drifted");
                Assert.AreEqual(expectedRear, rear, $"seed {seed}: rear count drifted");
                Assert.AreEqual(0, entranceFrontCount,
                    $"seed {seed}: planner placed a window over the main entrance");
            }
        }

        [Test]
        public void FirstFloorWindowsRetainTallerBedchamberApertures()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 17u);
            CastleKeepWindowPlan windows = CastleKeepWindowPlanner.Create(in plan);

            for (int i = 0; i < windows.Count; i++)
            {
                CastleKeepWindowSpec window = windows.Window(i);
                int expected = window.FloorIndex == 1
                    ? plan.FloorHeight - 14
                    : plan.FloorHeight - 18;
                Assert.AreEqual(expected, window.Height, $"window {i}");
            }
        }
    }
}
