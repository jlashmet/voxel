using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlaceholderHumanoidMaterialTests
    {
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Models/Male_Adult_01.fbx")]
        [TestCase("Assets/ThirdParty/PlaceholderHumanoids/Models/Female_Adult_01.fbx")]
        public void PlaceholderBody_ActiveMeshesHaveAssignedMaterials(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, $"Unity could not load placeholder body {path}");

            var activeRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => IsActiveWithinAsset(renderer.transform, model.transform))
                .ToArray();
            Assert.That(activeRenderers.Length, Is.GreaterThan(0),
                $"{path} has no active skinned mesh renderer");

            foreach (var renderer in activeRenderers)
            {
                Assert.That(renderer.sharedMaterials.Length, Is.GreaterThan(0),
                    $"{path} renderer {renderer.name} has no material slots");
                Assert.That(renderer.sharedMaterials.All(material => material != null), Is.True,
                    $"{path} renderer {renderer.name} contains an unassigned material slot");
                Assert.That(renderer.sharedMaterials.All(material => material.shader != null), Is.True,
                    $"{path} renderer {renderer.name} contains a material with no shader");
            }
        }

        private static bool IsActiveWithinAsset(Transform transform, Transform root)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }

                if (current == root)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
