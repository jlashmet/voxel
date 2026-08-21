using System.Collections;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Fast visual iteration loop for exterior art direction. It deliberately omits interiors,
    /// cutaways, traversal, and gameplay checks; those remain in the full visual suite.
    ///
    /// The PlayMode phase keeps the renderer/coverage assertions and exercises the same camera
    /// transitions, but it no longer writes editor RenderTexture pixels to disk. Selecting either
    /// test through the single-test workflow automatically builds VoxelShowcase as a real player
    /// and publishes presented-frame screenshots every ten seconds through the shared capture
    /// utility.
    /// </summary>
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: visual acceptance for human review; run it
    /// by name when you want the real-player artifacts.
    /// </remarks>
    [NUnit.Framework.Explicit("Visual acceptance for human review; run by name.")]
    public sealed class CastleExteriorLookdevTests
    {
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

            var uploadTarget = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = uploadTarget;
            // Surface extraction is incremental; give its bounded queue several render
            // frames before judging the surface state.
            for (int densityDrainFrame = 0; densityDrainFrame < 8; densityDrainFrame++)
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
        /// Flies the camera along a continuous path and renders every frame.
        ///
        /// Fixed teleported views hide everything the incremental surface cache does over time:
        /// chunks arriving behind the camera, the coarse level retiring before the fine level has
        /// filled in, and geometry from the previous viewpoint still resident. This keeps that
        /// motion exercise as a renderer assertion; persisted imagery comes from the real player.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator CaptureMovingCameraSequence()
        {
            IEnumerator setup = PrepareShowcase();
            while (setup.MoveNext()) yield return setup.Current;

            Vector3 centre = CastleCentre(_world);
            Vector3 start = centre + new Vector3(-58f, 20f, -104f);
            Vector3 end = centre + new Vector3(34f, 12f, -30f);
            Vector3 lookAt = centre + new Vector3(0f, 6f, 0f);

            const int frames = 96;
            const int sampleStride = 8;
            int sampleCount = 0;
            var motionTarget = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            try
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    float t = frame / (float)(frames - 1);
                    _camera.transform.position = Vector3.Lerp(start, end, t);
                    _camera.transform.LookAt(lookAt);

                    // Render every frame, never only the sampled ones. The cache advances by a
                    // bounded number of builds per frame, so skipping frames would hand it a
                    // budget no player ever gives it.
                    _camera.targetTexture = motionTarget;
                    _camera.Render();
                    _camera.targetTexture = null;
                    yield return null;

                    if (frame % sampleStride != 0) continue;
                    sampleCount++;
                    UnityEngine.Debug.Log($"### MOTION_SAMPLE {sampleCount:D2}: "
                                          + VoxelRenderBridge.LastSurfacePassState);
                }
            }
            finally
            {
                motionTarget.Release();
                Object.DestroyImmediate(motionTarget);
            }

            Assert.Greater(sampleCount, 4, "The motion sequence needs enough sampled frames to compare.");
        }

        private static Vector3 CastleCentre(ShowcaseWorld world)
        {
            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(256, ground, 376), world.Seed);
            return new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                               plan.Centre.z) * 0.1f;
        }

        // Generating the castle and exercising every view exceeds the 180 s default. Set
        // VOXEL_LOOKDEV_FILTER to exercise a single view when iterating on look.
        [UnityTest, Timeout(900000)]
        public IEnumerator CaptureExteriorLookdevViews()
        {
            var timer = Stopwatch.StartNew();
            IEnumerator prepare = PrepareShowcase();
            while (prepare.MoveNext()) yield return prepare.Current;
            ShowcaseWorld world = _world;
            Camera camera = _camera;

            int ground = world.SurfaceHeight(256, 376);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(256, ground, 376), world.Seed);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            Vector3 centre = new Vector3(plan.Centre.x, baseY, plan.Centre.z) * 0.1f;
            Vector3 waterfall = new Vector3(CastleLayout.WaterfallStreamX(in plan),
                                            baseY - 48,
                                            CastleLayout.WaterfallLipZ(in plan)) * 0.1f;
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
                // converge around that viewpoint before judging the full-resolution render.
                var warmupTarget = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = warmupTarget;
                for (int warmup = 0; warmup < warmupFrames; warmup++)
                {
                    camera.Render();
                    yield return null;
                }
                camera.targetTexture = null;
                warmupTarget.Release();
                Object.DestroyImmediate(warmupTarget);

                RenderView(camera, 1280, 720);
                UnityEngine.Debug.Log($"### VIEW_METRICS {views[i].name}: "
                                    + VoxelRenderBridge.LastSurfacePassState);
                VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.AreEqual(0, metrics.MissingVisibleSolidChunks,
                    $"{views[i].name} has known coarse chunks inside the camera frustum "
                  + "without ready geometry; this produces rectangular holes in the castle.");
                Assert.Greater(metrics.UploadedGeometryBytes, 0ul,
                    $"{views[i].name} did not render through the authoritative "
                  + "feature-aware surface extractor.");
            }

            timer.Stop();
            VoxelRenderBridge.SurfaceDebugTint = Color.white;
            UnityEngine.Debug.Log($"### CASTLE_LOOKDEV {timer.Elapsed.TotalSeconds:0.0}s " +
                                  $"voxels={world.CastleVoxels:N0}");
        }

        private static void RenderView(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = previous;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
