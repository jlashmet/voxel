using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Tests
{
    public sealed class CharacterFactoryCharacterPrefabCleanupTests
    {
        private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        private const string CleanupTypeName =
            "MountingForce.Game.Composition.CharacterEquipment.Editor.CharacterFactoryCharacterPrefabCleanup, " +
            "Game.Composition.CharacterEquipment.Editor";
        private const string GeneratedRoot = "Assets/Generated/CharacterFactory";
        private const string TestFolder = GeneratedRoot + "/__PrefabCleanupTests";
        private const string PrefabPath = TestFolder + "/madeline_body_01.prefab";
        private const string DescriptorPath = TestFolder + "/madeline_body_01.characterfactory.json";

        [SetUp]
        public void SetUp()
        {
            EnsureFolder(GeneratedRoot);
            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.CreateFolder(GeneratedRoot, "__PrefabCleanupTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void DeletedGeneratedDescriptor_RemovesSiblingRuntimePrefab()
        {
            GameObject root = new GameObject("madeline_body_01");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Is.Not.Null);

            Type cleanupType = Type.GetType(CleanupTypeName, throwOnError: false);
            Assert.That(cleanupType, Is.Not.Null, "Character Factory prefab cleanup assembly did not load.");
            MethodInfo method = cleanupType.GetMethod("RemoveStaleRuntimePrefabs", StaticNonPublic);
            Assert.That(method, Is.Not.Null, "Prefab cleanup method was not found.");

            method.Invoke(null, new object[] { new[] { DescriptorPath } });

            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                Is.Null,
                "removing a staged character descriptor must not leave a stale runtime prefab");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }
    }
}
