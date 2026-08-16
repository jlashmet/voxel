using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes preplanned keep apertures from the actual semantic world-space keep centre. Runtime
    /// owns masonry/glazing voxel work but never decides which windows exist or which bay is the
    /// entrance.
    /// </summary>
    internal static class CastleKeepWindowRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 worldKeepCentre,
            CastleKeepWindowSpec[] windows)
        {
            if (!CastleKeepWindowPlanner.TryValidate(in plan, windows, out string error))
            {
                throw new InvalidOperationException(
                    $"Castle keep window plan is invalid at realization: {error}.");
            }

            int baseY = plan.Centre.y + plan.PlateauHeight;
            for (int i = 0; i < windows.Length; i++)
            {
                CastleKeepWindowSpec window = windows[i];
                int x = worldKeepCentre.x + window.LocalOrigin.x;
                int z = worldKeepCentre.y + window.LocalOrigin.y;
                int y = baseY + window.BaseYOffset;

                brush.Arch(
                    new int3(x, y, z),
                    window.Width,
                    window.Height,
                    window.Depth,
                    2,
                    Mat.Empty);

                if (!window.HasLitGlazing)
                    continue;

                brush.Box(
                    new int3(x + 3, y + 4, z + 2),
                    new int3(10, window.Height - 10, 2),
                    Mat.LitWindow);
                brush.Box(
                    new int3(x + 7, y + 5, z + 1),
                    new int3(2, window.Height - 12, 3),
                    Mat.DarkStone);
                brush.Box(
                    new int3(x + 3, y + window.Height / 2, z + 1),
                    new int3(10, 2, 3),
                    Mat.DarkStone);
            }
        }
    }
}
