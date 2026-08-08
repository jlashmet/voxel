using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Terrain;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Do the catalogue's cottages actually exist in the world the player walks into?
    ///
    /// Checked against the brickmap rather than the screen: a structure that generates but does
    /// not render is a different bug from one that never generated, and the two look identical
    /// from a screenshot.
    /// </summary>
    public sealed class ShowcaseFeatureTests
    {
        [UnityTest]
        public IEnumerator CottagesGenerateOnTheGround()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(4f);

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            Debug.Log($"### FEATURES built={world.FeatureInstancesBuilt} " +
                      $"voxels={world.FeatureVoxelsBuilt} ms={world.LastFeatureMs:0.0}");

            Assert.Greater(world.FeatureInstancesBuilt, 0, "no cottage was generated");
            Assert.Greater(world.FeatureVoxelsBuilt, 1000, "cottages generated but wrote almost nothing");

            // Wall material at the south face of the first cottage, just above its foundation.
            var origin = new int3(700, 0, 100);
            int lowest = int.MaxValue;
            for (var z = 0; z <= 96; z += 16)
            for (var x = 0; x <= 96; x += 16)
            {
                int h = TerrainSampler.HeightAt(origin.x + x, origin.z + z, world.Seed);
                if (h < lowest) lowest = h;
            }

            int baseY = lowest - 4;
            int wallY = baseY + 12;

            byte wall = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                                             new int3(origin.x + 2, wallY, origin.z + 2));
            byte interior = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                                                 new int3(origin.x + 32, wallY, origin.z + 32));

            Debug.Log($"### COTTAGE baseY={baseY} wallVoxel={wall} interiorVoxel={interior}");

            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty, wall, "the cottage has no wall");
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, interior,
                "the cottage interior was not carved — it is a solid block");

            // The roof must be above the walls and below the footprint ceiling.
            byte roof = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                                             new int3(origin.x + 32, baseY + 44, origin.z + 32));
            Debug.Log($"### ROOF voxel={roof}");
            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty, roof, "the cottage has no roof");
        }
    }
}
