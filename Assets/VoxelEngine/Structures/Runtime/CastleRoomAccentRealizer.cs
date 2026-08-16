using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes planner-owned variable accents for one keep floor. This component contains no
    /// randomness: every placement and dimension comes from the frozen room-accent specs.
    /// </summary>
    internal static class CastleRoomAccentRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            int3 keepInteriorMin,
            int floorY,
            CastleRoomAccentSpec[] accents)
        {
            if (accents == null)
                return;

            for (int i = 0; i < accents.Length; i++)
            {
                CastleRoomAccentSpec accent = accents[i];
                int x = keepInteriorMin.x + accent.LocalX;
                int z = keepInteriorMin.z + accent.LocalZ;

                brush.Cylinder(
                    x,
                    floorY + 3,
                    z,
                    accent.Radius,
                    accent.Height,
                    Mat.Wood);
                brush.Box(
                    new int3(x - accent.Radius, floorY + 7, z - accent.Radius - 1),
                    new int3(accent.Radius * 2, 2, accent.Radius * 2 + 2),
                    Mat.Gold);
            }
        }
    }
}
