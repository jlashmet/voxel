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
        [SerializeField] private Vector3 socketLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 socketLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 socketLocalScale = Vector3.one;

        public string PartId => partId;
        public string Slot => slot;
        public CharacterPartKind PartKind => partKind;
        public CharacterPartMountMode MountMode => mountMode;
        public GameObject Prefab => prefab;
        public string Socket => socket;
        public Vector3 SocketLocalPosition => socketLocalPosition;
        public Vector3 SocketLocalEulerAngles => socketLocalEulerAngles;
        public Vector3 SocketLocalScale => socketLocalScale;

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
            : this(
                partId,
                slot,
                partKind,
                mountMode,
                prefab,
                socket,
                Vector3.zero,
                Vector3.zero,
                Vector3.one)
        {
        }

        public CharacterPartDefinition(
            string partId,
            string slot,
            CharacterPartKind partKind,
            CharacterPartMountMode mountMode,
            GameObject prefab,
            string socket,
            Vector3 socketLocalPosition,
            Vector3 socketLocalEulerAngles,
            Vector3 socketLocalScale)
        {
            this.partId = partId ?? string.Empty;
            this.slot = slot ?? string.Empty;
            this.partKind = partKind;
            this.mountMode = mountMode;
            this.prefab = prefab;
            this.socket = socket ?? string.Empty;
            this.socketLocalPosition = socketLocalPosition;
            this.socketLocalEulerAngles = socketLocalEulerAngles;
            this.socketLocalScale = socketLocalScale;
        }
    }
}
