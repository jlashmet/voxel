using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Core.Storage;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseSpawnTests
    {
        [UnityTest]
        public IEnumerator PlayerStartsOnClearGroundSouthOfCastle()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            Vector3 eye = showcase.transform.position;
            Vector3 feet = eye - Vector3.up * 1.65f;
            const float voxelSize = VoxelSurfaceRenderer.VoxelSize;

            Assert.Less(eye.z, 0f, "Spawn must remain on the open southern approach.");
            Assert.Greater(eye.x, ShowcaseWorld.RegionMetres * 0.5f + 8f,
                "Spawn should be offset east for an oblique castle reveal.");

            int minX = Mathf.FloorToInt((feet.x - 0.3f) / voxelSize);
            int maxX = Mathf.FloorToInt((feet.x + 0.3f - 1e-4f) / voxelSize);
            int minY = Mathf.FloorToInt(feet.y / voxelSize);
            int maxY = Mathf.FloorToInt((feet.y + 1.8f - 1e-4f) / voxelSize);
            int minZ = Mathf.FloorToInt((feet.z - 0.3f) / voxelSize);
            int maxZ = Mathf.FloorToInt((feet.z + 0.3f - 1e-4f) / voxelSize);

            for (int z = minZ; z <= maxZ; z++)
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                byte material = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                                                     new int3(x, y, z));
                Assert.AreEqual(VoxelDimensions.MaterialEmpty, material,
                    $"Player spawn overlaps occupied voxel ({x}, {y}, {z}).");
            }

            int centreX = Mathf.FloorToInt(feet.x / voxelSize);
            int centreZ = Mathf.FloorToInt(feet.z / voxelSize);
            int surface = world.OccupiedSurfaceHeight(centreX, centreZ);
            int footprintSurface = int.MinValue;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
                footprintSurface = Mathf.Max(footprintSurface,
                    world.OccupiedSurfaceHeight(x, z));
            Assert.That(minY - footprintSurface, Is.InRange(1, 2),
                "Player feet should start no more than one clearance voxel above the highest " +
                "surface under the capsule footprint.");

            byte ground = VoxelAccess.GetVoxel(ref world.Table, in world.Pool,
                                               new int3(centreX, surface, centreZ));
            Assert.AreNotEqual(VoxelDimensions.MaterialEmpty, ground,
                "Player spawn has no occupied ground directly beneath it.");

            Vector3 castleTarget = new(ShowcaseWorld.RegionMetres * 0.5f,
                                       eye.y + 5f,
                                       (ShowcaseWorld.RegionVoxelEdge / 2 + 120) * voxelSize);
            Vector3 expectedView = (castleTarget - eye).normalized;
            Assert.Greater(Vector3.Dot(showcase.transform.forward, expectedView), 0.995f,
                "Initial camera must frame the castle from the oblique spawn.");
        }
    }
}
