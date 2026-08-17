namespace Game.Structures.Api
{
    /// <summary>
    /// Broad visual/cultural family for generated decoration. The family occupies the high byte of
    /// DecorationContext.StyleId; the remaining 24 bits are free for deterministic local variation.
    /// </summary>
    public enum DecorationStyleFamily : byte
    {
        Unknown = 0,
        Rustic = 1,
        Courtly = 2,
        Martial = 3,
        Sacred = 4,
        Frontier = 5,
    }

    public static class DecorationStyleIds
    {
        private const uint VariationMask = 0x00FFFFFFu;

        public static uint Compose(DecorationStyleFamily family, uint variation)
        {
            uint familyBits = (uint)family << 24;
            return familyBits | (variation & VariationMask);
        }

        public static DecorationStyleFamily FamilyOf(uint styleId)
        {
            byte value = (byte)(styleId >> 24);
            return value <= (byte)DecorationStyleFamily.Frontier
                ? (DecorationStyleFamily)value
                : DecorationStyleFamily.Unknown;
        }

        public static uint VariationOf(uint styleId) => styleId & VariationMask;

        public static bool HasExplicitFamily(uint styleId) =>
            FamilyOf(styleId) != DecorationStyleFamily.Unknown;
    }
}
