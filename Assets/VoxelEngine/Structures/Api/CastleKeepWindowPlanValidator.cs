using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure validation for frozen keep-window geometry. Planning may create the apertures, but
    /// runtime admission depends only on this validator and never on the planner itself.
    /// </summary>
    public static class CastleKeepWindowPlanValidator
    {
        private const int FloorBottomInset = 12;

        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepWindowPlan windows,
            out string error)
        {
            CastleKeepWindowSpec[] snapshot = windows?.SnapshotWindows();
            return TryValidate(in plan, snapshot, InferFrontFace(snapshot), out error);
        }

        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepWindowPlan windows,
            CastleKeepFace frontFace,
            out string error) =>
            TryValidate(in plan, windows?.SnapshotWindows(), frontFace, out error);

        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepWindowSpec[] windows,
            out string error) =>
            TryValidate(in plan, windows, InferFrontFace(windows), out error);

        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepWindowSpec[] windows,
            CastleKeepFace frontFace,
            out string error)
        {
            if (windows == null)
            {
                error = "window plan is missing";
                return false;
            }

            int expectedCount = plan.Floors * 6 - 1;
            if (windows.Length != expectedCount)
            {
                error = $"expected {expectedCount} apertures but found {windows.Length}";
                return false;
            }

            for (int i = 0; i < windows.Length; i++)
            {
                CastleKeepWindowSpec window = windows[i];
                if (window.Id != i)
                {
                    error = $"window id {window.Id} is out of order at {i}";
                    return false;
                }
                if (window.FloorIndex < 0 || window.FloorIndex >= plan.Floors ||
                    window.Width <= 0 || window.Height <= 0 || window.Depth <= 0)
                {
                    error = $"window {i} has invalid dimensions or floor";
                    return false;
                }

                CastleKeepFace expectedWall = window.Face == CastleKeepWindowFace.Front
                    ? frontFace
                    : Opposite(frontFace);
                if (window.WallFace != expectedWall)
                {
                    error = $"window {i} is attached to {window.WallFace} instead of {expectedWall}";
                    return false;
                }

                int expectedDepthAxis =
                    window.WallFace == CastleKeepFace.East ||
                    window.WallFace == CastleKeepFace.West
                        ? 0
                        : 2;
                if (window.DepthAxis != expectedDepthAxis)
                {
                    error = $"window {i} uses the wrong depth axis";
                    return false;
                }

                if (!WindowFitsWall(in plan, in window))
                {
                    error = $"window {i} is detached from or leaves its keep wall";
                    return false;
                }

                int expectedY = window.FloorIndex * plan.FloorHeight + FloorBottomInset;
                if (window.BaseYOffset != expectedY ||
                    window.BaseYOffset + window.Height > (window.FloorIndex + 1) * plan.FloorHeight)
                {
                    error = $"window {i} leaves its assigned floor";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static CastleKeepFace InferFrontFace(CastleKeepWindowSpec[] windows)
        {
            if (windows == null || windows.Length == 0)
                return CastleKeepFace.South;

            for (int i = 0; i < windows.Length; i++)
            {
                CastleKeepWindowSpec window = windows[i];
                if (window.Face == CastleKeepWindowFace.Front)
                    return window.WallFace;
                if (window.Face == CastleKeepWindowFace.Rear)
                    return Opposite(window.WallFace);
            }

            return CastleKeepFace.South;
        }

        private static bool WindowFitsWall(
            in CastlePlan plan,
            in CastleKeepWindowSpec window)
        {
            int minX = window.LocalOrigin.x;
            int minZ = window.LocalOrigin.y;
            int maxX = minX + (window.DepthAxis == 0 ? window.Depth : window.Width) - 1;
            int maxZ = minZ + (window.DepthAxis == 2 ? window.Depth : window.Width) - 1;

            if (minX < -plan.KeepHalfX || maxX > plan.KeepHalfX ||
                minZ < -plan.KeepHalfZ || maxZ > plan.KeepHalfZ)
                return false;

            switch (window.WallFace)
            {
                case CastleKeepFace.South:
                    return minZ == -plan.KeepHalfZ;
                case CastleKeepFace.East:
                    return maxX == plan.KeepHalfX;
                case CastleKeepFace.North:
                    return maxZ == plan.KeepHalfZ;
                case CastleKeepFace.West:
                    return minX == -plan.KeepHalfX;
                default:
                    return false;
            }
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
