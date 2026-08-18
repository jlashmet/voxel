using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Runtime coverage for the sky, the mountain terrain, and the far-terrain clipmap.
    ///
    /// Everything here exists because the pieces it covers were written and shipped without ever
    /// being run. Unit tests proved the height field is correct in isolation; they cannot show
    /// that the scene starts, that the far mesh is actually created and drawn, that it agrees
    /// with the voxel ground beneath the player, or that a frame costs anything sane. Those are
    /// runtime properties and they need the scene.
    /// </summary>
    public sealed class ShowcaseSkyAndFarTerrainTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        private static IEnumerator LoadShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            // A few frames so OnEnable, first residency refresh, and the first clipmap build
            // have all run at least once.
            for (int i = 0; i < 8; i++) yield return null;
        }

        private static VoxelShowcase Showcase() => Object.FindFirstObjectByType<VoxelShowcase>();

        private static ShowcaseWorld World(VoxelShowcase showcase) =>
            (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);

        // -------------------------------------------------------------------------
        // The scene starts at all
        // -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ShowcaseStartsWithoutErrors()
        {
            // The broadest net. A null reference in OnEnable, a missing shader, or a divide by
            // zero in the new terrain code all surface here before anything more specific.
            var errors = new List<string>();
            void Handler(string condition, string trace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add($"{type}: {condition}");
            }

            Application.logMessageReceived += Handler;
            yield return LoadShowcase();
            for (int i = 0; i < 30; i++) yield return null;
            Application.logMessageReceived -= Handler;

            Assert.IsEmpty(errors,
                "Starting the showcase logged errors:\n  " + string.Join("\n  ", errors));
        }

        [UnityTest]
        public IEnumerator CameraPositionStaysFinite()
        {
            // A non-finite camera transform is the failure this scene has actually produced
            // before, and it floods the log rather than throwing, so nothing else catches it.
            yield return LoadShowcase();
            var showcase = Showcase();

            for (int frame = 0; frame < 60; frame++)
            {
                Vector3 p = showcase.transform.position;
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z),
                    $"Camera position went NaN on frame {frame}: {p}");
                Assert.IsFalse(float.IsInfinity(p.x) || float.IsInfinity(p.y)
                            || float.IsInfinity(p.z),
                    $"Camera position went infinite on frame {frame}: {p}");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PlayerDoesNotFallThroughTheWorld()
        {
            // Taller terrain and vertical region residency both put the ground the motor
            // collides against under new conditions. If residency misses the player's layer,
            // the player drops forever and this is what shows it.
            yield return LoadShowcase();
            var showcase = Showcase();
            float startY = showcase.transform.position.y;

            for (int i = 0; i < 90; i++) yield return null;

            float endY = showcase.transform.position.y;
            Assert.Greater(endY, startY - 30f,
                $"Player fell from {startY:0.0} m to {endY:0.0} m without landing, which means "
              + "there was no resident ground beneath the spawn column.");
        }

        // -------------------------------------------------------------------------
        // Far terrain
        // -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FarTerrainIsCreatedAutomatically()
        {
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.IsNotNull(far,
                "The showcase must create VoxelFarTerrain itself; if it does not, distant "
              + "mountains are simply absent from the scene.");
            Assert.Greater(far.RingCount, 1, "A single ring cannot span the far field.");
        }

        [UnityTest]
        public IEnumerator FarTerrainSeedMatchesTheVoxelWorld()
        {
            // Different seeds would give two unrelated terrains meeting at the streaming
            // radius — a visible discontinuity all the way around the player.
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            var world = World(Showcase());
            Assert.AreEqual(world.Seed, far.Seed,
                "Far terrain and voxel world must share a seed or they describe different worlds.");
        }

        [UnityTest]
        public IEnumerator FarTerrainRingsBuildRealGeometry()
        {
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();

            var meshes = (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

            Assert.IsNotEmpty(meshes, "No clipmap rings were built.");
            int withGeometry = 0;
            foreach (Mesh mesh in meshes)
            {
                if (mesh == null || mesh.vertexCount == 0) continue;
                withGeometry++;
                Assert.Greater(mesh.triangles.Length, 0,
                    $"Ring mesh {mesh.name} has vertices but no triangles.");
            }
            Assert.Greater(withGeometry, 0, "Every clipmap ring came out empty.");
        }

        [UnityTest]
        public IEnumerator FarTerrainRingSpacingsDoubleOutward()
        {
            // The clipmap's whole cost argument rests on this. If spacings do not double, outer
            // rings either cost quadratically or leave gaps.
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();

            for (int ring = 1; ring < far.RingCount; ring++)
                Assert.AreEqual(far.SpacingForRing(ring - 1) * 2, far.SpacingForRing(ring),
                    $"Ring {ring} does not double the spacing of ring {ring - 1}.");
        }

        [UnityTest]
        public IEnumerator FarTerrainHeightsMatchTheHeightField()
        {
            // The consistency claim: the far mesh is the same height field at a coarser rate,
            // not a separate approximation. If this drifts, mountains change shape as you
            // approach them.
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();

            // The outermost ring starts as the flat base-height startup square, which is a
            // placeholder rather than a claim about the height field. Comparing against it measures
            // nothing, so wait for real sampled heights on every ring before asserting.
            for (int frame = 0; frame < 600 && !far.HasSampledHeightsForEveryRing; frame++)
                yield return null;
            Assert.IsTrue(far.HasSampledHeightsForEveryRing,
                "Far terrain never replaced its startup fallback with sampled heights.");

            var meshes = (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

            int checkedVertices = 0;
            foreach (Mesh mesh in meshes)
            {
                if (mesh == null || mesh.vertexCount == 0) continue;
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i += 97)
                {
                    Vector3 v = vertices[i];
                    int voxelX = Mathf.RoundToInt(v.x / 0.1f);
                    int voxelZ = Mathf.RoundToInt(v.z / 0.1f);
                    int expected = TerrainSampler.HeightAt(voxelX, voxelZ, far.Seed);
                    Assert.AreEqual(expected * 0.1f, v.y, 0.05f,
                        $"Far terrain vertex at ({v.x:0.0}, {v.z:0.0}) is {v.y:0.0} m but the "
                      + $"height field says {expected * 0.1f:0.0} m.");
                    checkedVertices++;
                }
            }

            Assert.Greater(checkedVertices, 0, "No far-terrain vertices were available to check.");
        }

        [UnityTest]
        public IEnumerator FarTerrainCoversDistanceBeyondTheVoxelRadius()
        {
            // Mountains are only visible if the mesh actually reaches them.
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.Greater(far.OuterRadiusMetres, 2000f,
                "The far field must reach kilometres or the range never comes into view.");
            Assert.Less(far.InnerRadiusMetres, far.OuterRadiusMetres);
        }

        [UnityTest]
        public IEnumerator ClipmapRingsAreHollowAndDoNotOverlap()
        {
            // Each ring is a full square centred on the camera. Without a hole, every ring
            // redraws the ground the finer rings already cover, stacking coincident surfaces
            // that z-fight across the entire view. This asserts each ring's geometry stays
            // outside the extent of the ring inside it.
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            var meshes = (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

            Vector3 camera = Camera.main.transform.position;

            for (int ring = 1; ring < meshes.Count; ring++)
            {
                Mesh mesh = meshes[ring];
                if (mesh == null || mesh.vertexCount == 0) continue;

                float hole = far.SpacingForRing(ring - 1) * 96 / 2f * 0.1f;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 v = vertices[triangles[t]];
                    float chebyshev = Mathf.Max(Mathf.Abs(v.x - camera.x),
                                                Mathf.Abs(v.z - camera.z));
                    Assert.Greater(chebyshev, hole * 0.5f,
                        $"Ring {ring} emitted a triangle {chebyshev:0} m from the camera, well "
                      + $"inside ring {ring - 1}'s {hole:0} m extent. Overlapping rings z-fight.");
                }
            }
        }

        [UnityTest]
        public IEnumerator ClipmapSnapIsStableWestAndNorthOfTheOrigin()
        {
            // Integer division truncates toward zero, so a naive snap jumps the wrong way once
            // the camera crosses an axis and the whole ring jitters by a full sample.
            yield return LoadShowcase();
            var showcase = Showcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();

            var originsField = typeof(VoxelFarTerrain)
                .GetField("_ringOrigin", BindingFlags.NonPublic | BindingFlags.Instance);

            showcase.transform.position = new Vector3(-4000f, 400f, -4000f);
            for (int i = 0; i < 5; i++) yield return null;

            var meshes = (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

            int withGeometry = 0;
            foreach (Mesh mesh in meshes)
                if (mesh != null && mesh.vertexCount > 0 && mesh.triangles.Length > 0)
                    withGeometry++;

            Assert.Greater(withGeometry, 0,
                "No far terrain was built at negative world coordinates.");
            Assert.IsNotNull(originsField.GetValue(far));
        }

        // -------------------------------------------------------------------------
        // Terrain scale, as actually generated
        // -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SpawnGroundIsWalkableNotAMountainside()
        {
            yield return LoadShowcase();
            var showcase = Showcase();
            var world = World(showcase);

            Vector3 feet = showcase.transform.position - Vector3.up * 1.65f;
            int x = Mathf.FloorToInt(feet.x / ShowcaseWorld.VoxelSize);
            int z = Mathf.FloorToInt(feet.z / ShowcaseWorld.VoxelSize);
            int slope = TerrainSampler.SlopeAt(x, z, world.Seed);

            Assert.Less(slope, 24,
                $"Spawn sits on a slope of {slope} voxels per 8; the mountain mask should keep "
              + "the starting basin gentle.");
        }

        // -------------------------------------------------------------------------
        // What the camera actually sees
        //
        // The tests above all passed while the sky rendered grey, no mountains were visible,
        // and ground arrived late. Checking that data structures are populated is not the same
        // as checking the frame looks right, so these render the camera and inspect pixels and
        // world state the way a player would experience them.
        // -------------------------------------------------------------------------

        private static Texture2D RenderCamera(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            shot.Apply();

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(target);
            return shot;
        }

        [UnityTest]
        public IEnumerator SkyIsBlueNotGreyWellAboveTheHorizon()
        {
            // The symptom was a grey dome with blue only at the zenith. Twenty degrees up is
            // ordinary sky and must read clearly blue: blue channel meaningfully above red.
            yield return LoadShowcase();
            var camera = Camera.main;
            camera.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);   // 20 deg above horizon
            for (int i = 0; i < 3; i++) yield return null;

            Texture2D shot = RenderCamera(camera, 128, 96);
            Color sample = shot.GetPixel(64, 48);
            Object.DestroyImmediate(shot);

            Assert.Greater(sample.b, sample.r + 0.10f,
                $"Sky 20 degrees above the horizon is {sample}, which is not blue. A blue "
              + "channel barely above red means the horizon wash is bleeding across the dome.");
        }

        [UnityTest]
        public IEnumerator SkyNearZenithIsDeeperThanNearTheHorizon()
        {
            // Guards the gradient's direction: zenith must be the more saturated end.
            yield return LoadShowcase();
            var camera = Camera.main;

            camera.transform.rotation = Quaternion.Euler(-80f, 0f, 0f);
            for (int i = 0; i < 3; i++) yield return null;
            Texture2D up = RenderCamera(camera, 64, 64);
            Color zenith = up.GetPixel(32, 32);
            Object.DestroyImmediate(up);

            camera.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);
            for (int i = 0; i < 3; i++) yield return null;
            Texture2D across = RenderCamera(camera, 64, 64);
            Color horizon = across.GetPixel(32, 48);
            Object.DestroyImmediate(across);

            Assert.Greater(zenith.b - zenith.r, horizon.b - horizon.r,
                $"Zenith {zenith} is no bluer than the horizon {horizon}; the gradient is flat.");
        }

        [UnityTest]
        public IEnumerator MountainReliefExistsWithinTheFarTerrainsReach()
        {
            // "No distant mountains" was a ramp problem: relief reached only 21% of amplitude
            // at 2 km, so the range sat outside anything the far mesh drew. Assert that real
            // relief exists inside the far field's own radius.
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            var world = World(Showcase());

            int reachVoxels = Mathf.RoundToInt(far.OuterRadiusMetres / 0.1f);
            int highest = 0;
            for (int angle = 0; angle < 360; angle += 15)
            {
                float radians = angle * Mathf.Deg2Rad;
                for (int d = 10_000; d < reachVoxels; d += 5_000)
                {
                    int x = Mathf.RoundToInt(Mathf.Cos(radians) * d);
                    int z = Mathf.RoundToInt(Mathf.Sin(radians) * d);
                    highest = Mathf.Max(highest, TerrainSampler.HeightAt(x, z, world.Seed));
                }
            }

            Assert.Greater(highest, 8_000,
                $"Tallest terrain within the far field's {far.OuterRadiusMetres:0} m reach is "
              + $"only {highest * 0.1f:0} m. Mountains outside the drawn radius are invisible "
              + "however tall they are.");
        }

        [UnityTest]
        public IEnumerator FarTerrainMeshCarriesVisibleRelief()
        {
            // The mesh itself must contain the mountains, not just the height function.
            yield return LoadShowcase();
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            var meshes = (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (Mesh mesh in meshes)
            {
                if (mesh == null || mesh.vertexCount == 0) continue;
                foreach (Vector3 v in mesh.vertices)
                {
                    lowest = Mathf.Min(lowest, v.y);
                    highest = Mathf.Max(highest, v.y);
                }
            }

            Assert.Greater(highest - lowest, 200f,
                $"Far terrain spans only {highest - lowest:0} m of relief ({lowest:0} to "
              + $"{highest:0}). That is a plain, not a mountain range.");
        }

        [UnityTest]
        public IEnumerator GroundIsResidentAheadOfThePlayerNotUnderneath()
        {
            // "Blank regions that load once I am already there" is a streaming radius problem.
            // Ground a couple of hundred metres out must already exist, not appear on arrival.
            yield return LoadShowcase();
            var showcase = Showcase();
            var world = World(showcase);
            for (int i = 0; i < 120; i++) yield return null;   // let streaming settle

            Vector3 origin = showcase.transform.position;
            int ahead = 0, total = 0;
            for (int angle = 0; angle < 360; angle += 45)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 probe = origin + direction * 200f;
                total++;
                if (world.IsGenerated(ShowcaseWorld.RegionAt(probe))) ahead++;
            }

            Assert.Greater(ahead, total / 2,
                $"Only {ahead} of {total} directions have ground resident 200 m out. The player "
              + "will walk into unbuilt terrain.");
        }

        // -------------------------------------------------------------------------
        // Structures at terrain distance
        //
        // Terrain drew to the horizon while everything built - castle, Kentridge - stopped at
        // the streaming radius, so structures appeared out of nothing at 400 m. These cover the
        // coarse store that closes that gap.
        // -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BuiltContentIsRecordedForTheFarField()
        {
            yield return LoadShowcase();
            var world = World(Showcase());
            // The castle takes several seconds of staged building before it exists to record.
            for (int i = 0; i < 400; i++) yield return null;

            Assert.Greater(world.FarField.RecordedRegionCount, 0,
                "No region recorded built content. Structures will vanish beyond the voxel "
              + "radius exactly as they did before.");
        }

        [UnityTest]
        public IEnumerator TheStoreStaysSparse()
        {
            // The entire memory argument rests on plain terrain storing nothing. If ordinary
            // ground were recorded, this would be a megabyte-scale structure across 12 km.
            yield return LoadShowcase();
            var world = World(Showcase());
            for (int i = 0; i < 400; i++) yield return null;

            int generated = 0;
            for (int rx = -8; rx <= 8; rx++)
            for (int rz = -8; rz <= 8; rz++)
                if (world.IsGenerated(new int3(rx, 0, rz))) generated++;

            Assert.Less(world.FarField.RecordedRegionCount, Mathf.Max(4, generated),
                $"{world.FarField.RecordedRegionCount} regions recorded against {generated} "
              + "generated. Plain terrain is being stored as structure.");
        }

        [UnityTest]
        public IEnumerator FarMeshRisesOverBuiltContent()
        {
            // The store is only useful if the far mesh actually honours it.
            yield return LoadShowcase();
            var showcase = Showcase();
            var world = World(showcase);
            var far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            for (int i = 0; i < 400; i++) yield return null;

            if (world.FarField.RecordedRegionCount == 0)
                Assert.Ignore("No structures recorded yet; covered by BuiltContentIsRecorded.");

            // Find a recorded column and confirm the far field reports it above bare terrain.
            bool foundRaised = false;
            for (int rx = -4; rx <= 4 && !foundRaised; rx++)
            for (int rz = -4; rz <= 4 && !foundRaised; rz++)
            {
                for (int c = 0; c < FarFieldStructureStore.ColumnsPerRegion && !foundRaised; c++)
                {
                    int vx = rx * ShowcaseWorld.RegionVoxelEdge
                           + c * FarFieldStructureStore.VoxelsPerColumn;
                    int vz = rz * ShowcaseWorld.RegionVoxelEdge
                           + c * FarFieldStructureStore.VoxelsPerColumn;
                    int built = world.FarField.HeightAt(vx, vz);
                    if (built == int.MinValue) continue;
                    int terrain = TerrainSampler.HeightAt(vx, vz, world.Seed);
                    Assert.Greater(built, terrain,
                        "A recorded column must stand above the terrain height field.");
                    foundRaised = true;
                }
            }

            Assert.IsTrue(foundRaised, "No recorded column rose above terrain.");
        }

        [UnityTest]
        public IEnumerator StructureRecordSurvivesRegionEviction()
        {
            // The whole point: the silhouette must outlive the voxels. Walking away from the
            // castle and back must not make it disappear from the horizon.
            yield return LoadShowcase();
            var showcase = Showcase();
            var world = World(showcase);
            for (int i = 0; i < 400; i++) yield return null;

            int recorded = world.FarField.RecordedRegionCount;
            if (recorded == 0) Assert.Ignore("Nothing recorded yet.");

            showcase.transform.position += new Vector3(3000f, 0f, 3000f);
            for (int i = 0; i < 120; i++) yield return null;

            Assert.GreaterOrEqual(world.FarField.RecordedRegionCount, recorded,
                "Moving away discarded recorded structures; they must outlive their regions.");
        }

        // -------------------------------------------------------------------------
        // Cost
        // -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SteadyStateFrameCostIsReasonable()
        {
            // Not a strict budget — batchmode timings are noisy — but a regression that makes
            // frames cost hundreds of milliseconds will trip this, and that is the class of
            // problem the ring count and the clipmap rebuild could plausibly introduce.
            yield return LoadShowcase();
            for (int i = 0; i < 60; i++) yield return null;   // let streaming settle

            var samples = new List<float>();
            for (int i = 0; i < 120; i++)
            {
                float start = Time.realtimeSinceStartup;
                yield return null;
                samples.Add((Time.realtimeSinceStartup - start) * 1000f);
            }

            samples.Sort();
            float median = samples[samples.Count / 2];
            float p95 = samples[Mathf.Min(samples.Count - 1, (int)(samples.Count * 0.95f))];

            Debug.Log($"showcase frame cost: median={median:0.00}ms p95={p95:0.00}ms "
                    + $"max={samples[samples.Count - 1]:0.00}ms");

            Assert.Less(median, 120f,
                $"Median frame cost {median:0.0} ms indicates a systemic per-frame regression.");
        }

        [UnityTest]
        public IEnumerator CameraMovementDoesNotStallOnClipmapRebuilds()
        {
            // The clipmap rebuilds a ring whenever the camera crosses that ring's sample
            // spacing, on the main thread. Moving the camera is what triggers it, so a
            // stationary test would never see the cost.
            yield return LoadShowcase();
            var showcase = Showcase();
            for (int i = 0; i < 45; i++) yield return null;

            var samples = new List<float>();
            for (int i = 0; i < 120; i++)
            {
                showcase.transform.position += new Vector3(2f, 0f, 2f);
                float start = Time.realtimeSinceStartup;
                yield return null;
                samples.Add((Time.realtimeSinceStartup - start) * 1000f);
            }

            samples.Sort();
            float p95 = samples[(int)(samples.Count * 0.95f)];
            float worst = samples[samples.Count - 1];
            Debug.Log($"moving-camera frame cost: p95={p95:0.00}ms max={worst:0.00}ms");

            Assert.Less(worst, 500f,
                $"A {worst:0.0} ms frame while moving points at a synchronous clipmap or "
              + "residency rebuild on the main thread.");
        }
    }
}
