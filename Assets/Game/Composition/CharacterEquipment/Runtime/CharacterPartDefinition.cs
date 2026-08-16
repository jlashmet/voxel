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
        [SerializeField] private string slot = string.Empty;
        [SerializeField] private CharacterPartKind partKind;
        [SerializeField] private CharacterPartMountMode mountMode;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string socket = string.Empty;

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
        {
            this.slot = slot;
            this.partKind = partKind;
            this.mountMode = mountMode;
            this.prefab = prefab;
            this.socket = socket ?? string.Empty;
        }
    }
}
