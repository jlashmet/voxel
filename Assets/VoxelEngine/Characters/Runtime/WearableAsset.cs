using UnityEngine;
using VoxelEngine.Characters.Api;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Runtime-owned authoring asset for a separately generated clothing, hair, cape, or equipment mesh.
    /// Gameplay systems reference only the stable PartId through Characters.Api.
    /// </summary>
    [CreateAssetMenu(fileName = "WearableAsset", menuName = "Voxel Engine/Characters/Wearable Asset")]
    public sealed class WearableAsset : ScriptableObject
    {
        public enum MountMode : byte
        {
            SkinnedToCharacterSkeleton = 0,
            BoneSocket = 1
        }

        [SerializeField] private string partId = string.Empty;
        [SerializeField] private CharacterEquipmentSlot slot;
        [SerializeField] private GameObject prefab;
        [SerializeField] private MountMode mountMode = MountMode.SkinnedToCharacterSkeleton;
        [SerializeField] private string socketBoneName = string.Empty;
        [SerializeField] private Vector3 socketLocalPosition;
        [SerializeField] private Vector3 socketLocalEulerAngles;
        [SerializeField] private Vector3 socketLocalScale = Vector3.one;

        public string PartId => partId;
        public CharacterEquipmentSlot Slot => slot;
        public GameObject Prefab => prefab;
        public MountMode Mode => mountMode;
        public string SocketBoneName => socketBoneName;
        public Vector3 SocketLocalPosition => socketLocalPosition;
        public Quaternion SocketLocalRotation => Quaternion.Euler(socketLocalEulerAngles);
        public Vector3 SocketLocalScale => socketLocalScale;
    }
}
