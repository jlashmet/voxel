using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CharacterFactoryMissingOutputRecoveryTests
    {
        private const string ModelsDirectory =
            "Assets/ThirdParty/PlaceholderHumanoids/Models";

        [Test]
        public void MissingGeneratedPrefab_IsDetectedWithoutMutatingProjectAssets()
        {
            string probeId = "__character_factory_recovery_probe_" + Guid.NewGuid().ToString("N");
            string descriptorAssetPath =
                ModelsDirectory + "/" + probeId + ".characterfactory.json";
            string descriptorFile = WriteTemporaryDescriptor(
                schemaVersion: 1,
                id: probeId,
                assetType: "character",
                fbx: "Male_Adult_01.fbx",
                catalogueAsset: string.Empty);

            try
            {
                Assert.That(
                    InvokeNeedsRecovery(descriptorAssetPath, descriptorFile),
                    Is.True,
                    "A valid descriptor with an imported source FBX and a missing derived prefab should be recovered.");
            }
            finally
            {
                File.Delete(descriptorFile);
            }
        }

        [Test]
        public void MissingSourceModel_DoesNotScheduleRecovery()
        {
            string probeId = "__character_factory_missing_source_" + Guid.NewGuid().ToString("N");
            string descriptorAssetPath =
                ModelsDirectory + "/" + probeId + ".characterfactory.json";
            string descriptorFile = WriteTemporaryDescriptor(
                schemaVersion: 1,
                id: probeId,
                assetType: "character",
                fbx: "__definitely_missing_character_factory_source__.fbx",
                catalogueAsset: string.Empty);

            try
            {
                Assert.That(
                    InvokeNeedsRecovery(descriptorAssetPath, descriptorFile),
                    Is.False,
                    "Recovery must wait until the source FBX is present in AssetDatabase.");
            }
            finally
            {
                File.Delete(descriptorFile);
            }
        }

        [Test]
        public void UnsupportedDescriptorSchema_DoesNotScheduleRecovery()
        {
            string probeId = "__character_factory_bad_schema_" + Guid.NewGuid().ToString("N");
            string descriptorAssetPath =
                ModelsDirectory + "/" + probeId + ".characterfactory.json";
            string descriptorFile = WriteTemporaryDescriptor(
                schemaVersion: 999,
                id: probeId,
                assetType: "character",
                fbx: "Male_Adult_01.fbx",
                catalogueAsset: string.Empty);

            try
            {
                Assert.That(
                    InvokeNeedsRecovery(descriptorAssetPath, descriptorFile),
                    Is.False,
                    "Recovery should ignore descriptors that the Character Factory importer cannot consume.");
            }
            finally
            {
                File.Delete(descriptorFile);
            }
        }

        private static string WriteTemporaryDescriptor(
            int schemaVersion,
            string id,
            string assetType,
            string fbx,
            string catalogueAsset)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "voxel-character-factory-recovery-" + Guid.NewGuid().ToString("N") + ".json");
            string json =
                "{\n" +
                $"  \"schemaVersion\": {schemaVersion},\n" +
                $"  \"id\": \"{id}\",\n" +
                $"  \"assetType\": \"{assetType}\",\n" +
                $"  \"fbx\": \"{fbx}\",\n" +
                $"  \"catalogueAsset\": \"{catalogueAsset}\"\n" +
                "}\n";
            File.WriteAllText(path, json);
            return path;
        }

        private static bool InvokeNeedsRecovery(string descriptorAssetPath, string descriptorFile)
        {
            const string TypeName =
                "VoxelEngine.Characters.Editor.CharacterFactoryMissingOutputRecovery, VoxelEngine.Characters.Editor";
            Type type = Type.GetType(TypeName);
            Assert.That(type, Is.Not.Null, $"Could not load editor type {TypeName}.");

            MethodInfo method = type.GetMethod(
                "NeedsRecovery",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not find {TypeName}.NeedsRecovery.");

            object result = method.Invoke(null, new object[] { descriptorAssetPath, descriptorFile });
            Assert.That(result, Is.TypeOf<bool>());
            return (bool)result;
        }
    }
}
