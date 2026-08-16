using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes planner-owned keep apertures. Runtime does not choose floor, bay, face, dimensions,
    /// glazing, or entrance omissions; it only projects each keep-local spec into world voxels.
    /// </summary>
    internal static class CastlePlannedKeepWindowRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan keepPlan,
            CastleKeepWindowSpec[] windows)
        {
            if (!CastleKeepWindowPlanner.TryValidate(in keepPlan, windows, out string error))
            {
                throw new InvalidOperationException(
                    $"Castle keep window plan is invalid at realization: {error}.");
            }

            int baseY = keepPlan.Centre.y + keepPlan.PlateauHeight;
            int keepCentreZ = keepPlan.Centre.z + CastleLayout.LegacyKeepCentreZOffset;

            for (int i = 0; i < windows.Length; i++)
            {
                CastleKeepWindowSpec window = windows[i];
                int x = keepPlan.Centre.x + window.LocalOrigin.x;
                int y = baseY + window.BaseYOffset;
                int z = keepCentreZ + window.LocalOrigin.y;

                brush.Arch(
                    new int3(x, y, z),
                    window.Width,
                    window.Height,
                    window.Depth,
                    2,
                    Mat.Empty);

                if (!window.HasLitGlazing)
                    continue;

                // Preserve the current authored leaded-window recipe while existence/placement is
                // entirely planner-owned. These are decorative realization details, not choices.
                brush.Box(
                    new int3(x + 3, y + 4, z + 2),
                    new int3(window.Width - 6, window.Height - 10, 2),
                    Mat.LitWindow);
                brush.Box(
                    new int3(x + window.Width / 2 - 1, y + 5, z + 1),
                    new int3(2, window.Height - 12, 3),
                    Mat.DarkStone);
                brush.Box(
                    new int3(x + 3, y + window.Height / 2, z + 1),
                    new int3(window.Width - 6, 2, 3),
                    Mat.DarkStone);
            }
        }
    }
}
