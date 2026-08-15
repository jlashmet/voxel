namespace VoxelEngine.Characters.Api
{
    /// <summary>
    /// Public character-equipment boundary.
    /// Consumers operate on stable part identifiers and never depend on Runtime asset types.
    /// </summary>
    public interface ICharacterEquipment
    {
        bool TryEquip(string partId, out CharacterEquipmentFailure failure);

        bool Unequip(CharacterEquipmentSlot slot);

        bool TryGetEquipped(CharacterEquipmentSlot slot, out string partId);
    }
}
