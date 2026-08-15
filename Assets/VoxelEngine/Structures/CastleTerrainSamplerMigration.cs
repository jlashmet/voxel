using System.Runtime.CompilerServices;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Structures
{
    // C# 9 has no global using aliases. Keep this internal and delete it when CastleBuilder moves
    // into Structures.Runtime and its source import is changed directly. This is not an exposed
    // compatibility API and does not restore VoxelEngine.Core.Terrain.TerrainSampler.
    internal static class TerrainSampler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int HeightAt(int worldX, int worldZ, uint seed) =>
            TerrainQuery.HeightAt(worldX, worldZ, seed);
    }
}
