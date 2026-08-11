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
    /// Does the renderer-independent MountingForce worldgen package actually compile Kentridge into
    /// the same voxel storage the player walks through? Geometry is checked in the brickmap so a
    /// generation failure is distinguishable from a rendering failure.
    /// </summary>
    public sealed class ShowcaseFeatureTests
    {
        [UnityTest]
        public IEnumerator KentridgeChurchGeneratesAsHollowStructure()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(5f);

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            Assert.Greater(world.FeatureInstancesBuilt, 0, "Kentridge generated no feature instances");
            Assert.Greater(world.FeatureVoxelsBuilt, 10000, "Kentridge wrote implausibly few voxels");

            // The prototype church is a stable landmark at 1000dm,100dm. The worldgen package owns
            // that semantic plot; this integration test only reproduces the adapter's temporary
            // lowest-ground placement so it can inspect the generated brickmap.
            const int originX = 1000;
            const int originZ = 100;
            const int footprint = 164;
            int lowest = int.MaxValue;
            for (int z = 0; z <= footprint; z += 16)
            for (int x = 0; x <= footprint; x += 16)
            {
                int h = TerrainSampler.HeightAt(originX + x, originZ + z, world.Seed);
                if (h < lowest) lowest = h;
            }

            int baseY = lowest - 5;

            // Church grammar: local nave begins at (22,8,18), with 5-voxel walls. Probe the west
            // wall and then the carved nave beyond it at the same height.
            byte wall = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                new int3(originX + 24, baseY + 18, originZ + 22));
            byte interior = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                new int3(originX + 82, baseY + 18, originZ + 84));
            byte roof = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                new int3(originX + 82, baseY + 80, originZ + 84));

            Debug.Log($"### KENTRIDGE features={world.FeatureInstancesBuilt} " +
                      $"voxels={world.FeatureVoxelsBuilt} wall={wall} interior={interior} roof={roof}");

            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty, wall, "Kentridge church has no wall");
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, interior,
                "Kentridge church interior was not carved");
            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty, roof, "Kentridge church has no roof");
        }
    }
}
