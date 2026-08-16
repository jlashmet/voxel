using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepFacadeWindowCompletionTests
    {
        [Test]
        public void DefaultValidationInfersEveryCardinalWindowFacade()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(320, 180, 420), 71u);
            CastleKeepFace[] faces =
            {
                CastleKeepFace.South,
                CastleKeepFace.East,
                CastleKeepFace.North,
                CastleKeepFace.West,
            };

            for (int i = 0; i < faces.Length; i++)
            {
                CastleKeepWindowPlan windows = CastleKeepWindowPlanner.Create(in plan, faces[i]);
                Assert.IsTrue(
                    CastleKeepWindowPlanner.TryValidate(in plan, windows, out string error),
                    $"{faces[i]} facade failed inferred validation: {error}");
            }
        }

        [Test]
        public void RuntimeReadyPlanningAlignsWindowsWithPlannedEntranceFacade()
        {
            bool sawRotatedFacade = false;

            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(new int3(320, 180, 420), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                CastleSpatialPlan completed = CastleTerrainPlanning.Resolve(
                    in plan, spatial, seed ^ 0x4B465743u);

                CastleKeepFace frontFace = completed.KeepCirculation.EntranceFace;
                if (frontFace != CastleKeepFace.South)
                    sawRotatedFacade = true;

                Assert.IsTrue(
                    CastleKeepWindowPlanner.TryValidate(
                        in plan, completed.KeepWindows, out string error),
                    $"seed {seed}: runtime-ready windows failed inferred validation: {error}");

                for (int i = 0; i < completed.KeepWindows.Length; i++)
                {
                    CastleKeepWindowSpec window = completed.KeepWindows[i];
                    CastleKeepFace expected = window.Face == CastleKeepWindowFace.Front
                        ? frontFace
                        : Opposite(frontFace);
                    Assert.AreEqual(
                        expected,
                        window.WallFace,
                        $"seed {seed}, window {window.Id}: aperture facade drifted from circulation");
                }
            }

            Assert.IsTrue(sawRotatedFacade,
                "Seed coverage did not exercise a non-south keep entrance/window facade.");
        }

        private static CastleKeepFace Opposite(CastleKeepFace face)
        {
            switch (face)
            {
                case CastleKeepFace.South: return CastleKeepFace.North;
                case CastleKeepFace.East: return CastleKeepFace.West;
                case CastleKeepFace.North: return CastleKeepFace.South;
                case CastleKeepFace.West: return CastleKeepFace.East;
                default: return CastleKeepFace.North;
            }
        }
    }
}
