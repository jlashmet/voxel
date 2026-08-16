using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Characters.Runtime;

namespace VoxelGame.Editor
{
    /// <summary>
    /// Adds the generic runtime animation seams to the derived Character Factory placeholder
    /// wrappers. The committed Rocketbox FBXs remain the durable source assets; generated
    /// wrapper prefabs can be deleted and recreated without manual animation setup.
    /// </summary>
    internal sealed class PlaceholderHumanoidAnimationConfigurator : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/PlaceholderHumanoids";
        private const string MalePrefab = Root + "/Models/placeholder_male.prefab";
        private const string FemalePrefab = Root + "/Models/placeholder_female.prefab";
        private const string AnimationRoot = Root + "/Animations/";

        private static readonly string[] WrapperPrefabs =
        {
            MalePrefab,
            FemalePrefab,
        };

        private static readonly string[] LocomotionNames =
        {
            "Idle",
            "Walk",
            "Run",
            "CrouchIdle",
        };

        private static readonly HashSet<string> PendingPrefabs =
            new HashSet<string>(StringComparer.Ordinal);

        private static bool processScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool locomotionChanged = false;
            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (IsWrapperPrefab(path))
                {
                    PendingPrefabs.Add(path);
                }

                if (IsLocomotionAsset(path))
                {
                    locomotionChanged = true;
                }
            }

            if (locomotionChanged)
            {
                QueueExistingWrappers();
            }

            ScheduleIfNeeded();
        }

        private static void QueueExistingWrappers()
        {
            for (int i = 0; i < WrapperPrefabs.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPrefabs[i]) != null)
                {
                    PendingPrefabs.Add(WrapperPrefabs[i]);
                }
            }
        }

        private static void ScheduleIfNeeded()
        {
            if (PendingPrefabs.Count == 0 || processScheduled)
            {
                return;
            }

            processScheduled = true;
            EditorApplication.delayCall += ProcessPendingPrefabs;
        }

        private static void ProcessPendingPrefabs()
        {
            processScheduled = false;
            if (PendingPrefabs.Count == 0)
            {
                return;
            }

            string[] paths = new string[PendingPrefabs.Count];
            PendingPrefabs.CopyTo(paths);
            PendingPrefabs.Clear();

            for (int i = 0; i < paths.Length; i++)
            {
                try
                {
                    ConfigurePrefab(paths[i]);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Placeholder humanoid animation setup failed for '{paths[i]}': " +
                        $"{exception.Message}\n{exception}"
                    );
                }
            }
        }

        private static void ConfigurePrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            AnimationClip idle = LoadClip("Idle");
            AnimationClip walk = LoadClip("Walk");
            AnimationClip run = LoadClip("Run");
            AnimationClip crouchIdle = LoadClip("CrouchIdle");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                bool changed = false;

                CharacterAnimationPlayer player = root.GetComponent<CharacterAnimationPlayer>();
                if (player == null)
                {
                    player = root.AddComponent<CharacterAnimationPlayer>();
                    changed = true;
                }

                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        $"Derived placeholder wrapper '{prefabPath}' contains no Animator."
                    );
                }

                SerializedObject playerSerialized = new SerializedObject(player);
                changed |= AssignObjectReference(playerSerialized, "animator", animator);
                if (changed)
                {
                    playerSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                CharacterAnimationPolicy policy = root.GetComponent<CharacterAnimationPolicy>();
                if (policy == null)
                {
                    policy = root.AddComponent<CharacterAnimationPolicy>();
                    changed = true;
                }

                SerializedObject policySerialized = new SerializedObject(policy);
                bool policyChanged = false;
                policyChanged |= AssignObjectReference(policySerialized, "player", player);
                policyChanged |= AssignObjectReference(policySerialized, "idleClip", idle);
                policyChanged |= AssignObjectReference(policySerialized, "walkClip", walk);
                policyChanged |= AssignObjectReference(policySerialized, "runClip", run);
                policyChanged |= AssignObjectReference(
                    policySerialized,
                    "crouchIdleClip",
                    crouchIdle
                );
                if (policyChanged)
                {
                    policySerialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (!changed)
                {
                    return;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save animation-ready placeholder wrapper '{prefabPath}'."
                    );
                }

                Debug.Log(
                    $"Placeholder humanoid wired animation runtime onto '{prefabPath}'."
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool AssignObjectReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized property '{propertyName}' was not found on " +
                    $"{serialized.targetObject.GetType().Name}."
                );
            }

            if (property.objectReferenceValue == value)
            {
                return false;
            }

            property.objectReferenceValue = value;
            return true;
        }

        private static AnimationClip LoadClip(string semanticName)
        {
            string path = AnimationRoot + semanticName + ".fbx";
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(clip.name, semanticName, StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            throw new InvalidOperationException(
                $"Placeholder locomotion clip '{semanticName}' is missing from '{path}'."
            );
        }

        private static bool IsWrapperPrefab(string path)
        {
            return string.Equals(path, MalePrefab, StringComparison.Ordinal) ||
                string.Equals(path, FemalePrefab, StringComparison.Ordinal);
        }

        private static bool IsLocomotionAsset(string path)
        {
            for (int i = 0; i < LocomotionNames.Length; i++)
            {
                if (string.Equals(
                    path,
                    AnimationRoot + LocomotionNames[i] + ".fbx",
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
