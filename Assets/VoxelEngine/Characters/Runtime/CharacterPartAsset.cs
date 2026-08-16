using UnityEngine;
using VoxelEngine.Characters.Api;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Runtime-owned definition for one independently generated clothing, weapon, or accessory asset.
    /// Gameplay systems reference only the stable PartId through Characters.Api.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterPartAsset", menuName = "Voxel Engine/Characters/Character Part Asset")]
    public sealed class CharacterPartAsset : ScriptableObject
    {
        public enum MountMode : byte
        {
            SkinnedToCharacterSkeleton = 0,
            BoneSocket = 1
        }

        [SerializeField] private string partId = string.Empty;
        [SerializeField] private CharacterPartKind kind = CharacterPartKind.Clothing;
        [SerializeField] private CharacterEquipmentSlot slot;
        [SerializeField] private GameObject prefab;
        [SerializeField] private MountMode mountMode = MountMode.SkinnedToCharacterSkeleton;
        [SerializeField] private string socketBoneName = string.Empty;
        [SerializeField] private Vector3 socketLocalPosition;
        [SerializeField] private Vector3 socketLocalEulerAngles;
        [SerializeField] private Vector3 socketLocalScale = Vector3.one;

        public string PartId => partId;
        public CharacterPartKind Kind => kind;
        public CharacterEquipmentSlot Slot => slot;
        public GameObject Prefab => prefab;
        public MountMode Mode => mountMode;
        public string SocketBoneName => socketBoneName;
        public Vector3 SocketLocalPosition => socketLocalPosition;
        public Quaternion SocketLocalRotation => Quaternion.Euler(socketLocalEulerAngles);
        public Vector3 SocketLocalScale => socketLocalScale;
    }
}
