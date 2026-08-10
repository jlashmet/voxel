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
using VoxelEngine.Rendering;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Fast visual iteration loop for exterior art direction. It deliberately omits interiors,
    /// cutaways, traversal, and gameplay checks; those remain in the full screenshot suite.
    /// </summary>
    public sealed class CastleExteriorLookdevTests
    {
        private const string OutputDirectory = "/tmp/castle_lookdev";
        private const string MotionDirectory = "/tmp/castle_lookdev/motion";

        private ShowcaseWorld _world;
        private Camera _camera;

        /// <summary>
        /// Loads the showcase, drains every pending voxel and density upload, and puts the
        /// camera under test control. Shared so the moving-camera sequence starts from exactly
        /// the same world state the fixed views do.
        /// </summary>
        private IEnumerator PrepareShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            _world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);
            _camera = Camera.main;
            Assert.NotNull(_camera);

            // Generous because the first render after a shader edit blocks on compilation, which
            // can take minutes and has nothing to do with upload progress. A tight deadline here
            // failed the whole suite every time a shader changed.
            float deadline = Time.realtimeSinceStartup + 240f;
            var uploadTarget = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = uploadTarget;
            while ((_world.Pool.DirtyBricks.Length > 0 || _world.RegionsNeedingUpload.Count > 0)
                   && Time.realtimeSinceStartup < deadline)
            {
                _camera.Render();
                yield return null;
            }
            Assert.Zero(_world.Pool.DirtyBricks.Length);
            Assert.Zero(_world.RegionsNeedingUpload.Count);

            // Density invalidation includes neighbouring bricks, so the final voxel-upload frame
            // may leave a small coalesced GPU-only tail. Give that bounded queue several render
            // dispatches before judging pixels; this is still dramatically cheaper than building
            // the entire castle as the first-line functional test.
            for (int densityDrainFrame = 0; densityDrainFrame < 4; densityDrainFrame++)
            {
                _camera.Render();
                yield return null;
            }
            _camera.targetTexture = null;
            uploadTarget.Release();
            Object.DestroyImmediate(uploadTarget);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);
        }

        /// <summary>
        /// Flies the camera along a continuous path, rendering every frame and capturing at a
        /// fixed stride.
        ///
        /// The fixed views cannot see any of this. They teleport, render once, and judge the
        /// result, which hides everything the incremental surface cache does over time: chunks
        /// arriving behind the camera, the coarse level retiring before the fine level has filled
        /// in, geometry from the previous viewpoint still resident. Those are the artefacts that
        /// only appear in motion, and they need a moving camera to reproduce.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator CaptureMovingCameraSequence()
        {
            Directory.CreateDirectory(MotionDirectory);
            IEnumerator setup = PrepareShowcase();
            while (setup.MoveNext()) yield return setup.Current;

            Vector3 centre = CastleCentre(_world);
            // A long approach that also strafes, so both radial and lateral streaming are
            // exercised: distance drives LOD selection, lateral motion drives residency churn.
            Vector3 start = centre + new Vector3(-58f, 20f, -104f);
            Vector3 end = centre + new Vector3(34f, 12f, -30f);
            Vector3 lookAt = centre + new Vector3(0f, 6f, 0f);

            const int frames = 96;
            const int captureStride = 8;
            int captureIndex = 0;
            var motionTarget = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            try
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    float t = frame / (float)(frames - 1);
                    _camera.transform.position = Vector3.Lerp(start, end, t);
                    _camera.transform.LookAt(lookAt);

                    // Render every frame, never only the captured ones. The cache advances by a
                    // bounded number of builds per frame, so skipping frames would hand it a
                    // budget no player ever gives it.
                    _camera.targetTexture = motionTarget;
                    _camera.Render();
                    _camera.targetTexture = null;
                    yield return null;

                    if (frame % captureStride != 0) continue;
                    Capture(_camera, Path.Combine(MotionDirectory,
                        $"motion_{captureIndex:D2}.png"));
                    captureIndex++;
                }
            }
            finally
            {
                motionTarget.Release();
                Object.DestroyImmediate(motionTarget);
            }

            Assert.Greater(captureIndex, 4, "The motion sequence needs enough frames to compare.");
        }

        private static Vector3 CastleCentre(ShowcaseWorld world)
        {
            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            return new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                               plan.Centre.z) * 0.1f;
        }

        // Generating the castle and capturing every view exceeds the 180 s default. Set
        // VOXEL_LOOKDEV_FILTER to capture a single view when iterating on look.
        [UnityTest, Timeout(900000)]
        public IEnumerator CaptureExteriorLookdevViews()
        {
            Directory.CreateDirectory(OutputDirectory);
            var timer = Stopwatch.StartNew();
            IEnumerator prepare = PrepareShowcase();
            while (prepare.MoveNext()) yield return prepare.Current;
            ShowcaseWorld world = _world;
            Camera camera = _camera;

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
            VoxelRenderBridge.SurfaceDebugTint = string.IsNullOrEmpty(
                System.Environment.GetEnvironmentVariable("VOXEL_SURFACE_DEBUG"))
                ? Color.white : new Color(0.05f, 1f, 0.9f, 1f);
            int warmupFrames = 1;
            string warmupValue = System.Environment.GetEnvironmentVariable("VOXEL_LOOKDEV_WARMUP_FRAMES");
            if (!string.IsNullOrEmpty(warmupValue) && int.TryParse(warmupValue, out int parsedWarmup))
                warmupFrames = Mathf.Clamp(parsedWarmup, 1, 120);
            for (int i = 0; i < views.Length; i++)
            {
                if (!string.IsNullOrEmpty(viewFilter)
                    && views[i].name.IndexOf(viewFilter,
                        System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                camera.transform.position = views[i].position;
                camera.transform.LookAt(views[i].target);
                // A teleported lookdev camera has none of the approach time a player supplies.
                // Optional tiny-target warmup renders let the incremental GPU surface cache
                // converge around that viewpoint before the expensive full-resolution capture.
                var warmupTarget = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = warmupTarget;
                for (int warmup = 0; warmup < warmupFrames; warmup++)
                {
                    camera.Render();
                    yield return null;
                }
                camera.targetTexture = null;
                warmupTarget.Release();
                Object.DestroyImmediate(warmupTarget);
                Capture(camera, Path.Combine(OutputDirectory, views[i].name + ".png"));
            }

            timer.Stop();
            VoxelRenderBridge.SurfaceDebugTint = Color.white;
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
