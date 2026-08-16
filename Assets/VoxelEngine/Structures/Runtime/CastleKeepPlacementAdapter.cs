using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    internal static class CastleKeepPlacementAdapter
    {
        internal const int LegacyKeepCentreZOffset = 60;
        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) => plan;
    }
}
