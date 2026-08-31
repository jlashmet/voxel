using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldbuildingGalleryScriptBindingTests
    {
        private const string ScenePath = "Assets/Scenes/WorldbuildingGalleryShowcase.unity";
        private const string ProductionDriverMetaPath =
            "Assets/Scenes/Showcase/WorldbuildingGalleryShowcase.cs.meta";
        private const string LegacyBehaviourMetaPath =
            "Assets/Game/Composition/Showcase/WorldbuildingGalleryShowcaseBehaviour.cs.meta";

        [Test]
        public void WorldbuildingGallerySceneBindsProductionDriverWithDistinctScriptGuids()
        {
            string productionGuid = ReadGuid(ProductionDriverMetaPath);
            string legacyGuid = ReadGuid(LegacyBehaviourMetaPath);

            Assert.AreNotEqual(
                productionGuid,
                legacyGuid,
                "The production gallery driver and legacy gallery behaviour must have distinct Unity asset GUIDs.");

            string scene = File.ReadAllText(ProjectPath(ScenePath));
            StringAssert.Contains(
                $"m_Script: {{fileID: 11500000, guid: {productionGuid}, type: 3}}",
                scene,
                "WorldbuildingGalleryShowcase.unity must bind the production WorldbuildingGalleryShowcase driver.");
        }

        private static string ReadGuid(string relativeMetaPath)
        {
            string contents = File.ReadAllText(ProjectPath(relativeMetaPath));
            Match match = Regex.Match(contents, @"(?m)^guid:\s*([0-9a-f]{32})\s*$");
            Assert.True(match.Success, $"Missing Unity GUID in {relativeMetaPath}.");
            return match.Groups[1].Value;
        }

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
