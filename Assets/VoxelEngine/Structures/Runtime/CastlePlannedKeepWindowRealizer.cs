using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes an already-planned keep aperture list. Window placement and facade choice belong to
    /// Structures.Api; Runtime only turns the supplied cardinal geometry into voxels.
    /// </summary>
    internal static class CastlePlannedKeepWindowRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan keepPlan,
            CastleKeepWindowSpec[] windows)
        {
            if (windows == null)
                throw new ArgumentNullException(nameof(windows));

            int baseY = keepPlan.Centre.y + keepPlan.PlateauHeight;
            int2 worldKeepCentre = new int2(
                keepPlan.Centre.x,
                keepPlan.Centre.z + CastleLayout.LegacyKeepCentreZOffset);

            for (int i = 0; i < windows.Length; i++)
            {
                CastleKeepWindowSpec window = windows[i];
                if (window.Id != i)
                {
                    throw new InvalidOperationException(
                        $"Castle keep window ids must be contiguous; expected {i}, found {window.Id}.");
                }

                BuildOne(ref brush, baseY, worldKeepCentre, in window);
            }
        }

        private static void BuildOne(
            ref VoxelBrush brush,
            int baseY,
            int2 worldKeepCentre,
            in CastleKeepWindowSpec window)
        {
            if (window.Width <= 0 || window.Height <= 0 || window.Depth <= 0 ||
                (window.DepthAxis != 0 && window.DepthAxis != 2))
            {
                throw new InvalidOperationException(
                    $"Castle keep window {window.Id} has invalid realization geometry.");
            }

            int3 origin = new int3(
                worldKeepCentre.x + window.LocalOrigin.x,
                baseY + window.BaseYOffset,
                worldKeepCentre.y + window.LocalOrigin.y);

            brush.Arch(
                origin,
                window.Width,
                window.Height,
                window.Depth,
                window.DepthAxis,
                Mat.Empty);

            if (!window.HasLitGlazing)
                return;

            BoxInWindow(
                ref brush,
                in origin,
                in window,
                tangentInset: 3,
                tangentSize: math.max(1, window.Width - 6),
                inwardInset: 2,
                inwardSize: math.min(2, window.Depth),
                yOffset: 4,
                height: math.max(1, window.Height - 10),
                Mat.LitWindow);

            BoxInWindow(
                ref brush,
                in origin,
                in window,
                tangentInset: math.max(0, window.Width / 2 - 1),
                tangentSize: math.min(2, window.Width),
                inwardInset: 1,
                inwardSize: math.min(3, window.Depth),
                yOffset: 5,
                height: math.max(1, window.Height - 12),
                Mat.DarkStone);

            BoxInWindow(
                ref brush,
                in origin,
                in window,
                tangentInset: 3,
                tangentSize: math.max(1, window.Width - 6),
                inwardInset: 1,
                inwardSize: math.min(3, window.Depth),
                yOffset: window.Height / 2,
                height: 2,
                Mat.DarkStone);
        }

        private static void BoxInWindow(
            ref VoxelBrush brush,
            in int3 apertureOrigin,
            in CastleKeepWindowSpec window,
            int tangentInset,
            int tangentSize,
            int inwardInset,
            int inwardSize,
            int yOffset,
            int height,
            byte material)
        {
            int normalStart = NormalStart(in window, inwardInset, inwardSize);

            if (window.DepthAxis == 2)
            {
                brush.Box(
                    new int3(
                        apertureOrigin.x + tangentInset,
                        apertureOrigin.y + yOffset,
                        apertureOrigin.z + normalStart),
                    new int3(tangentSize, height, inwardSize),
                    material);
            }
            else
            {
                brush.Box(
                    new int3(
                        apertureOrigin.x + normalStart,
                        apertureOrigin.y + yOffset,
                        apertureOrigin.z + tangentInset),
                    new int3(inwardSize, height, tangentSize),
                    material);
            }
        }

        private static int NormalStart(
            in CastleKeepWindowSpec window,
            int inwardInset,
            int inwardSize)
        {
            bool outwardAtMinimum =
                window.WallFace == CastleKeepFace.South ||
                window.WallFace == CastleKeepFace.West;

            if (outwardAtMinimum)
                return math.clamp(inwardInset, 0, math.max(0, window.Depth - inwardSize));

            return math.clamp(
                window.Depth - inwardInset - inwardSize,
                0,
                math.max(0, window.Depth - inwardSize));
        }
    }
}
