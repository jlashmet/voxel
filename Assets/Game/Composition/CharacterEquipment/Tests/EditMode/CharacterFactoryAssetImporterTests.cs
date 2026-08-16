using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Tests
{
    public sealed class CharacterFactoryAssetImporterTests
    {
        private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        private const string ImporterTypeName =
            "MountingForce.Game.Composition.CharacterEquipment.Editor.CharacterFactoryAssetImporter, " +
            "Game.Composition.CharacterEquipment.Editor";
        private const string CharacterPrefabImporterTypeName =
            "MountingForce.Game.Composition.CharacterEquipment.Editor.CharacterFactoryCharacterPrefabImporter, " +
            "Game.Composition.CharacterEquipment.Editor";
        private const string TestRoot = "Assets/__CharacterFactoryPrefabTests";
        private const string SourcePrefabPath = TestRoot + "/SourceCharacter.prefab";
        private const string CataloguePath = TestRoot + "/CharacterPartCatalogue.asset";
        private const string GeneratedPrefabPath = TestRoot + "/madeline_test.prefab";

        private static Type ImporterType
        {
            get
            {
                Type type = Type.GetType(ImporterTypeName, throwOnError: false);
                Assert.That(type, Is.Not.Null, "Character Factory editor importer assembly did not load.");
                return type;
            }
        }

        private static Type CharacterPrefabImporterType
        {
            get
            {
                Type type = Type.GetType(CharacterPrefabImporterTypeName, throwOnError: false);
                Assert.That(
                    type,
                    Is.Not.Null,
                    "Character Factory character-prefab importer assembly did not load.");
                return type;
            }
        }

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "__CharacterFactoryPrefabTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void DescriptorRelativeFbx_ResolvesInsideGeneratedAssets()
        {
            MethodInfo method = RequireMethod("TryResolveSiblingAssetPath");
            object[] arguments =
            {
                "Assets/Generated/CharacterFactory/weapon/sun_staff/sun_staff.characterfactory.json",
                "sun_staff.fbx",
                null
            };

            bool resolved = (bool)method.Invoke(null, arguments);

            Assert.That(resolved, Is.True);
            Assert.That(
                arguments[2],
                Is.EqualTo("Assets/Generated/CharacterFactory/weapon/sun_staff/sun_staff.fbx"));
        }

        [Test]
        public void CataloguePath_AcceptsGeneratedAssetAndRejectsTraversal()
        {
            MethodInfo method = RequireMethod("TryNormalizeCataloguePath");

            object[] validArguments =
            {
                "Assets/Generated/CharacterFactory/CharacterPartCatalogue.asset",
                null
            };
            bool valid = (bool)method.Invoke(null, validArguments);
            Assert.That(valid, Is.True);
            Assert.That(
                validArguments[1],
                Is.EqualTo("Assets/Generated/CharacterFactory/CharacterPartCatalogue.asset"));

            object[] invalidArguments =
            {
                "Assets/Generated/CharacterFactory/../Escaped.asset",
                null
            };
            bool invalid = (bool)method.Invoke(null, invalidArguments);
            Assert.That(invalid, Is.False);
        }

        [Test]
        public void SocketTransformVector_PreservesGeneratedMetadata()
        {
            MethodInfo method = RequireMethod("ReadVector3");
            var expected = new Vector3(0.125f, -0.25f, 1.5f);

            var actual = (Vector3)method.Invoke(
                null,
                new object[]
                {
                    new[] { expected.x, expected.y, expected.z },
                    Vector3.zero
                });

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void SocketTransformVector_UsesFallbackForMalformedMetadata()
        {
            MethodInfo method = RequireMethod("ReadVector3");
            var fallback = new Vector3(1f, 1f, 1f);

            var actual = (Vector3)method.Invoke(
                null,
                new object[]
                {
                    new[] { 1f, 2f },
                    fallback
                });

            Assert.That(actual, Is.EqualTo(fallback));
        }

        [Test]
        public void CharacterPrefab_WiresCatalogueSkeletonAndDedicatedEquipmentRoot()
        {
            GameObject source = new GameObject("SourceCharacter");
            GameObject armature = new GameObject("Armature");
            armature.transform.SetParent(source.transform, false);
            PrefabUtility.SaveAsPrefabAsset(source, SourcePrefabPath);
            UnityEngine.Object.DestroyImmediate(source);

            CharacterPartCatalogue catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            AssetDatabase.CreateAsset(catalogue, CataloguePath);
            AssetDatabase.SaveAssets();

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            CharacterPartCatalogue catalogueAsset =
                AssetDatabase.LoadAssetAtPath<CharacterPartCatalogue>(CataloguePath);
            Assert.That(sourcePrefab, Is.Not.Null);
            Assert.That(catalogueAsset, Is.Not.Null);

            MethodInfo method = CharacterPrefabImporterType.GetMethod(
                "CreateOrUpdateCharacterPrefab",
                StaticNonPublic);
            Assert.That(method, Is.Not.Null, "Character prefab materializer method was not found.");
            method.Invoke(
                null,
                new object[]
                {
                    "madeline_test",
                    sourcePrefab,
                    catalogueAsset,
                    GeneratedPrefabPath
                });

            GameObject generated = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedPrefabPath);
            Assert.That(generated, Is.Not.Null);

            Transform model = generated.transform.Find("Model");
            Transform equipmentRoot = generated.transform.Find("Equipment");
            Assert.That(model, Is.Not.Null);
            Assert.That(equipmentRoot, Is.Not.Null);

            Transform generatedArmature = model.Find("Armature");
            Assert.That(generatedArmature, Is.Not.Null);

            ModularCharacterAssembler assembler =
                equipmentRoot.GetComponent<ModularCharacterAssembler>();
            CharacterEquipmentController controller =
                equipmentRoot.GetComponent<CharacterEquipmentController>();
            Assert.That(assembler, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(assembler.SkeletonRoot, Is.SameAs(generatedArmature));
            Assert.That(controller.Catalogue, Is.SameAs(catalogueAsset));
            Assert.That(controller.Assembler, Is.SameAs(assembler));
        }

        private static MethodInfo RequireMethod(string name)
        {
            MethodInfo method = ImporterType.GetMethod(name, StaticNonPublic);
            Assert.That(method, Is.Not.Null, $"Importer method '{name}' was not found.");
            return method;
        }
    }
}
