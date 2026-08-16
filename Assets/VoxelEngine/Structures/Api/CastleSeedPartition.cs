namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Stable semantic random streams for castle planning. Domain values are explicit so adding
    /// another domain cannot renumber existing streams and perturb previously generated castles.
    /// </summary>
    public enum CastleSeedDomain : uint
    {
        Layout = 0x4C41594Fu,
        Walls = 0x57414C4Cu,
        Keep = 0x4B454550u,
        Rooms = 0x524F4F4Du,
        Dungeon = 0x44554E47u,
        Cave = 0x43415645u,
        Decor = 0x4445434Fu,
    }

    /// <summary>
    /// Derives independent deterministic seeds from a castle root seed without consuming a shared
    /// random-number stream. Returned seeds are always non-zero so they are safe for RNGs that
    /// reserve zero as an invalid state.
    /// </summary>
    public static class CastleSeedPartition
    {
        public static uint Derive(uint rootSeed, CastleSeedDomain domain) =>
            Mix(rootSeed ^ (uint)domain);

        public static uint Derive(uint rootSeed, CastleSeedDomain domain, uint elementId)
        {
            uint domainSeed = Derive(rootSeed, domain);
            uint elementSeed = Mix(elementId + 0xD1B54A35u);
            return Mix(domainSeed ^ elementSeed);
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                uint mixed = value + 0x9E3779B9u;
                mixed = (mixed ^ (mixed >> 16)) * 0x85EBCA6Bu;
                mixed = (mixed ^ (mixed >> 13)) * 0xC2B2AE35u;
                mixed ^= mixed >> 16;
                return mixed == 0u ? 0x6E624EB7u : mixed;
            }
        }
    }
}
