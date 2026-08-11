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
        public IEnumerator KentridgeTownLayoutAndChurchGenerate()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(5f);

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            Assert.Greater(world.FeatureInstancesBuilt, 0, "Kentridge generated no feature instances");
            Assert.Greater(world.FeatureVoxelsBuilt, 10000, "Kentridge wrote implausibly few voxels");

            const int originX = 834;
            const int originZ = 68;
            const int footprint = 164;
            const byte orientation = 3;

            int lowest = int.MaxValue;
            for (int z = 0; z <= footprint; z += 16)
            for (int x = 0; x <= footprint; x += 16)
            {
                int h = TerrainSampler.HeightAt(originX + x, originZ + z, world.Seed);
                if (h < lowest) lowest = h;
            }

            int baseY = lowest - 5;

            int3 wallLocal = RotateLocal(new int3(24, 18, 22), footprint, orientation);
            int3 interiorLocal = RotateLocal(new int3(82, 18, 84), footprint, orientation);
            int3 roofLocal = RotateLocal(new int3(82, 80, 84), footprint, orientation);

            byte wall = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                new int3(originX + wallLocal.x, baseY + wallLocal.y, originZ + wallLocal.z));
            byte interior = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                new int3(originX + interiorLocal.x, baseY + interiorLocal.y, originZ + interiorLocal.z));
            byte roof = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                new int3(originX + roofLocal.x, baseY + roofLocal.y, originZ + roofLocal.z));

            const int roadX = 1050;
            const int roadZ = 300;
            const int roadTileCentreZ = 312;
            int roadY = TerrainSampler.HeightAt(roadX, roadTileCentreZ, world.Seed);
            byte road = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                new int3(roadX, roadY, roadZ));

            Debug.Log($"### KENTRIDGE features={world.FeatureInstancesBuilt} " +
                      $"voxels={world.FeatureVoxelsBuilt} road={road} " +
                      $"wall={wall} interior={interior} roof={roof}");

            Assert.AreEqual(13, road, "Kentridge main road surface was not generated");
            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty, wall, "Kentridge church has no wall");
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, interior,
                "Kentridge church interior was not carved");
            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty, roof, "Kentridge church has no roof");
        }

        private static int3 RotateLocal(int3 p, int footprint, byte orientation)
        {
            int max = footprint - 1;

            return (orientation & 3) switch
            {
                1 => new int3(max - p.z, p.y, p.x),
                2 => new int3(max - p.x, p.y, max - p.z),
                3 => new int3(p.z, p.y, max - p.x),
                _ => p,
            };
        }
    }
}
