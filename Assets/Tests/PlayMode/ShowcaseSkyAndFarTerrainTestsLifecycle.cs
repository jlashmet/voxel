using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Lifecycle regressions share the ShowcaseSkyAndFarTerrainTests prefix deliberately so the
    /// existing isolated showcase CI shard runs them without broadening another test process.
    /// </summary>
    public sealed class ShowcaseSkyAndFarTerrainTestsLifecycle
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        [UnityTest, Timeout(900000)]
        public IEnumerator ReenablingShowcaseReplacesFarTerrainWithoutDuplicates()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            for (int frame = 0; frame < 8; frame++) yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);

            VoxelFarTerrain[] initial = Object.FindObjectsByType<VoxelFarTerrain>(
                FindObjectsSortMode.None);
            Assert.AreEqual(1, initial.Length,
                "A freshly loaded showcase must own exactly one far-terrain clipmap.");
            VoxelFarTerrain original = initial[0];
            FieldInfo materialField = typeof(VoxelFarTerrain).GetField(
                "m_Material", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(materialField);
            Material originalMaterial = materialField.GetValue(original) as Material;
            Assert.NotNull(originalMaterial,
                "Dynamically-created far terrain did not create its fallback material.");

            showcase.enabled = false;
            yield return null;

            VoxelFarTerrain[] disabled = Object.FindObjectsByType<VoxelFarTerrain>(
                FindObjectsSortMode.None);
            Assert.AreEqual(0, disabled.Length,
                "Disabling the showcase left its dynamically-created far terrain alive. That "
              + "clipmap retains native/mesh resources and a structure-store reference to the "
              + "world being disposed.");
            Assert.True(original == null,
                "The original far-terrain component survived the showcase lifecycle boundary.");
            Assert.True(originalMaterial == null,
                "Destroying the dynamically-created far terrain leaked its owned fallback Material.");

            showcase.enabled = true;
            yield return null;
            yield return null;

            VoxelFarTerrain[] reenabled = Object.FindObjectsByType<VoxelFarTerrain>(
                FindObjectsSortMode.None);
            Assert.AreEqual(1, reenabled.Length,
                "Re-enabling the showcase accumulated duplicate far-terrain clipmaps.");
            Assert.False(object.ReferenceEquals(original, reenabled[0]),
                "Re-enable did not create a fresh far-terrain owner for the fresh world.");
            Assert.NotNull(reenabled[0].Structures,
                "The replacement far terrain was not rebound to the fresh world's structures.");
        }
    }
}
