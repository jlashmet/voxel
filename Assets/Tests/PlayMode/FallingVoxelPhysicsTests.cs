using System.Collections;
using System.IO;
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
    public sealed class FallingVoxelPhysicsTests
    {
        [Test]
        public void SeveredComponentFallsAndSettlesBackIntoAuthoritativeGrid()
        {
            var world = new ShowcaseWorld(123u, 4096, 1, 2);
            try
            {
                // Indestructible ground, a two-voxel stump, one removed joint, and a three-voxel
                // detached top. The upper component has no path to the bedrock anchor.
                Set(world, new int3(20, 0, 20), ShowcaseWorld.MatBedrock);
                for (int y = 1; y <= 6; y++)
                    Set(world, new int3(20, y, 20), ShowcaseWorld.MatWood);

                int changed = world.RemoveAndResolveCollapse(new int3(20, 3, 20));

                Assert.GreaterOrEqual(changed, 4, "joint plus detached top should leave the grid");
                Assert.AreEqual(3, world.ActiveFallingVoxels);
                Assert.AreEqual(VoxelDimensions.MaterialEmpty, Get(world, new int3(20, 6, 20)));

                for (int i = 0; i < 20 && world.ActiveFallingClusters > 0; i++)
                    world.StepPhysics(0.05f);

                Assert.Zero(world.ActiveFallingClusters);
                Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, new int3(20, 3, 20)));
                Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, new int3(20, 4, 20)));
                Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, new int3(20, 5, 20)));
                Assert.AreEqual(VoxelDimensions.MaterialEmpty, Get(world, new int3(20, 6, 20)));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator TornadoTravelsBeforeImpactingKnownVoxelTarget()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            var origin = new Vector3(10f, 40f, 10f);
            int3 target = new int3(160, 400, 100); // six metres along +X, in otherwise open sky.
            Set(world, target, ShowcaseWorld.MatWood);

            showcase.LaunchTornado(origin, Vector3.right, 2);
            Assert.AreEqual(1, showcase.ActiveTornadoCount);
            Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, target),
                "launch itself must not apply an instant hitscan blast");

            var step = typeof(VoxelShowcase).GetMethod(
                "StepTornadoes", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < 10 && showcase.ActiveTornadoCount > 0; i++)
                step.Invoke(showcase, new object[] { 0.025f });

            Assert.Zero(showcase.ActiveTornadoCount, "projectile never reached its target");
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, Get(world, target),
                "impact did not destroy the target voxel");
        }

        [UnityTest]
        public IEnumerator CaptureTornadoInFlight()
        {
            const string output = "/tmp/physics_shots/tornado_in_flight.png";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var cam = Camera.main;
            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);
            var origin = new Vector3(10f, 90f, 10f);
            showcase.LaunchTornado(origin, Vector3.right, 2);

            var step = typeof(VoxelShowcase).GetMethod(
                "StepTornadoes", BindingFlags.NonPublic | BindingFlags.Instance);
            step.Invoke(showcase, new object[] { 0.12f });

            cam.transform.position = new Vector3(10.5f, 92.2f, 4.8f);
            cam.transform.LookAt(new Vector3(13.4f, 90f, 10f));
            cam.fieldOfView = 42f;
            yield return null;

            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            texture.Apply();
            File.WriteAllBytes(output, texture.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = oldTarget;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);

            Assert.True(File.Exists(output));
        }

        private static void Set(ShowcaseWorld world, int3 voxel, byte material) =>
            VoxelAccess.SetVoxel(ref world.Table, ref world.Pool, voxel, material);

        private static byte Get(ShowcaseWorld world, int3 voxel) =>
            VoxelAccess.GetVoxel(ref world.Table, in world.Pool, voxel);
    }
}
