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
        public readonly int2 LocalOrigin;
        public readonly int BaseYOffset;
        public readonly int Width;
        public readonly int Height;
        public readonly int Depth;
        public readonly bool HasLitGlazing;

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
        {
            Id = id;
            FloorIndex = floorIndex;
            Face = face;
            LocalOrigin = localOrigin;
            BaseYOffset = baseYOffset;
            Width = width;
            Height = height;
            Depth = depth;
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
    /// Plans the current three-bay front/rear keep aperture pattern. The ground-floor centre front
    /// bay is omitted for the main entrance, preserving the existing authored keep while moving the
    /// decision about which windows exist out of Runtime.
    /// </summary>
    public static class CastleKeepWindowPlanner
    {
        private const int WindowWidth = 16;
        private const int WindowDepth = 9;
        private const int FloorBottomInset = 12;
        private const int FrontBackInset = 8;

        public static CastleKeepWindowPlan Create(in CastlePlan plan)
        {
            if (plan.KeepHalfX <= 0 || plan.KeepHalfZ <= 0 ||
                plan.FloorHeight <= 18 || plan.Floors <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(plan), "Castle keep dimensions must be valid before window planning.");
            }

            var windows = new CastleKeepWindowSpec[plan.Floors * 6 - 1];
            int cursor = 0;
            int width = plan.KeepHalfX * 2;
            for (int floor = 0; floor < plan.Floors; floor++)
            {
                int height = floor == 1
                    ? plan.FloorHeight - 14
                    : plan.FloorHeight - 18;
                int yOffset = floor * plan.FloorHeight + FloorBottomInset;

                for (int bay = 0; bay < 3; bay++)
                {
                    int localX = -plan.KeepHalfX
                               + width / 4
                               + bay * width / 4
                               - WindowWidth / 2;

                    bool mainEntrance = floor == 0 && bay == 1;
                    if (!mainEntrance)
                    {
                        windows[cursor] = new CastleKeepWindowSpec(
                            cursor,
                            floor,
                            CastleKeepWindowFace.Front,
                            new int2(localX, -plan.KeepHalfZ),
                            yOffset,
                            WindowWidth,
                            height,
                            WindowDepth,
                            true);
                        cursor++;
                    }

                    windows[cursor] = new CastleKeepWindowSpec(
                        cursor,
                        floor,
                        CastleKeepWindowFace.Rear,
                        new int2(localX, plan.KeepHalfZ - FrontBackInset),
                        yOffset,
                        WindowWidth,
                        height,
                        WindowDepth,
                        false);
                    cursor++;
                }
            }

            var result = new CastleKeepWindowPlan(windows);
            if (!TryValidate(in plan, windows, out string error))
                throw new InvalidOperationException($"Planned castle keep windows are invalid: {error}");
            return result;
        }

        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepWindowPlan windows,
            out string error) =>
            TryValidate(in plan, windows?.SnapshotWindows(), out error);

        /// <summary>Validates a completed/snapshotted aperture list without requiring wrapper state.</summary>
        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepWindowSpec[] windows,
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
                if (window.LocalOrigin.x < -plan.KeepHalfX ||
                    window.LocalOrigin.x + window.Width > plan.KeepHalfX)
                {
                    error = $"window {i} leaves the keep width";
                    return false;
                }

                int expectedY = window.FloorIndex * plan.FloorHeight + FloorBottomInset;
                if (window.BaseYOffset != expectedY ||
                    window.BaseYOffset + window.Height > (window.FloorIndex + 1) * plan.FloorHeight)
                {
                    error = $"window {i} leaves its assigned floor";
                    return false;
                }

                switch (window.Face)
                {
                    case CastleKeepWindowFace.Front:
                        if (window.LocalOrigin.y != -plan.KeepHalfZ)
                        {
                            error = $"front window {i} is detached from the front wall";
                            return false;
                        }
                        break;
                    case CastleKeepWindowFace.Rear:
                        if (window.LocalOrigin.y != plan.KeepHalfZ - FrontBackInset)
                        {
                            error = $"rear window {i} is detached from the rear wall";
                            return false;
                        }
                        break;
                    default:
                        error = $"window {i} has an unknown face";
                        return false;
                }
            }

            error = null;
            return true;
        }
    }
}
