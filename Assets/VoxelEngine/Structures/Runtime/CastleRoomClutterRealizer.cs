using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes preplanned per-floor room clutter. Runtime performs no random draws and makes no
    /// choice about count, wall side, position, radius, or height.
    /// </summary>
    internal static class CastleRoomClutterRealizer
    {
        internal static void BuildFloor(
            ref VoxelBrush brush,
            int2 worldKeepCentre,
            int floorBaseY,
            int floorIndex,
            CastleRoomClutterSpec[] clutter)
        {
            if (clutter == null)
                throw new ArgumentNullException(nameof(clutter));

            for (int i = 0; i < clutter.Length; i++)
            {
                CastleRoomClutterSpec item = clutter[i];
                if (item.FloorIndex != floorIndex)
                    continue;

                int x = worldKeepCentre.x + item.LocalCentre.x;
                int z = worldKeepCentre.y + item.LocalCentre.y;
                brush.Cylinder(x, floorBaseY + 3, z, item.Radius, item.Height, Mat.Wood);
                brush.Box(
                    new int3(x - item.Radius, floorBaseY + 7, z - item.Radius - 1),
                    new int3(item.Radius * 2, 2, item.Radius * 2 + 2),
                    Mat.Gold);
            }
        }
    }
}
