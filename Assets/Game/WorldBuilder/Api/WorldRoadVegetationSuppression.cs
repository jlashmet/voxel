namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Deterministic ecology thinning for the shared 0..31 road influence. The regional ecology
    /// planner remains authoritative for candidates; roads only suppress candidates that already
    /// exist. For a stable candidate key, lowering suppression can only restore vegetation.
    /// </summary>
    public static class WorldRoadVegetationSuppression
    {
        public static bool ShouldSuppress(
            byte suppression31,
            uint worldSeed,
            int xdm,
            int zdm,
            int ordinal)
        {
            if (suppression31 == 0) return false;
            if (suppression31 >= 31) return true;

            unchecked
            {
                uint hash = worldSeed ^ 0x9E3779B9u;
                hash = (hash ^ (uint)xdm) * 16777619u;
                hash = (hash ^ (uint)zdm) * 16777619u;
                hash = (hash ^ (uint)ordinal) * 16777619u;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash % 31u < suppression31;
            }
        }
    }
}
