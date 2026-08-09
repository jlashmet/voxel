using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Structures;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Fast visual iteration loop for exterior art direction. It deliberately omits interiors,
    /// cutaways, traversal, and gameplay checks; those remain in the full screenshot suite.
    /// </summary>
    public sealed class CastleExteriorLookdevTests
    {
        private const string OutputDirectory = "/tmp/castle_lookdev";

        [UnityTest]
        public IEnumerator CaptureExteriorLookdevViews()
        {
            Directory.CreateDirectory(OutputDirectory);
            var timer = Stopwatch.StartNew();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);
            var camera = Camera.main;
            Assert.NotNull(camera);

            float deadline = Time.realtimeSinceStartup + 30f;
            var uploadTarget = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32);
            camera.targetTexture = uploadTarget;
            while ((world.Pool.DirtyBricks.Length > 0 || world.RegionsNeedingUpload.Count > 0)
                   && Time.realtimeSinceStartup < deadline)
            {
                camera.Render();
                yield return null;
            }
            camera.targetTexture = null;
            uploadTarget.Release();
            Object.DestroyImmediate(uploadTarget);
            Assert.Zero(world.Pool.DirtyBricks.Length);
            Assert.Zero(world.RegionsNeedingUpload.Count);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            Vector3 centre = new Vector3(plan.Centre.x, baseY, plan.Centre.z) * 0.1f;
            float orbitScale = math.max(plan.BaileyHalfX, plan.BaileyHalfZ) / 175f;
            var views = new (string name, Vector3 position, Vector3 target)[]
            {
                ("approach", centre + new Vector3(0f, 11f, -52f * orbitScale),
                             centre + new Vector3(0f, 10f, 0f)),
                ("silhouette", centre + new Vector3(82f, 29f, -82f),
                               centre + new Vector3(0f, 11f, 0f)),
                ("wall", centre + new Vector3(-32f, 8f, -22f),
                         centre + new Vector3(-23f, 8f, -12f)),
                ("rear_annex", centre + new Vector3(28f, 13f, 36f),
                               centre + new Vector3(4f, 13f, 4f)),
            };

            for (int i = 0; i < views.Length; i++)
            {
                camera.transform.position = views[i].position;
                camera.transform.LookAt(views[i].target);
                yield return null;
                Capture(camera, Path.Combine(OutputDirectory, views[i].name + ".png"));
            }

            timer.Stop();
            UnityEngine.Debug.Log($"### CASTLE_LOOKDEV {timer.Elapsed.TotalSeconds:0.0}s " +
                                  $"voxels={world.CastleVoxels:N0}");
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            target.Release();
            Object.DestroyImmediate(target);
            Assert.True(File.Exists(path));
        }
    }
}
