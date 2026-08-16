using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepFacadeTests
    {
        [Test]
        public void FacadePlannerFacesThePrimaryGateFromKeepCentre()
        {
            AssertFace(new int2(0, -120), CastleKeepFace.South);
            AssertFace(new int2(140, -20), CastleKeepFace.East);
            AssertFace(new int2(15, 130), CastleKeepFace.North);
            AssertFace(new int2(-150, 10), CastleKeepFace.West);
        }

        [Test]
        public void ExactDiagonalTiePrefersZFacadeForStableLegacyBias()
        {
            var southEast = new CastleGatePlacementSpec
            {
                Centre = new int2(90, -90),
                Outward = new float2(1f, -1f),
            };
            var northWest = new CastleGatePlacementSpec
            {
                Centre = new int2(-90, 90),
                Outward = new float2(-1f, 1f),
            };

            Assert.AreEqual(
                CastleKeepFace.South,
                CastleKeepFacadePlanner.FacingPrimaryGate(int2.zero, in southEast));
            Assert.AreEqual(
                CastleKeepFace.North,
                CastleKeepFacadePlanner.FacingPrimaryGate(int2.zero, in northWest));
        }

        [Test]
        public void FacadeFrameUsesTheCorrectRectangularKeepExtent()
        {
            var plan = new CastlePlan
            {
                KeepHalfX = 100,
                KeepHalfZ = 70,
            };

            Assert.AreEqual(
                new int2(12, -70),
                CastleKeepFacadeFrame.For(CastleKeepFace.South)
                    .PointFromFacade(in plan, 12, 0));
            Assert.AreEqual(
                new int2(100, 12),
                CastleKeepFacadeFrame.For(CastleKeepFace.East)
                    .PointFromFacade(in plan, 12, 0));
            Assert.AreEqual(
                new int2(-12, 70),
                CastleKeepFacadeFrame.For(CastleKeepFace.North)
                    .PointFromFacade(in plan, 12, 0));
            Assert.AreEqual(
                new int2(-100, -12),
                CastleKeepFacadeFrame.For(CastleKeepFace.West)
                    .PointFromFacade(in plan, 12, 0));
        }

        [Test]
        public void PositiveInsetAlwaysMovesTowardKeepInterior()
        {
            var plan = new CastlePlan
            {
                KeepHalfX = 100,
                KeepHalfZ = 70,
            };

            CastleKeepFacadeFrame east = CastleKeepFacadeFrame.For(CastleKeepFace.East);
            CastleKeepFacadeFrame north = CastleKeepFacadeFrame.For(CastleKeepFace.North);

            Assert.AreEqual(new int2(80, 0), east.PointFromFacade(in plan, 0, 20));
            Assert.AreEqual(new int2(0, 50), north.PointFromFacade(in plan, 0, 20));
            Assert.AreEqual(new int2(-1, 0), east.Inward);
            Assert.AreEqual(new int2(0, -1), north.Inward);
        }

        [Test]
        public void CirculationPlannerRotatesHistoricalRecipeIntoFacadeBasis()
        {
            var plan = KeepPlan();

            CastleKeepCirculationPlan south =
                CastleKeepCirculationPlanner.Create(in plan, CastleKeepFace.South);
            CastleKeepCirculationPlan east =
                CastleKeepCirculationPlanner.Create(in plan, CastleKeepFace.East);
            CastleKeepCirculationPlan north =
                CastleKeepCirculationPlanner.Create(in plan, CastleKeepFace.North);
            CastleKeepCirculationPlan west =
                CastleKeepCirculationPlanner.Create(in plan, CastleKeepFace.West);

            Assert.AreEqual(new int2(0, -80), south.EntranceCentre);
            Assert.AreEqual(new int2(100, 0), east.EntranceCentre);
            Assert.AreEqual(new int2(0, 80), north.EntranceCentre);
            Assert.AreEqual(new int2(-100, 0), west.EntranceCentre);

            Assert.AreEqual(new int2(72, -68), east.GrandStairOrigin);
            Assert.AreEqual(new int2(68, 52), north.GrandStairOrigin);
            Assert.AreEqual(new int2(66, -46), east.SpiralStairCentre);

            AssertValid(in plan, in south);
            AssertValid(in plan, in east);
            AssertValid(in plan, in north);
            AssertValid(in plan, in west);
        }

        [Test]
        public void CompatibilityCirculationOverloadRemainsSouthFacing()
        {
            CastlePlan plan = KeepPlan();
            CastleKeepCirculationPlan legacy = CastleKeepCirculationPlanner.Create(in plan);

            Assert.AreEqual(CastleKeepFace.South, legacy.EntranceFace);
            Assert.AreEqual(new int2(0, -80), legacy.EntranceCentre);
            Assert.AreEqual(new int2(-68, -52), legacy.GrandStairOrigin);
        }

        [Test]
        public void WindowPlannerRotatesFrontAndRearWallsWithFacade()
        {
            CastlePlan plan = KeepPlan();
            CastleKeepWindowPlan windows =
                CastleKeepWindowPlanner.Create(in plan, CastleKeepFace.East);

            int groundFrontCount = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                CastleKeepWindowSpec window = windows.Window(i);
                if (window.Face == CastleKeepWindowFace.Front)
                {
                    Assert.AreEqual(CastleKeepFace.East, window.WallFace);
                    Assert.AreEqual(0, window.DepthAxis);
                    if (window.FloorIndex == 0) groundFrontCount++;
                }
                else
                {
                    Assert.AreEqual(CastleKeepFace.West, window.WallFace);
                    Assert.AreEqual(0, window.DepthAxis);
                }
            }

            Assert.AreEqual(2, groundFrontCount,
                "The centre ground-floor front bay must remain reserved for the entrance.");
            Assert.AreEqual(new int2(92, -48), windows.Window(0).LocalOrigin);
            Assert.IsTrue(
                CastleKeepWindowPlanner.TryValidate(
                    in plan, windows, CastleKeepFace.East, out string error),
                error);
        }

        [Test]
        public void CompatibilityWindowOverloadRemainsSouthFacing()
        {
            CastlePlan plan = KeepPlan();
            CastleKeepWindowPlan windows = CastleKeepWindowPlanner.Create(in plan);

            for (int i = 0; i < windows.Count; i++)
            {
                CastleKeepWindowSpec window = windows.Window(i);
                Assert.AreEqual(2, window.DepthAxis);
                Assert.AreEqual(
                    window.Face == CastleKeepWindowFace.Front
                        ? CastleKeepFace.South
                        : CastleKeepFace.North,
                    window.WallFace);
            }
        }

        private static CastlePlan KeepPlan() => new CastlePlan
        {
            KeepHalfX = 100,
            KeepHalfZ = 80,
            FloorHeight = 46,
            Floors = 5,
        };

        private static void AssertValid(
            in CastlePlan plan,
            in CastleKeepCirculationPlan circulation)
        {
            Assert.IsTrue(CastleKeepCirculationPlanner.TryValidate(
                in plan, in circulation, out CastleKeepCirculationPlanIssue issue),
                issue.ToString());
        }

        private static void AssertFace(int2 gateCentre, CastleKeepFace expected)
        {
            var gate = new CastleGatePlacementSpec
            {
                Centre = gateCentre,
                Outward = math.normalizesafe(new float2(gateCentre.x, gateCentre.y)),
            };
            Assert.AreEqual(
                expected,
                CastleKeepFacadePlanner.FacingPrimaryGate(int2.zero, in gate));
        }
    }
}
