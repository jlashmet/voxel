using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using Unity.Profiling;
using UnityEngine.Profiling;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VoxelEngine.Tests.PlayMode
{
    /// <remarks>
    /// Diagnostic, not a gate. It answers one question the pass/fail traversal tests cannot:
    /// where the frame goes once the world is <em>fully</em> meshed, as opposed to while it is
    /// still assembling. The steady-state target is a moving player at 2 ms/frame, and preload
    /// time is explicitly free, so this spends whatever it needs up front and only then measures.
    /// </remarks>
    [NUnit.Framework.Explicit("Frame-budget diagnostic for human review; run by name.")]
    public sealed class ShowcaseFrameBudgetDiagnosticTests
    {
        /// <summary>
        /// Which showcase to measure. SmallVoxelShowcase is terrain plus one house, which isolates
        /// frame cost from the town and the castle.
        /// </summary>
        private static string ScenePath =>
            System.Environment.GetEnvironmentVariable("VOXEL_SHOWCASE_SCENE")
            ?? "Assets/Scenes/VoxelShowcase.unity";

        /// <summary>Ceiling on the preload phase. Generous: preload cost is not being measured.</summary>
        private static double PreloadSeconds =>
            double.TryParse(System.Environment.GetEnvironmentVariable("VOXEL_PRELOAD_SECONDS"),
                            out double seconds) && seconds > 0 ? seconds : 180.0;
        /// <summary>Frames per measured phase. Kept small for deep-profiled runs, whose capture
        /// grows without bound and whose timings are distorted anyway.</summary>
        private static int MeasuredFrames =>
            int.TryParse(System.Environment.GetEnvironmentVariable("VOXEL_MEASURED_FRAMES"),
                         out int frames) && frames > 0 ? frames : 200;

        /// <summary>Consecutive idle frames before the world is accepted as fully built.</summary>
        private const int QuietFramesForConvergence = 240;

        [UnityTest, Timeout(900000)]
        public IEnumerator ReportWhereTheFrameGoesOnceTheWorldIsFullyMeshed()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            Camera camera = Camera.main;
            Assert.NotNull(camera);
            Assert.IsTrue(VoxelRenderBridge.TryGetWorld(out _), "No render-world view.");

            // Resolution is overridable so the remaining render cost can be split into per-pixel
            // shading and per-draw submission: halving the pixels halves only the former.
            int width = int.TryParse(System.Environment.GetEnvironmentVariable("VOXEL_RT_WIDTH"),
                                     out int w) && w > 0 ? w : 960;
            int height = int.TryParse(System.Environment.GetEnvironmentVariable("VOXEL_RT_HEIGHT"),
                                      out int h) && h > 0 ? h : 540;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ShowcaseFrameBudgetDiagnostic.Target",
                antiAliasing = 1
            };
            RenderTexture previousTarget = camera.targetTexture;
            target.Create();
            camera.targetTexture = target;

            // A binary capture is opt-in and off by default: under -deepprofiling this run wrote
            // a 241 GB .raw and took the volume to 84% full. Nothing in Unity bounds that file, so
            // it is only ever enabled deliberately, and only around the measured window.
            bool captureBinaryLog =
                System.Environment.GetEnvironmentVariable("VOXEL_PROFILER_CAPTURE") == "1";
            if (captureBinaryLog)
            {
                string captureDirectory = System.IO.Path.GetFullPath("Artifacts/Baseline");
                System.IO.Directory.CreateDirectory(captureDirectory);
                Profiler.logFile = System.IO.Path.Combine(captureDirectory, "showcase-frame");
                Profiler.enableBinaryLog = true;
            }

            double previousScale = VoxelRenderBridge.SurfaceConvergenceBudgetScale;
            int previousConverging = VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging;
            int previousConverged = VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged;
            int previousResident = VoxelRenderBridge.SurfaceMaxResidentChunksPerRing;
            try
            {
                // Preload flat out. The converged cap is the one that normally starves prefetch,
                // so it is raised to match the converging cap rather than left at its default.
                VoxelRenderBridge.SurfaceConvergenceBudgetScale = 64.0;
                VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = 12;
                VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged = 12;
                // Residency must fit the arena or the two fight: the cache admits, the arena
                // evicts, the evicted chunk goes dirty, and extraction never stops. Overridable so
                // a run can compare the overcommitted default against a cap that actually fits.
                string capOverride =
                    System.Environment.GetEnvironmentVariable("VOXEL_MAX_RESIDENT_PER_RING");
                if (int.TryParse(capOverride, out int cap) && cap > 0)
                    VoxelRenderBridge.SurfaceMaxResidentChunksPerRing = cap;

                // Convergence budgets are sized for a 410 m world on a frame budget. A small map
                // can afford far more, and preload time is explicitly not what is being measured.
                if (double.TryParse(System.Environment.GetEnvironmentVariable("VOXEL_BUILD_MS"),
                                    out double buildMs) && buildMs > 0)
                    VoxelRenderBridge.SolidBuildBudgetMs = buildMs;
                if (double.TryParse(System.Environment.GetEnvironmentVariable("VOXEL_DISCOVERY_MS"),
                                    out double discoveryMs) && discoveryMs > 0)
                    VoxelRenderBridge.SurfaceDiscoveryBudgetMs = discoveryMs;

                // Builds in flight, applied before preload. Each build spans many frames, so the
                // world fills at concurrency divided by build latency — a per-frame millisecond
                // budget cannot speed it up, and raising those did nothing at all.
                if (int.TryParse(
                        System.Environment.GetEnvironmentVariable("VOXEL_PRELOAD_BUILD_CAP"),
                        out int preloadCap) && preloadCap > 0)
                {
                    VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = preloadCap;
                    VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged = preloadCap;
                }

                var preload = Stopwatch.StartNew();
                VoxelSurfaceMetrics m = default;
                int quietFrames = 0;
                int lastKnownChunks = -1;
                while (preload.Elapsed.TotalSeconds < PreloadSeconds)
                {
                    camera.Render();
                    yield return null;
                    m = VoxelRenderBridge.SurfaceMetrics;

                    // Meshing going quiet does not mean the world is built: region generation
                    // keeps running and each finished region discovers new chunks. Requiring the
                    // quiet state to hold rides through those gaps instead of declaring victory
                    // in the first one — which previously ended preload after 1.4 s with two
                    // regions generated and called an empty world converged.
                    // Surface discovery runs on its own small per-frame budget and keeps finding
                    // chunks long after meshing has gone quiet, so a still-growing known count is
                    // itself proof the world is not finished.
                    bool quiet = m.SolidKnownChunks > 0 && m.VisibleSolidChunks > 0
                              && m.MissingVisibleSolidChunks == 0 && m.SolidDirtyChunks == 0
                              && m.RunningSolidJobs == 0
                              && m.SolidKnownChunks == lastKnownChunks;
                    lastKnownChunks = m.SolidKnownChunks;
                    quietFrames = quiet ? quietFrames + 1 : 0;
                    if (quietFrames >= QuietFramesForConvergence) break;
                }
                int storageRegions = -1;
                if (VoxelRenderBridge.TryGetWorld(out VoxelWorldView view))
                    using (var resident = view.Storage.GetResidentRegionCoords(Allocator.Temp))
                        storageRegions = resident.Length;

                // The far field draws through Graphics.DrawMesh, which a manual camera.Render()
                // capture cannot see — so its state has to be asserted numerically rather than
                // looked at. A zero hole means the clipmap is covering the near field.
                float holeMetres = -1f;
                foreach (VoxelFarTerrain far in Object.FindObjectsByType<VoxelFarTerrain>(
                             FindObjectsSortMode.None))
                    holeMetres = far.HoleRadiusMetres;
                Debug.Log($"DIAG farfield hole={holeMetres:0.0}m "
                        + $"coverage={RenderingComposition.HasCompletePublishedNearSurfaceCoverage()}");

                Debug.Log($"DIAG preload {preload.Elapsed.TotalSeconds:0.0}s "
                        + $"storageRegions={storageRegions} "
                        + $"residentCap={VoxelRenderBridge.SurfaceMaxResidentChunksPerRing} "
                        + $"surfaceBricks={m.DiscoveredSurfaceBricks} changes={m.ChangeRecords} "
                        + $"known={m.SolidKnownChunks} resident={m.SolidResidentChunks} "
                        + $"dirty={m.SolidDirtyChunks} visible={m.VisibleSolidChunks} "
                        + $"missing={m.MissingVisibleSolidChunks} jobs={m.RunningSolidJobs} "
                        + $"arenaUsed={m.SolidArenaUsedBytes} arenaCommitted={m.SolidArenaCommittedBytes} "
                        + $"arenaFailures={m.SolidArenaAllocationFailures} "
                        + $"arenaEvictions={m.SolidArenaPressureEvictions}");

                // Restore production tuning before measuring: the point is what a shipped frame
                // costs, not what a frame costs under diagnostic budgets.
                VoxelRenderBridge.SurfaceConvergenceBudgetScale = previousScale;
                VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = previousConverging;
                VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged = previousConverged;

                // Look at the farm from a series of fixed ranges. Walking past it measured an
                // empty frustum once the camera left the world; holding the landmark in view at a
                // bounded distance is what actually exercises its LOD.
                if (System.Environment.GetEnvironmentVariable("VOXEL_SKIP_RANGES") != "1")
                    yield return CaptureAtRanges(camera);

                // Concurrency cap for the measured phases. Each build in flight costs main-thread
                // time inside the render pass, so this trades convergence speed — which is free
                // here, preload having already run — against the cost of a moving frame.
                if (int.TryParse(
                        System.Environment.GetEnvironmentVariable("VOXEL_MOVING_BUILD_CAP"),
                        out int movingCap) && movingCap > 0)
                {
                    VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = movingCap;
                    VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged = movingCap;
                }

                // The far-terrain clipmap rebuilds its ring meshes as the view moves, at a cost
                // that does not depend on how much voxel terrain is visible. Disabling it isolates
                // that from everything else in the frame.
                if (System.Environment.GetEnvironmentVariable("VOXEL_NO_FAR_TERRAIN") == "1")
                    foreach (VoxelFarTerrain far in Object.FindObjectsByType<VoxelFarTerrain>(
                                 FindObjectsSortMode.None))
                        far.enabled = false;

                // Shrinking the meshed radius cuts visible chunks without touching anything else,
                // which is how per-chunk draw submission can be separated from per-pixel cost.
                if (float.TryParse(System.Environment.GetEnvironmentVariable("VOXEL_RING_METRES"),
                                   out float ringMetres) && ringMetres > 0f)
                    VoxelRenderBridge.SurfaceMaxVoxelRingRadiusMetres = ringMetres;

                // Shadow rendering is re-done when the view moves, so it shows up as render cost
                // only while moving. Turning it off isolates how much of that cost it is.
                if (System.Environment.GetEnvironmentVariable("VOXEL_NO_SHADOWS") == "1")
                    foreach (Light light in Object.FindObjectsByType<Light>(
                                 FindObjectsSortMode.None))
                        light.shadows = LightShadows.None;

                // The world is built; freeze streaming so the patrol measures movement through
                // finished terrain rather than the cost of generating ground for the first time.
                if (System.Environment.GetEnvironmentVariable("VOXEL_FREEZE_STREAMING") == "1")
                {
                    var streamer = Object.FindFirstObjectByType<VoxelShowcase>();
                    if (streamer != null) streamer.StreamingEnabled = false;
                }

                yield return Measure(camera, "stationary", moveMetresPerFrame: 0f);
                // Patrol inside the streamed world rather than walking out of it. Preload is
                // explicitly free, so the question is what a moving frame costs once the ground
                // the player crosses is already built — walking outward measures region
                // generation instead, which is a different question.
                yield return Patrol(camera, "patrol-slow", metresPerFrame: 0.35f, extentMetres: 30f);
                yield return Patrol(camera, "patrol-fast", metresPerFrame: 1.5f, extentMetres: 40f);
            }
            finally
            {
                VoxelRenderBridge.SurfaceConvergenceBudgetScale = previousScale;
                VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = previousConverging;
                VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged = previousConverged;
                VoxelRenderBridge.SurfaceMaxResidentChunksPerRing = previousResident;
                Profiler.enabled = false;
                if (captureBinaryLog)
                {
                    Profiler.enableBinaryLog = false;
                    Profiler.logFile = null;
                }
                camera.targetTexture = previousTarget;
                target.Release();
                Object.Destroy(target);
            }
        }

        /// <summary>
        /// Frames the landmark from several ranges, holding it in view at each.
        ///
        /// The showcase rewrites the camera every frame, so both position and rotation are set
        /// immediately before the render they affect and settled for a few frames first, letting
        /// visibility and ring selection catch up to the new viewpoint.
        /// </summary>
        private static IEnumerator CaptureAtRanges(Camera camera)
        {
            var driver = Object.FindFirstObjectByType<VoxelShowcase>();
            Transform view = camera.transform;
            const float voxelSize = 0.1f;

            // Four sides at close range, then a distance ladder. Only the front facade is
            // configured with windows, so which side is which has to be established by looking
            // rather than assumed from the anchor's declared facing.
            // Orbit the house at close range from an elevated eye, so every facade is framed
            // regardless of which way the front ended up facing and regardless of local terrain.
            var shots = new (string Name, float X, float Z)[]
            {
                ("wall-S", 0f, -5f),     ("close-S", 0f, -9f),
                ("close-E", 9f, 0f),     ("close-W", -9f, 0f),
            };

            foreach ((string name, float dx, float dz) in shots)
            {
                // The placement is the house's minimum corner, so aim at its middle.
                var houseCentre = new Vector3(ShowcaseWorld.LandmarkCentreX * voxelSize + 4f,
                                              0f,
                                              ShowcaseWorld.LandmarkCentreZ * voxelSize + 2.8f);
                Vector3 eye = new Vector3(houseCentre.x + dx, 0f, houseCentre.z + dz);

                // Move the player, so streaming requests the regions this viewpoint needs, then
                // let it settle before judging what the frame looks like.
                if (driver != null) driver.TeleportTo(eye);

                VoxelSurfaceMetrics m = default;
                var settle = Stopwatch.StartNew();
                int quiet = 0;
                while (settle.Elapsed.TotalSeconds < 45.0 && quiet < 90)
                {
                    // Raise the eye and look slightly down so the whole facade is in frame even
                    // when the ground under the camera is higher than the ground under the house.
                    // Level with the openings rather than looking down at the base, so glazing is
                    // judged on the window itself instead of the wall below it.
                    Vector3 raised = view.position + Vector3.up * 1.7f;
                    view.position = raised;
                    Vector3 target = new Vector3(houseCentre.x, raised.y, houseCentre.z);
                    view.rotation = Quaternion.LookRotation(target - raised);
                    camera.Render();
                    yield return null;
                    m = VoxelRenderBridge.SurfaceMetrics;
                    quiet = (m.MissingVisibleSolidChunks == 0 && m.SolidDirtyChunks == 0
                             && m.RunningSolidJobs == 0) ? quiet + 1 : 0;
                }

                Debug.Log($"DIAG {name} settled={settle.Elapsed.TotalSeconds:0.0}s "
                        + $"visible={m.VisibleSolidChunks} missing={m.MissingVisibleSolidChunks} "
                        + $"dirty={m.SolidDirtyChunks} jobs={m.RunningSolidJobs} "
                        + $"known={m.SolidKnownChunks}");
                yield return CaptureEndOfFrame(camera, name);
            }
        }

        /// <summary>
        /// Writes what Unity actually renders, at end of frame.
        ///
        /// An explicit camera.Render() draws only what the render pipeline submits for that call.
        /// Anything registered for the ordinary loop — Graphics.DrawMesh far terrain, instanced
        /// vegetation, ambient life — is simply absent, so a capture taken that way can show a
        /// clean scene while the real frame has a clipmap drawn across it. Waiting for the end of
        /// a real frame captures the composited result instead.
        /// </summary>
        private static IEnumerator CaptureEndOfFrame(Camera camera, string label)
        {
            // Two ordinary frames rather than WaitForEndOfFrame, which does not fire in batch
            // mode. A camera with a target texture is rendered by the loop anyway, and letting it
            // do so is the whole point: that pass includes the DrawMesh submissions an explicit
            // Render() call leaves out.
            yield return null;
            yield return null;

            RenderTexture target = camera.targetTexture;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply(false, false);
            RenderTexture.active = previous;

            string directory = System.IO.Path.GetFullPath("Artifacts/Baseline/Frames");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(directory, $"{label}.png"), image.EncodeToPNG());
            Object.Destroy(image);
        }

        /// <summary>
        /// Measures movement that stays inside the already-streamed world, turning back at the
        /// extent instead of walking out of it.
        /// </summary>
        private static IEnumerator Patrol(Camera camera, string label,
                                          float metresPerFrame, float extentMetres)
        {
            var driver = Object.FindFirstObjectByType<VoxelShowcase>();
            Transform view = camera.transform;
            Vector3 centre = view.position;
            Vector3 forward = view.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;

            var frameMs = new List<double>(MeasuredFrames);
            var renderMs = new List<double>(MeasuredFrames);
            using var probes = new Probes();
            Profiler.enabled = true;
            var frameClock = Stopwatch.StartNew();
            VoxelSurfaceMetrics m = default;
            float travelled = 0f;
            float direction = 1f;

            for (int i = 0; i < MeasuredFrames; i++)
            {
                travelled += metresPerFrame * direction;
                if (Mathf.Abs(travelled) >= extentMetres) direction = -direction;
                Vector3 where = centre + forward * travelled;

                // Camera-only movement skips SnapToGround and the world's residency follow-up,
                // which separates the cost of moving the player from the cost of the view moving.
                if (System.Environment.GetEnvironmentVariable("VOXEL_CAMERA_ONLY") == "1")
                    view.position = where;
                else if (driver != null)
                    driver.TeleportTo(where);

                var renderClock = Stopwatch.StartNew();
                camera.Render();
                renderMs.Add(renderClock.Elapsed.TotalMilliseconds);

                frameClock.Restart();
                yield return null;
                frameMs.Add(frameClock.Elapsed.TotalMilliseconds);
                probes.Accumulate();
                m = VoxelRenderBridge.SurfaceMetrics;
            }

            frameMs.Sort();
            renderMs.Sort();
            Debug.Log($"DIAG {label} frameP50={P(frameMs, 0.50):0.00}ms "
                    + $"frameP95={P(frameMs, 0.95):0.00}ms frameP99={P(frameMs, 0.99):0.00}ms "
                    + $"frameMax={P(frameMs, 1.0):0.00}ms renderP50={P(renderMs, 0.50):0.00}ms "
                    + $"renderP95={P(renderMs, 0.95):0.00}ms "
                    + $"| visible={m.VisibleSolidChunks} missing={m.MissingVisibleSolidChunks} "
                    + $"dirty={m.SolidDirtyChunks} jobs={m.RunningSolidJobs} "
                    + $"changes={m.ChangeRecords} known={m.SolidKnownChunks} "
                    + $"resident={m.SolidResidentChunks} stale={m.RejectedStaleSolidBuilds} "
                    + $"completed={m.CompletedSolidBuilds} "
                    + $"arenaEvict={m.SolidArenaPressureEvictions} "
                    + $"capacity={m.SolidCapacityPressureEvents}");
            Debug.Log($"PROBE {label} {probes.Summarise()}");
            // Which extraction phase overruns the per-frame build budget. The budget gates
            // admission between workers, but a slice inside one can run long, and these say which.
            Debug.Log($"PHASE {label} "
                    + $"snapshotP95={m.SnapshotTiming.P95Ms:0.00} max={m.SnapshotTiming.MaxMs:0.00} "
                    + $"densityP95={m.DensityJobTurnaroundTiming.P95Ms:0.00} "
                    + $"compactP95={m.TopologyCompactTiming.P95Ms:0.00} "
                    + $"facetedP95={m.FacetedMergeTiming.P95Ms:0.00} "
                    + $"uploadP95={m.UploadTiming.P95Ms:0.00} max={m.UploadTiming.MaxMs:0.00} "
                    + $"buildLatencyP95={m.BuildLatencyTiming.P95Ms:0.00} "
                    + $"capacityP95={m.CapacityTiming.P95Ms:0.00} "
                    + $"prunP95={m.ResidencyPruneTiming.P95Ms:0.00} "
                    + $"selectP95={m.BuildSelectionTiming.P95Ms:0.00}");
            Profiler.enabled = false;
            yield return CaptureEndOfFrame(camera, label);
        }

        private static IEnumerator Measure(Camera camera, string label, float moveMetresPerFrame)
        {
            var frameMs = new List<double>(MeasuredFrames);
            var renderMs = new List<double>(MeasuredFrames);
            Transform cameraTransform = camera.transform;
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;

            using var probes = new Probes();
            Profiler.enabled = true;
            var frameClock = Stopwatch.StartNew();
            VoxelSurfaceMetrics m = default;
            // Movement has to move the player, not the camera. Streaming and residency follow the
            // showcase transform, so a camera-only walk measures a stationary world with a sliding
            // viewpoint — no region requests, no extraction, and a frame cost that flatters itself.
            var driver = Object.FindFirstObjectByType<VoxelShowcase>();
            Vector3 origin = cameraTransform.position;
            for (int i = 0; i < MeasuredFrames; i++)
            {
                if (moveMetresPerFrame > 0f && driver != null)
                    driver.TeleportTo(origin + forward * (moveMetresPerFrame * i));

                var renderClock = Stopwatch.StartNew();
                camera.Render();
                renderMs.Add(renderClock.Elapsed.TotalMilliseconds);

                frameClock.Restart();
                yield return null;
                frameMs.Add(frameClock.Elapsed.TotalMilliseconds);
                probes.Accumulate();
                m = VoxelRenderBridge.SurfaceMetrics;
            }

            frameMs.Sort();
            renderMs.Sort();
            Debug.Log($"DIAG {label} "
                    + $"frameP50={P(frameMs, 0.50):0.00}ms frameP95={P(frameMs, 0.95):0.00}ms "
                    + $"frameP99={P(frameMs, 0.99):0.00}ms frameMax={P(frameMs, 1.0):0.00}ms "
                    + $"renderP50={P(renderMs, 0.50):0.00}ms renderP95={P(renderMs, 0.95):0.00}ms "
                    + $"renderMax={P(renderMs, 1.0):0.00}ms "
                    + $"| schedPrepP50={m.SchedulerPrepareTiming.P50Ms:0.000} "
                    + $"schedPrepP95={m.SchedulerPrepareTiming.P95Ms:0.000} "
                    + $"workerPrepP95={m.WorkerPrepareTiming.P95Ms:0.000} "
                    + $"visibilityP95={m.VisibilityTiming.P95Ms:0.000} "
                    + $"| visible={m.VisibleSolidChunks} missing={m.MissingVisibleSolidChunks} "
                    + $"dirty={m.SolidDirtyChunks} jobs={m.RunningSolidJobs} "
                    + $"geomJobs={m.RunningGeometryJobs} "
                    + $"allocBytes={m.LastFrameManagedAllocationBytes} "
                    + $"arenaUsed={m.SolidArenaUsedBytes} "
                    + $"arenaFailures={m.SolidArenaAllocationFailures} "
                    + $"arenaEvictions={m.SolidArenaPressureEvictions} "
                    + $"uploadBytes={m.UploadedGeometryBytes}");
            Debug.Log($"PROBE {label} {probes.Summarise()}");
            yield return CaptureEndOfFrame(camera, label);
            Profiler.enabled = false;
        }

        /// <summary>
        /// Reads Unity's own counters rather than inferring them from wall clock.
        ///
        /// The wall-clock numbers elsewhere in this file say how much time a phase took but not
        /// where it went; these say where. Draw-call and SetPass counts test the per-chunk
        /// submission cost directly, and the job-wait markers test whether the main thread is
        /// actually blocked on extraction rather than merely correlated with it.
        /// </summary>
        private sealed class Probes : System.IDisposable
        {
            private readonly List<(string Name, ProfilerRecorder Recorder, bool IsTime)> _probes = new();

            public Probes()
            {
                Add("drawCalls", ProfilerCategory.Render, "Draw Calls Count", false);
                Add("setPass", ProfilerCategory.Render, "SetPass Calls Count", false);
                Add("batches", ProfilerCategory.Render, "Batches Count", false);
                Add("verts", ProfilerCategory.Render, "Vertices Count", false);
                // Main-thread stalls. Names differ between Unity versions, so every probe is
                // validity-checked and simply reports n/a when the marker is absent.
                Add("jobWait", ProfilerCategory.Internal, "Semaphore.WaitForSignal", true);
                Add("gfxWait", ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", true);
                Add("schedPrep", ProfilerCategory.Scripts, "Voxel.Surface.SchedulerPrepare", true);
                Add("workerAdmit", ProfilerCategory.Scripts, "Voxel.Surface.WorkerAdmission", true);
                Add("workerPrep", ProfilerCategory.Scripts, "Voxel.Surface.WorkerPrepare", true);
                Add("visibility", ProfilerCategory.Scripts, "Voxel.Surface.Visibility", true);
            }

            private void Add(string name, ProfilerCategory category, string stat, bool isTime)
            {
                // Sum every sample in the frame. The default reports the last one, so a marker
                // entered once per frame read as a true total while one entered per worker read
                // as a single call — which made the per-worker cost look like it had vanished.
                var recorder = ProfilerRecorder.StartNew(
                    category, stat, 1, ProfilerRecorderOptions.SumAllSamplesInFrame);
                _probes.Add((name, recorder, isTime));
            }

            private readonly List<List<double>> _history = new();

            /// <summary>Records this frame's value for every probe. Must be called each frame:
            /// a single reading at the end of a phase describes one frame, not the phase.</summary>
            public void Accumulate()
            {
                while (_history.Count < _probes.Count) _history.Add(new List<double>());
                for (int i = 0; i < _probes.Count; i++)
                {
                    (_, ProfilerRecorder recorder, bool isTime) = _probes[i];
                    if (!recorder.Valid) continue;
                    _history[i].Add(isTime ? recorder.LastValue * 1e-6 : recorder.LastValue);
                }
            }

            /// <summary>Median and 95th percentile per probe across the accumulated frames.</summary>
            public string Summarise()
            {
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < _probes.Count; i++)
                {
                    (string name, ProfilerRecorder recorder, bool isTime) = _probes[i];
                    if (!recorder.Valid || i >= _history.Count || _history[i].Count == 0)
                    {
                        text.Append(name).Append("=n/a ");
                        continue;
                    }
                    List<double> samples = _history[i];
                    samples.Sort();
                    string unit = isTime ? "ms" : "";
                    text.Append(name).Append("=p50:").Append(P(samples, 0.50).ToString("0.00"))
                        .Append(unit).Append("/p95:").Append(P(samples, 0.95).ToString("0.00"))
                        .Append(unit).Append(' ');
                }
                return text.ToString();
            }

            public void Dispose()
            {
                foreach ((_, ProfilerRecorder recorder, _) in _probes)
                    if (recorder.Valid) recorder.Dispose();
                _probes.Clear();
            }
        }

        private static double P(List<double> sorted, double fraction)
        {
            if (sorted.Count == 0) return 0.0;
            return sorted[Mathf.Clamp((int)(fraction * (sorted.Count - 1)), 0, sorted.Count - 1)];
        }
    }
}
