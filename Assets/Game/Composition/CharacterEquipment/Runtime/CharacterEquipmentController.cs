using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment
{
    public sealed class CharacterEquipmentController : MonoBehaviour
    {
        [SerializeField] private CharacterPartCatalogue catalogue;
        [SerializeField] private ModularCharacterAssembler assembler;

        public CharacterPartCatalogue Catalogue => catalogue;
        public ModularCharacterAssembler Assembler => assembler;

        private void Awake()
        {
            ResolveAssembler();
        }

        public void Configure(
            CharacterPartCatalogue targetCatalogue,
            ModularCharacterAssembler targetAssembler)
        {
            catalogue = targetCatalogue;
            assembler = targetAssembler;
        }

        public bool TryEquipById(string partId, out GameObject equippedInstance)
        {
            equippedInstance = null;
            if (catalogue == null ||
                !catalogue.TryGetPart(partId, out CharacterPartDefinition definition))
            {
                return false;
            }

            ModularCharacterAssembler resolvedAssembler = ResolveAssembler();
            return resolvedAssembler != null &&
                   resolvedAssembler.TryEquip(definition, out equippedInstance);
        }

        public bool TryUnequipSlot(string slot)
        {
            ModularCharacterAssembler resolvedAssembler = ResolveAssembler();
            return resolvedAssembler != null && resolvedAssembler.TryUnequip(slot);
        }

        public bool TryGetEquipped(string slot, out GameObject equippedInstance)
        {
            equippedInstance = null;
            ModularCharacterAssembler resolvedAssembler = ResolveAssembler();
            return resolvedAssembler != null &&
                   resolvedAssembler.TryGetEquipped(slot, out equippedInstance);
        }

        private ModularCharacterAssembler ResolveAssembler()
        {
            if (assembler == null)
            {
                assembler = GetComponent<ModularCharacterAssembler>();
            }

            return assembler;
        }
    }
}
