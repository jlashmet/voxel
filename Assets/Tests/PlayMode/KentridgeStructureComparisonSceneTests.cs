using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeStructureComparisonSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SceneBuildsOriginalAndModifiedThroughProductionRenderer()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/KentridgeStructureComparison.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("KentridgeStructureComparison", LoadSceneMode.Single);
#endif
            yield return null;

            KentridgeStructureComparisonShowcase showcase =
                Object.FindAnyObjectByType<KentridgeStructureComparisonShowcase>();
            Assert.NotNull(showcase);
            Assert.That(showcase.IsBuilt, Is.True);
            Assert.That(showcase.RoleCount, Is.EqualTo(17));
            Assert.That(showcase.SelectedRole, Is.EqualTo(1));
            Assert.That(showcase.SelectedRoleName, Is.Not.Empty);
            Assert.That(VoxelRenderBridge.TryGetWorld(out var world), Is.True);
            Assert.NotNull(world.Storage);
        }
    }
}
