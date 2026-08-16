namespace VoxelEngine.Characters.Api
{
    /// <summary>
    /// Stable runtime slots for independently-authored character parts.
    /// The character body is not a slot: it owns the canonical skeleton that parts bind to.
    /// </summary>
    public enum CharacterEquipmentSlot : byte
    {
        Hair = 0,
        Headwear = 1,
        Torso = 2,
        Legs = 3,
        Hands = 4,
        Feet = 5,
        Cape = 6,
        Accessory = 7,
        MainHand = 8,
        OffHand = 9
    }
}
