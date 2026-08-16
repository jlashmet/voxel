using System;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment
{
    public enum CharacterPartKind
    {
        Clothing,
        Weapon
    }

    public enum CharacterPartMountMode
    {
        RebindSkeleton,
        Socket
    }

    [Serializable]
    public sealed class CharacterPartDefinition
    {
        [SerializeField] private string partId = string.Empty;
        [SerializeField] private string slot = string.Empty;
        [SerializeField] private CharacterPartKind partKind;
        [SerializeField] private CharacterPartMountMode mountMode;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string socket = string.Empty;

        public string PartId => partId;
        public string Slot => slot;
        public CharacterPartKind PartKind => partKind;
        public CharacterPartMountMode MountMode => mountMode;
        public GameObject Prefab => prefab;
        public string Socket => socket;

        public CharacterPartDefinition(
            string slot,
            CharacterPartKind partKind,
            CharacterPartMountMode mountMode,
            GameObject prefab,
            string socket = "")
            : this(string.Empty, slot, partKind, mountMode, prefab, socket)
        {
        }

        public CharacterPartDefinition(
            string partId,
            string slot,
            CharacterPartKind partKind,
            CharacterPartMountMode mountMode,
            GameObject prefab,
            string socket = "")
        {
            this.partId = partId ?? string.Empty;
            this.slot = slot ?? string.Empty;
            this.partKind = partKind;
            this.mountMode = mountMode;
            this.prefab = prefab;
            this.socket = socket ?? string.Empty;
        }
    }
}
