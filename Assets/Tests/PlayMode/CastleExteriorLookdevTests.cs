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
            Assert.Zero(world.Pool.DirtyBricks.Length);
            Assert.Zero(world.RegionsNeedingUpload.Count);

            // Density invalidation includes neighbouring bricks, so the final voxel-upload frame
            // may leave a small coalesced GPU-only tail. Give that bounded queue several render
            // dispatches before judging pixels; this is still dramatically cheaper than building
            // the entire castle as the first-line functional test.
            for (int densityDrainFrame = 0; densityDrainFrame < 4; densityDrainFrame++)
            {
                camera.Render();
                yield return null;
            }
            camera.targetTexture = null;
            uploadTarget.Release();
            Object.DestroyImmediate(uploadTarget);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            Vector3 centre = new Vector3(plan.Centre.x, baseY, plan.Centre.z) * 0.1f;
            Vector3 waterfall = new Vector3(CastleBuilder.WaterfallStreamX(in plan),
                                            baseY - 48,
                                            CastleBuilder.WaterfallLipZ(in plan)) * 0.1f;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
            int trapZ = keepMinZ + plan.KeepHalfZ + 40;
            int dungeonY = baseY - 166;
            Vector3 cave = new Vector3(plan.Centre.x, dungeonY + 14, trapZ - 411) * 0.1f;
            Vector3 grotto = new Vector3(plan.Centre.x + 145, dungeonY + 14,
                                         trapZ - 386) * 0.1f;
            Vector3 farCamera = centre + new Vector3(-24f, 28f, -76f);
            Vector3 farTarget = centre + new Vector3(0f, 19f, 44f);
            Vector3 SurfacePoint(int x, int z, int aboveVoxels) =>
                new Vector3(x, world.OccupiedSurfaceHeight(x, z) + aboveVoxels, z) * 0.1f;
            Vector3 smoothTerrainCamera = SurfacePoint(446, -220, 22);
            Vector3 smoothTerrainTarget = SurfacePoint(330, 40, 8);
            float orbitScale = math.max(plan.BaileyHalfX, plan.BaileyHalfZ) / 175f;
            var views = new (string name, Vector3 position, Vector3 target)[]
            {
                ("approach", centre + new Vector3(0f, 11f, -52f * orbitScale),
                             centre + new Vector3(0f, 10f, 0f)),
                ("terrain_layers", centre + new Vector3(-42f, 21f, -72f),
                                   centre + new Vector3(4f, 3f, -15f)),
                ("smooth_terrain", smoothTerrainCamera, smoothTerrainTarget),
                ("smooth_ravine", waterfall + new Vector3(7f, 8f, -18f),
                                   waterfall + new Vector3(-1f, -3f, 2f)),
                ("waterfall_east", waterfall + new Vector3(18f, 12f, -18f),
                                   waterfall + new Vector3(0f, 0f, 0f)),
                ("waterfall_south", waterfall + new Vector3(3f, 11f, -24f),
                                    waterfall + new Vector3(0f, -1f, 0f)),
                ("waterfall_wide", centre + new Vector3(50f, 19f, -58f),
                                   centre + new Vector3(30f, -3f, -15f)),
                ("reference_hero", centre + new Vector3(-38f, 17f, -66f),
                                   centre + new Vector3(5f, 3f, -7f)),
                ("silhouette", centre + new Vector3(82f, 29f, -82f),
                               centre + new Vector3(0f, 11f, 0f)),
                ("wall", centre + new Vector3(-32f, 8f, -22f),
                         centre + new Vector3(-23f, 8f, -12f)),
                ("rear_annex", centre + new Vector3(28f, 13f, 36f),
                               centre + new Vector3(4f, 13f, 4f)),
                ("far_translation_a", farCamera, farTarget),
                ("far_translation_b", farCamera + Vector3.right * 6f,
                                      farTarget + Vector3.right * 6f),
                ("cave_entrance", cave + new Vector3(-1.8f, 0.1f, 8.5f),
                                  cave + new Vector3(2.7f, -0.5f, -7.0f)),
                ("cave_pool", cave + new Vector3(-5.5f, 0.3f, 2f),
                              cave + new Vector3(2.7f, -0.5f, -7.4f)),
                ("cave_grotto", grotto + new Vector3(-5f, 0.2f, -4f),
                                grotto + new Vector3(2f, -0.2f, 1f)),
            };

            string viewFilter = System.Environment.GetEnvironmentVariable("VOXEL_LOOKDEV_FILTER");
            for (int i = 0; i < views.Length; i++)
            {
                if (!string.IsNullOrEmpty(viewFilter)
                    && views[i].name.IndexOf(viewFilter,
                        System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
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
