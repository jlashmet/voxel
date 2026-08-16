using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Tests
{
    public sealed class CharacterFactoryAssetImporterTests
    {
        private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        private const string ImporterTypeName =
            "MountingForce.Game.Composition.CharacterEquipment.Editor.CharacterFactoryAssetImporter, " +
            "Game.Composition.CharacterEquipment.Editor";

        private static Type ImporterType
        {
            get
            {
                Type type = Type.GetType(ImporterTypeName, throwOnError: false);
                Assert.That(type, Is.Not.Null, "Character Factory editor importer assembly did not load.");
                return type;
            }
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

        private static MethodInfo RequireMethod(string name)
        {
            MethodInfo method = ImporterType.GetMethod(name, StaticNonPublic);
            Assert.That(method, Is.Not.Null, $"Importer method '{name}' was not found.");
            return method;
        }
    }
}
