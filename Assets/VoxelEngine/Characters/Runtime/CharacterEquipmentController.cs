using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Characters.Api;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Binds independently-authored parts to one canonical character skeleton at runtime.
    /// Skinned parts are rebound by canonical bone name; rigid parts are attached to a bone socket.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterEquipmentController : MonoBehaviour, ICharacterEquipment
    {
        [SerializeField] private Transform skeletonRoot;
        [SerializeField] private Transform equipmentRoot;
        [SerializeField] private WearableCatalogue catalogue;

        private readonly Dictionary<CharacterEquipmentSlot, GameObject> equippedInstances =
            new Dictionary<CharacterEquipmentSlot, GameObject>();

        private readonly Dictionary<CharacterEquipmentSlot, string> equippedIds =
            new Dictionary<CharacterEquipmentSlot, string>();

        private Dictionary<string, Transform> bonesByName;
        private HashSet<string> ambiguousBoneNames;

        public bool TryEquip(string partId, out CharacterEquipmentFailure failure)
        {
            if (string.IsNullOrWhiteSpace(partId))
            {
                failure = CharacterEquipmentFailure.PartIdRequired;
                return false;
            }

            if (catalogue == null)
            {
                failure = CharacterEquipmentFailure.CatalogueUnavailable;
                return false;
            }

            if (!catalogue.TryGet(partId, out WearableAsset asset))
            {
                failure = CharacterEquipmentFailure.PartNotFound;
                return false;
            }

            if (asset.Prefab == null)
            {
                failure = CharacterEquipmentFailure.PrefabMissing;
                return false;
            }

            EnsureRoots();
            if (skeletonRoot == null)
            {
                failure = CharacterEquipmentFailure.SkeletonUnavailable;
                return false;
            }

            if (equippedIds.TryGetValue(asset.Slot, out string currentId) &&
                string.Equals(currentId, partId, StringComparison.Ordinal))
            {
                failure = CharacterEquipmentFailure.None;
                return true;
            }

            GameObject candidate;
            if (asset.Mode == WearableAsset.MountMode.BoneSocket)
            {
                if (!TryCreateSocketPart(asset, out candidate, out failure))
                {
                    return false;
                }
            }
            else
            {
                candidate = Instantiate(asset.Prefab, equipmentRoot, false);
                candidate.name = asset.Prefab.name;

                if (!TryRebindSkinnedPart(candidate, out failure))
                {
                    DestroyInstance(candidate);
                    return false;
                }

                DisableEmbeddedAnimators(candidate);
            }

            if (equippedInstances.TryGetValue(asset.Slot, out GameObject previous))
            {
                DestroyInstance(previous);
            }

            equippedInstances[asset.Slot] = candidate;
            equippedIds[asset.Slot] = partId;
            failure = CharacterEquipmentFailure.None;
            return true;
        }

        public bool Unequip(CharacterEquipmentSlot slot)
        {
            if (!equippedInstances.TryGetValue(slot, out GameObject instance))
            {
                return false;
            }

            equippedInstances.Remove(slot);
            equippedIds.Remove(slot);
            DestroyInstance(instance);
            return true;
        }

        public bool TryGetEquipped(CharacterEquipmentSlot slot, out string partId)
        {
            return equippedIds.TryGetValue(slot, out partId);
        }

        /// <summary>
        /// Rebuilds the canonical-bone lookup after a character body/skeleton replacement.
        /// Existing equipment is not rebound automatically.
        /// </summary>
        public void InvalidateSkeletonBinding()
        {
            bonesByName = null;
            ambiguousBoneNames = null;
        }

        private void Awake()
        {
            EnsureRoots();
        }

        private void EnsureRoots()
        {
            if (skeletonRoot == null)
            {
                skeletonRoot = transform;
            }

            if (equipmentRoot == null)
            {
                equipmentRoot = transform;
            }
        }

        private bool TryCreateSocketPart(
            WearableAsset asset,
            out GameObject instance,
            out CharacterEquipmentFailure failure)
        {
            instance = null;
            if (string.IsNullOrWhiteSpace(asset.SocketBoneName))
            {
                failure = CharacterEquipmentFailure.SocketNotFound;
                return false;
            }

            if (!TryResolveBone(asset.SocketBoneName, out Transform socket, out failure))
            {
                if (failure == CharacterEquipmentFailure.BoneNotFound)
                {
                    failure = CharacterEquipmentFailure.SocketNotFound;
                }

                return false;
            }

            instance = Instantiate(asset.Prefab, socket, false);
            instance.name = asset.Prefab.name;
            Transform partTransform = instance.transform;
            partTransform.localPosition = asset.SocketLocalPosition;
            partTransform.localRotation = asset.SocketLocalRotation;
            partTransform.localScale = asset.SocketLocalScale;
            DisableEmbeddedAnimators(instance);
            failure = CharacterEquipmentFailure.None;
            return true;
        }

        private bool TryRebindSkinnedPart(GameObject instance, out CharacterEquipmentFailure failure)
        {
            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
                failure = CharacterEquipmentFailure.NoSkinnedMesh;
                return false;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                Transform[] sourceBones = renderer.bones;
                Transform[] targetBones = new Transform[sourceBones.Length];

                for (int boneIndex = 0; boneIndex < sourceBones.Length; boneIndex++)
                {
                    Transform sourceBone = sourceBones[boneIndex];
                    if (sourceBone == null)
                    {
                        failure = CharacterEquipmentFailure.BoneNotFound;
                        return false;
                    }

                    if (!TryResolveBone(sourceBone.name, out Transform targetBone, out failure))
                    {
                        return false;
                    }

                    targetBones[boneIndex] = targetBone;
                }

                Transform targetRoot = skeletonRoot;
                if (renderer.rootBone != null &&
                    !TryResolveBone(renderer.rootBone.name, out targetRoot, out failure))
                {
                    return false;
                }

                renderer.bones = targetBones;
                renderer.rootBone = targetRoot;
            }

            failure = CharacterEquipmentFailure.None;
            return true;
        }

        private bool TryResolveBone(
            string boneName,
            out Transform bone,
            out CharacterEquipmentFailure failure)
        {
            EnsureBoneIndex();

            if (ambiguousBoneNames.Contains(boneName))
            {
                bone = null;
                failure = CharacterEquipmentFailure.AmbiguousBoneName;
                return false;
            }

            if (!bonesByName.TryGetValue(boneName, out bone))
            {
                failure = CharacterEquipmentFailure.BoneNotFound;
                return false;
            }

            failure = CharacterEquipmentFailure.None;
            return true;
        }

        private void EnsureBoneIndex()
        {
            if (bonesByName != null)
            {
                return;
            }

            bonesByName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            ambiguousBoneNames = new HashSet<string>(StringComparer.Ordinal);

            Transform[] bones = skeletonRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bonesByName.ContainsKey(bone.name))
                {
                    ambiguousBoneNames.Add(bone.name);
                    continue;
                }

                bonesByName.Add(bone.name, bone);
            }
        }

        private static void DisableEmbeddedAnimators(GameObject instance)
        {
            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
            }
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
