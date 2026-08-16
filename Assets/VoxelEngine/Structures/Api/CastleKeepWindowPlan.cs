using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleKeepWindowFace : byte
    {
        Front,
        Rear,
    }

    /// <summary>Pure keep-local geometry for one authored window aperture.</summary>
    public readonly struct CastleKeepWindowSpec
    {
        public readonly int Id;
        public readonly int FloorIndex;
        public readonly CastleKeepWindowFace Face;
        public readonly CastleKeepFace WallFace;
        public readonly int2 LocalOrigin;
        public readonly int BaseYOffset;
        public readonly int Width;
        public readonly int Height;
        public readonly int Depth;
        public readonly int DepthAxis;
        public readonly bool HasLitGlazing;

        /// <summary>Compatibility constructor for the historical south/front, north/rear layout.</summary>
        public CastleKeepWindowSpec(
            int id,
            int floorIndex,
            CastleKeepWindowFace face,
            int2 localOrigin,
            int baseYOffset,
            int width,
            int height,
            int depth,
            bool hasLitGlazing)
            : this(
                id,
                floorIndex,
                face,
                face == CastleKeepWindowFace.Front
                    ? CastleKeepFace.South
                    : CastleKeepFace.North,
                localOrigin,
                baseYOffset,
                width,
                height,
                depth,
                2,
                hasLitGlazing)
        {
        }

        public CastleKeepWindowSpec(
            int id,
            int floorIndex,
            CastleKeepWindowFace face,
            CastleKeepFace wallFace,
            int2 localOrigin,
            int baseYOffset,
            int width,
            int height,
            int depth,
            int depthAxis,
            bool hasLitGlazing)
        {
            Id = id;
            FloorIndex = floorIndex;
            Face = face;
            WallFace = wallFace;
            LocalOrigin = localOrigin;
            BaseYOffset = baseYOffset;
            Width = width;
            Height = height;
            Depth = depth;
            DepthAxis = depthAxis;
            HasLitGlazing = hasLitGlazing;
        }
    }

    /// <summary>Immutable keep-window layout produced before Runtime realization.</summary>
    public sealed class CastleKeepWindowPlan
    {
        private readonly CastleKeepWindowSpec[] _windows;

        internal CastleKeepWindowPlan(CastleKeepWindowSpec[] windows)
        {
            _windows = windows ?? Array.Empty<CastleKeepWindowSpec>();
        }

        public int Count => _windows.Length;

        public CastleKeepWindowSpec Window(int index)
        {
            if ((uint)index >= (uint)_windows.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _windows[index];
        }

        public CastleKeepWindowSpec[] SnapshotWindows() =>
            (CastleKeepWindowSpec[])_windows.Clone();
    }

    /// <summary>
    /// Plans the three-bay keep aperture pattern in a cardinal facade basis. The compatibility
    /// overload remains south-facing; a face-aware caller can rotate front/rear walls without
    /// leaving Runtime to infer an aperture orientation.
    /// </summary>
    public static class CastleKeepWindowPlanner
    {
        private const int WindowWidth = 16;
        private const int WindowDepth = 9;
        private const int FloorBottomInset = 12;

        public static CastleKeepWindowPlan Create(in CastlePlan plan) =>
            Create(in plan, CastleKeepFace.South);

        public static CastleKeepWindowPlan Create(
            in CastlePlan plan,
            CastleKeepFace frontFace)
        {
            if (plan.KeepHalfX <= 0 || plan.KeepHalfZ <= 0 ||
                plan.FloorHeight <= 18 || plan.Floors <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(plan), "Castle keep dimensions must be valid before window planning.");
            }

            var windows = new CastleKeepWindowSpec[plan.Floors * 6 - 1];
            int cursor = 0;
            for (int floor = 0; floor < plan.Floors; floor++)
            {
                int height = floor == 1
                    ? plan.FloorHeight - 14
                    : plan.FloorHeight - 18;
                int yOffset = floor * plan.FloorHeight + FloorBottomInset;

                for (int bay = 0; bay < 3; bay++)
                {
                    bool mainEntrance = floor == 0 && bay == 1;
                    if (!mainEntrance)
                    {
                        windows[cursor] = CreateWindow(
                            in plan,
                            cursor,
                            floor,
                            bay,
                            CastleKeepWindowFace.Front,
                            frontFace,
                            yOffset,
                            height,
                            true);
                        cursor++;
                    }

                    CastleKeepFace rearFace = Opposite(frontFace);
                    windows[cursor] = CreateWindow(
                        in plan,
                        cursor,
                        floor,
                        bay,
                        CastleKeepWindowFace.Rear,
                        rearFace,
                        yOffset,
                        height,
                        false);
                    cursor++;
                }
            }

            var result = new CastleKeepWindowPlan(windows);
            if (!TryValidate(in plan, windows, frontFace, out string error))
                throw new InvalidOperationException($"Planned castle keep windows are invalid: {error}");
            return result;
        }

        /// <summary>
        /// Validates a frozen aperture plan in the facade basis encoded by the plan itself. This
        /// remains compatible with historical south-facing plans while allowing runtime-ready
        /// castles to carry east/north/west-facing keep fronts without a second orientation input.
        /// </summary>
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

        /// <summary>Validates a completed/snapshotted aperture list in its frozen facade basis.</summary>
        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepWindowSpec[] windows,
            out string error) =>
            TryValidate(in plan, windows, InferFrontFace(windows), out error);

        /// <summary>Validates a completed/snapshotted aperture list for the supplied front facade.</summary>
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

        private static CastleKeepWindowSpec CreateWindow(
            in CastlePlan plan,
            int id,
            int floor,
            int bay,
            CastleKeepWindowFace relativeFace,
            CastleKeepFace wallFace,
            int yOffset,
            int height,
            bool lit)
        {
            CastleKeepFacadeFrame frame = CastleKeepFacadeFrame.For(wallFace);
            int tangentHalf = frame.TangentHalfExtent(in plan);
            int centreTangent = (bay - 1) * (tangentHalf / 2);
            int startTangent = centreTangent - WindowWidth / 2;
            int endTangent = startTangent + WindowWidth - 1;

            int2 faceStart = frame.PointFromFacade(in plan, startTangent, 0);
            int2 faceEnd = frame.PointFromFacade(in plan, endTangent, 0);
            int2 innerStart = frame.PointFromFacade(in plan, startTangent, WindowDepth - 1);
            int2 innerEnd = frame.PointFromFacade(in plan, endTangent, WindowDepth - 1);
            int2 min = math.min(math.min(faceStart, faceEnd), math.min(innerStart, innerEnd));

            int depthAxis = wallFace == CastleKeepFace.East || wallFace == CastleKeepFace.West
                ? 0
                : 2;
            return new CastleKeepWindowSpec(
                id,
                floor,
                relativeFace,
                wallFace,
                min,
                yOffset,
                WindowWidth,
                height,
                WindowDepth,
                depthAxis,
                lit);
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
