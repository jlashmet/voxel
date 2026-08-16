using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepWindowPlanValidatorTests
    {
        [Test]
        public void GeneratedWindowPlanValidatesAcrossFacadeOrientations()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 41u);
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
                    CastleKeepWindowPlanValidator.TryValidate(
                        in plan, windows, faces[i], out string error),
                    $"{faces[i]} window plan failed validation: {error}");
            }
        }

        [Test]
        public void ValidatorRejectsFrozenWindowWithWrongDepthAxis()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 43u);
            CastleKeepWindowPlan planned = CastleKeepWindowPlanner.Create(
                in plan, CastleKeepFace.East);
            CastleKeepWindowSpec[] windows = planned.SnapshotWindows();
            CastleKeepWindowSpec original = windows[0];
            windows[0] = new CastleKeepWindowSpec(
                original.Id,
                original.FloorIndex,
                original.Face,
                original.WallFace,
                original.LocalOrigin,
                original.BaseYOffset,
                original.Width,
                original.Height,
                original.Depth,
                original.DepthAxis == 0 ? 2 : 0,
                original.HasLitGlazing);

            Assert.IsFalse(
                CastleKeepWindowPlanValidator.TryValidate(
                    in plan, windows, CastleKeepFace.East, out string error));
            StringAssert.Contains("wrong depth axis", error);
        }
    }
}
