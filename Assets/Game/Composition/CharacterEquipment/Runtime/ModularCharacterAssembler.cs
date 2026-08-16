using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment
{
    public sealed class ModularCharacterAssembler : MonoBehaviour
    {
        [SerializeField] private Transform skeletonRoot;

        private readonly Dictionary<string, GameObject> equippedBySlot =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> skeletonByName =
            new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly HashSet<string> ambiguousSkeletonNames =
            new HashSet<string>(StringComparer.Ordinal);

        private Transform indexedSkeletonRoot;
        private bool skeletonIndexBuilt;

        public Transform SkeletonRoot
        {
            get => skeletonRoot;
            set
            {
                skeletonRoot = value;
                RebuildSkeletonIndex();
            }
        }

        private void Awake()
        {
            if (skeletonRoot == null)
            {
                skeletonRoot = transform;
            }

            RebuildSkeletonIndex();
        }

        public void RebuildSkeletonIndex()
        {
            skeletonByName.Clear();
            ambiguousSkeletonNames.Clear();

            if (skeletonRoot == null)
            {
                skeletonRoot = transform;
            }

            Transform[] transforms = skeletonRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                string boneName = current.name;

                if (ambiguousSkeletonNames.Contains(boneName))
                {
                    continue;
                }

                if (skeletonByName.ContainsKey(boneName))
                {
                    skeletonByName.Remove(boneName);
                    ambiguousSkeletonNames.Add(boneName);
                    continue;
                }

                skeletonByName.Add(boneName, current);
            }

            indexedSkeletonRoot = skeletonRoot;
            skeletonIndexBuilt = true;
        }

        public bool TryEquip(CharacterPartDefinition definition, out GameObject equippedInstance)
        {
            equippedInstance = null;
            if (!IsValidDefinition(definition))
            {
                return false;
            }

            if (definition.PartKind == CharacterPartKind.Clothing &&
                definition.MountMode != CharacterPartMountMode.RebindSkeleton)
            {
                return false;
            }

            if (definition.PartKind == CharacterPartKind.Weapon &&
                definition.MountMode != CharacterPartMountMode.Socket)
            {
                return false;
            }

            EnsureSkeletonIndex();

            GameObject candidate;
            switch (definition.MountMode)
            {
                case CharacterPartMountMode.Socket:
                    if (!TryResolveSkeletonTransform(definition.Socket, out Transform socket))
                    {
                        return false;
                    }

                    candidate = Instantiate(definition.Prefab, socket, false);
                    ApplySocketTransform(candidate.transform, definition);
                    DisableCandidateAnimators(candidate);
                    break;

                case CharacterPartMountMode.RebindSkeleton:
                    candidate = Instantiate(definition.Prefab, transform, false);
                    ResetLocalTransform(candidate.transform);

                    if (!TryPrepareRebind(candidate, out List<RendererRebindPlan> plans))
                    {
                        DestroyOwnedObject(candidate);
                        return false;
                    }

                    for (int i = 0; i < plans.Count; i++)
                    {
                        RendererRebindPlan plan = plans[i];
                        plan.Renderer.bones = plan.Bones;
                        plan.Renderer.rootBone = plan.RootBone;
                    }

                    DisableCandidateAnimators(candidate);
                    break;

                default:
                    return false;
            }

            if (equippedBySlot.TryGetValue(definition.Slot, out GameObject previous))
            {
                DestroyOwnedObject(previous);
            }

            equippedBySlot[definition.Slot] = candidate;
            equippedInstance = candidate;
            return true;
        }

        public bool TryUnequip(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot) || !equippedBySlot.TryGetValue(slot, out GameObject equipped))
            {
                return false;
            }

            equippedBySlot.Remove(slot);
            DestroyOwnedObject(equipped);
            return true;
        }

        public bool TryGetEquipped(string slot, out GameObject instance)
        {
            instance = null;
            if (string.IsNullOrWhiteSpace(slot) || !equippedBySlot.TryGetValue(slot, out GameObject equipped))
            {
                return false;
            }

            if (equipped == null)
            {
                equippedBySlot.Remove(slot);
                return false;
            }

            instance = equipped;
            return true;
        }

        private static bool IsValidDefinition(CharacterPartDefinition definition)
        {
            return definition != null &&
                   !string.IsNullOrWhiteSpace(definition.Slot) &&
                   definition.Prefab != null;
        }

        private void EnsureSkeletonIndex()
        {
            if (skeletonRoot == null)
            {
                skeletonRoot = transform;
            }

            if (!skeletonIndexBuilt || indexedSkeletonRoot != skeletonRoot)
            {
                RebuildSkeletonIndex();
            }
        }

        private bool TryResolveSkeletonTransform(string transformName, out Transform resolved)
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(transformName))
            {
                return false;
            }

            EnsureSkeletonIndex();
            if (ambiguousSkeletonNames.Contains(transformName))
            {
                return false;
            }

            return skeletonByName.TryGetValue(transformName, out resolved);
        }

        private bool TryPrepareRebind(GameObject candidate, out List<RendererRebindPlan> plans)
        {
            plans = new List<RendererRebindPlan>();
            SkinnedMeshRenderer[] renderers = candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
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
                    if (sourceBone == null || !TryResolveSkeletonTransform(sourceBone.name, out Transform targetBone))
                    {
                        return false;
                    }

                    targetBones[boneIndex] = targetBone;
                }

                Transform targetRootBone = null;
                if (renderer.rootBone != null &&
                    !TryResolveSkeletonTransform(renderer.rootBone.name, out targetRootBone))
                {
                    return false;
                }

                plans.Add(new RendererRebindPlan(renderer, targetBones, targetRootBone));
            }

            return true;
        }

        private static void DisableCandidateAnimators(GameObject candidate)
        {
            Animator[] animators = candidate.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
            }
        }

        private static void ApplySocketTransform(
            Transform target,
            CharacterPartDefinition definition)
        {
            target.localPosition = definition.SocketLocalPosition;
            target.localRotation = Quaternion.Euler(definition.SocketLocalEulerAngles);
            target.localScale = definition.SocketLocalScale;
        }

        private static void ResetLocalTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static void DestroyOwnedObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class RendererRebindPlan
        {
            public readonly SkinnedMeshRenderer Renderer;
            public readonly Transform[] Bones;
            public readonly Transform RootBone;

            public RendererRebindPlan(SkinnedMeshRenderer renderer, Transform[] bones, Transform rootBone)
            {
                Renderer = renderer;
                Bones = bones;
                RootBone = rootBone;
            }
        }
    }
}
