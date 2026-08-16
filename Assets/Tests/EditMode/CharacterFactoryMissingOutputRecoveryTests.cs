using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CharacterFactoryMissingOutputRecoveryTests
    {
        private const string MaleDescriptorPath =
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_male.characterfactory.json";
        private const string MalePrefabPath =
            "Assets/ThirdParty/PlaceholderHumanoids/Models/placeholder_male.prefab";

        [Test]
        public void MissingGeneratedPrefab_IsRecoveredFromCommittedDescriptor()
        {
            EnsureCharacterFactoryOutputExists();

            string prefabFullPath = Path.GetFullPath(MalePrefabPath);
            string metaFullPath = prefabFullPath + ".meta";
            Assert.That(File.Exists(prefabFullPath), Is.True,
                "Test precondition failed: Character Factory did not generate the placeholder prefab.");

            try
            {
                // Reproduce a branch switch/git clean with a retained Unity Library cache:
                // generated files disappear on disk without deleting the committed descriptor.
                File.Delete(prefabFullPath);
                File.Delete(metaFullPath);
                Assert.That(File.Exists(prefabFullPath), Is.False);

                InvokePrivateStatic(
                    "VoxelEngine.Characters.Editor.CharacterFactoryMissingOutputRecovery, VoxelEngine.Characters.Editor",
                    "RecoverMissingOutputs");
                InvokePrivateStatic(
                    "VoxelEngine.Characters.Editor.CharacterFactoryAssetImporter, VoxelEngine.Characters.Editor",
                    "ProcessPendingDescriptors");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                Assert.That(File.Exists(prefabFullPath), Is.True,
                    "Recovery did not recreate the missing Character Factory prefab.");
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(MalePrefabPath), Is.Not.Null,
                    "Recovered prefab was not loadable through AssetDatabase.");
            }
            finally
            {
                // Leave the project in the same valid generated state regardless of assertion outcome.
                if (!File.Exists(prefabFullPath))
                {
                    EnsureCharacterFactoryOutputExists();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        private static void EnsureCharacterFactoryOutputExists()
        {
            AssetDatabase.ImportAsset(
                MaleDescriptorPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            InvokePrivateStatic(
                "VoxelEngine.Characters.Editor.CharacterFactoryAssetImporter, VoxelEngine.Characters.Editor",
                "ProcessPendingDescriptors");
        }

        private static void InvokePrivateStatic(string assemblyQualifiedTypeName, string methodName)
        {
            Type type = Type.GetType(assemblyQualifiedTypeName);
            Assert.That(type, Is.Not.Null, $"Could not load editor type {assemblyQualifiedTypeName}.");

            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                $"Could not find private static method {assemblyQualifiedTypeName}.{methodName}.");

            method.Invoke(null, null);
        }
    }
}
