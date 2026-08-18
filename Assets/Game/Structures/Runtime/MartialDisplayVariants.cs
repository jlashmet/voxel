namespace Game.Structures.Runtime
{
    public enum MartialDisplayKind : byte { Unknown = 0, Shield = 1, Weapons = 2, Armor = 3 }

    public static class MartialDisplayVariants
    {
        public static uint Create(MartialDisplayKind kind, uint payload) =>
            ((uint)kind << 30) | (payload & 0x3FFFFFFFu);
        public static MartialDisplayKind KindOf(uint variant) =>
            (MartialDisplayKind)(variant >> 30);
    }
}
