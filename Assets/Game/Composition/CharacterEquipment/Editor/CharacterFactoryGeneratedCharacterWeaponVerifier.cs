using System;
using System.IO;
using MountingForce.Game.Composition.CharacterEquipment;
using UnityEditor;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Editor
{
    public static class CharacterFactoryGeneratedCharacterWeaponVerifier
    {
        private const string DefaultAssetsRoot = "Assets/Generated/CharacterFactoryE2E";
        private const string DefaultCharacterId = "cf_e2e_hero_01";
        private const string DefaultWeaponId = "cf_e2e_weapon_01";
        private const string ExpectedSlot = "MainHand";
        private const string ExpectedSocket = "RightHand";

        public static void Verify()
        {
            string assetsRoot = ReadEnvironment("CHARACTER_FACTORY_E2E_ASSETS_ROOT", DefaultAssetsRoot)
                .Replace('\\', '/')
                .TrimEnd('/');
            string characterId = ReadEnvironment("CHARACTER_FACTORY_E2E_CHARACTER_ID", DefaultCharacterId);
            string weaponId = ReadEnvironment("CHARACTER_FACTORY_E2E_WEAPON_ID", DefaultWeaponId);
            string evidencePath = ReadEnvironment(
                "CHARACTER_FACTORY_E2E_EVIDENCE",
                Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    "Artifacts/CharacterFactorySmoke/e2e/unity-evidence.json")));

            if (!assetsRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "CHARACTER_FACTORY_E2E_ASSETS_ROOT must be an Assets/... path: " + assetsRoot);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!EditorApplication.ExecuteMenuItem(
                    "Mounting Force/Characters/Refresh Generated Part Catalogue"))
            {
                throw new InvalidOperationException("Could not execute generated part catalogue refresh.");
            }

            if (!EditorApplication.ExecuteMenuItem(
                    "Mounting Force/Characters/Refresh Generated Character Prefabs"))
            {
                throw new InvalidOperationException("Could not execute generated character prefab refresh.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string cataloguePath = assetsRoot + "/CharacterPartCatalogue.asset";
            string characterPrefabPath =
                $"{assetsRoot}/character/{characterId}/{characterId}.prefab";
            string weaponFbxPath =
                $"{assetsRoot}/weapon/{weaponId}/{weaponId}.fbx";

            CharacterPartCatalogue catalogue =
                AssetDatabase.LoadAssetAtPath<CharacterPartCatalogue>(cataloguePath);
            if (catalogue == null)
            {
                throw new InvalidOperationException(
                    "Generated Character Factory catalogue was not imported: " + cataloguePath);
            }

            if (!catalogue.TryGetPart(weaponId, out CharacterPartDefinition weaponDefinition))
            {
                throw new InvalidOperationException(
                    $"Generated weapon '{weaponId}' was not added to catalogue '{cataloguePath}'.");
            }

            if (!string.Equals(weaponDefinition.Slot, ExpectedSlot, StringComparison.Ordinal) ||
                !string.Equals(weaponDefinition.Socket, ExpectedSocket, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Generated weapon runtime metadata is wrong: slot={weaponDefinition.Slot}, " +
                    $"socket={weaponDefinition.Socket}.");
            }

            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(characterPrefabPath);
            if (characterPrefab == null)
            {
                throw new InvalidOperationException(
                    "Generated character prefab was not imported: " + characterPrefabPath);
            }

            GameObject weaponModel = AssetDatabase.LoadAssetAtPath<GameObject>(weaponFbxPath);
            if (weaponModel == null)
            {
                throw new InvalidOperationException(
                    "Generated weapon FBX was not imported: " + weaponFbxPath);
            }

            GameObject characterInstance = PrefabUtility.InstantiatePrefab(characterPrefab) as GameObject;
            if (characterInstance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate generated character prefab: " + characterPrefabPath);
            }

            try
            {
                CharacterEquipmentController controller =
                    characterInstance.GetComponentInChildren<CharacterEquipmentController>(true);
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        "Generated character prefab has no CharacterEquipmentController.");
                }

                ModularCharacterAssembler assembler = controller.Assembler;
                if (assembler == null || assembler.SkeletonRoot == null)
                {
                    throw new InvalidOperationException(
                        "Generated character prefab has no configured modular skeleton assembler.");
                }

                Transform rightHand = FindUnique(assembler.SkeletonRoot, ExpectedSocket);
                if (rightHand == null)
                {
                    throw new InvalidOperationException(
                        "Generated character skeleton does not contain a unique RightHand socket.");
                }

                SkinnedMeshRenderer[] characterRenderers =
                    characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (characterRenderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Generated character prefab contains no skinned mesh renderer.");
                }

                if (!controller.TryEquipById(weaponId, out GameObject equippedWeapon) ||
                    equippedWeapon == null)
                {
                    throw new InvalidOperationException(
                        $"TryEquipById failed for generated weapon '{weaponId}'.");
                }

                if (equippedWeapon.transform.parent != rightHand)
                {
                    throw new InvalidOperationException(
                        $"Generated weapon was not attached to RightHand; actual parent=" +
                        (equippedWeapon.transform.parent != null
                            ? equippedWeapon.transform.parent.name
                            : "<null>"));
                }

                if (!controller.TryGetEquipped(ExpectedSlot, out GameObject equippedBySlot) ||
                    equippedBySlot != equippedWeapon)
                {
                    throw new InvalidOperationException(
                        "Generated weapon was not registered in the MainHand equipment slot.");
                }

                Renderer[] weaponRenderers = equippedWeapon.GetComponentsInChildren<Renderer>(true);
                if (weaponRenderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Equipped generated weapon contains no renderer after Unity import.");
                }

                var evidence = new Evidence
                {
                    characterId = characterId,
                    weaponId = weaponId,
                    characterPrefab = characterPrefabPath,
                    weaponFbx = weaponFbxPath,
                    catalogue = cataloguePath,
                    slot = ExpectedSlot,
                    socket = rightHand.name,
                    characterSkinnedRendererCount = characterRenderers.Length,
                    equippedWeaponRendererCount = weaponRenderers.Length,
                    equippedWeaponParent = equippedWeapon.transform.parent.name,
                };

                string directory = Path.GetDirectoryName(evidencePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true) + Environment.NewLine);

                Debug.Log(
                    $"CHARACTER_FACTORY_CHARACTER_WEAPON_E2E_OK character={characterId} " +
                    $"weapon={weaponId} slot={ExpectedSlot} socket={rightHand.name} " +
                    $"evidence={evidencePath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(characterInstance);
            }
        }

        private static string ReadEnvironment(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static Transform FindUnique(Transform root, string name)
        {
            Transform found = null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (!string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found != null)
                {
                    return null;
                }

                found = candidate;
            }

            return found;
        }

        [Serializable]
        private sealed class Evidence
        {
            public string characterId;
            public string weaponId;
            public string characterPrefab;
            public string weaponFbx;
            public string catalogue;
            public string slot;
            public string socket;
            public int characterSkinnedRendererCount;
            public int equippedWeaponRendererCount;
            public string equippedWeaponParent;
        }
    }
}
