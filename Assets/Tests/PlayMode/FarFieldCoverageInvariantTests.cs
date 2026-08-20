using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Coverage invariants at the seam between extracted voxel geometry and the far heightfield.
    ///
    /// These deliberately do not use generated-region residency as a proxy for near coverage.
    /// A near column counts only when a published surface entry with non-zero geometry can
    /// actually submit a draw over that world-space column. Far coverage likewise comes from
    /// the published ring triangles rather than the configured ring radii.
    /// </summary>
    public sealed class FarFieldCoverageInvariantTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const float VoxelSize = 0.1f;

        [Test]
        public void IncompleteNearCoverageClosesAnAlreadyOpenFarFallbackHole()
        {
            var go = new GameObject("FarFieldCoverageInvariantTests.IncompleteNearCoverage");
            try
            {
                var far = go.AddComponent<VoxelFarTerrain>();
                SetField(far, "m_InnerRadiusMetres", 409.6f);
                SetField(far, "_requirePublishedNearCoverage", true);
                SetField(far, "_holeRadiusMetres", 391.5f);

                // No active surface pass means the current view has no proven near coverage.
                // This is the same transition produced by rising above the showcase and turning
                // the camera down before the newly visible chunks have published.
                far.HoleRadiusMetres = 409.6f;

                Assert.AreEqual(0f, far.HoleRadiusMetres,
                    "An unbacked far-field hole exposes the camera clear/sky through terrain.");
                Assert.AreEqual(409.6f,
                    GetField<float>(far, "_requestedHoleRadiusMetres"), 0.001f,
                    "The desired handoff radius must survive fallback closure so it can reopen "
                  + "after the current near view completes.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ColdStartMaintainsContinuousPublishedFallbackCoverage()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            int observedFrames = 0;
            bool sawStartupFallback = false;
            for (int frame = 0; frame < 120; frame++)
            {
                // UnityTest coroutines resume in the update phase. Sample after yielding so the
                // preceding rendered frame has executed VoxelFarTerrain.LateUpdate; inspecting
                // immediately after scene activation is pre-render state and cannot establish a
                // player-visible coverage hole.
                yield return null;

                VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
                VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
                if (showcase != null && far != null && TryWorld(showcase, out ShowcaseWorld world))
                {
                    bool startupFallbackActive = StartupFallbackActive(far);
                    if (sawStartupFallback && !startupFallbackActive)
                    {
                        Assert.True(AllRingHeightCachesValid(far),
                            "Cold-start fallback retired before every far ring had an authoritative height cache.");
                        break;
                    }

                    AssertContinuousCoverage(world, far, showcase.transform.position,
                        $"cold-start rendered frame {frame}");
                    observedFrames++;
                    sawStartupFallback |= startupFallbackActive;
                }
            }

            Assert.Greater(observedFrames, 0,
                "The showcase never exposed a world/far-field pair to validate during cold start.");
            Assert.True(sawStartupFallback,
                "The far field never published its cold-start fallback before async ring publication.");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator RingZeroHoleNeverExceedsDrawableNearCoverageInWorldSpace()
        {
            yield return LoadShowcase();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.NotNull(showcase);
            Assert.NotNull(far);
            Assert.True(TryWorld(showcase, out ShowcaseWorld world));

            // Check repeatedly while streaming changes the handoff. A radius computed from
            // generated residency can look correct at one instant while the renderer is still
            // missing the step-4 geometry that is supposed to fill the hole.
            for (int sample = 0; sample < 9; sample++)
            {
                AssertHoleIsBackedByDrawableNearGeometry(world, far,
                    showcase.transform.position, $"streaming sample {sample}");
                for (int i = 0; i < 15; i++) yield return null;
            }
        }

        [Test]
        public void PublishedParentChildRingsOverlapAcrossIndependentSnapStates()
        {
            var go = new GameObject("FarFieldCoverageInvariantTests.ParentChildOverlap");
            try
            {
                var far = go.AddComponent<VoxelFarTerrain>();
                const int resolution = 8;
                SetField(far, "m_InnerRadiusMetres", 12.8f);
                SetField(far, "m_OuterRadiusMetres", 64f);
                SetField(far, "m_Resolution", resolution);
                far.HoleRadiusMetres = 4f;
                Invoke(far, "EnsureRings");

                var heights = GetField<List<NativeArray<int>>>(far, "_ringHeights");
                var meshes = GetField<List<Mesh>>(far, "_ringMeshes");
                Assert.GreaterOrEqual(heights.Count, 2);
                Assert.GreaterOrEqual(meshes.Count, 2);

                for (int ring = 0; ring < 2; ring++)
                {
                    NativeArray<int> ringHeights = heights[ring];
                    for (int i = 0; i < ringHeights.Length; i++)
                        ringHeights[i] = ShowcaseWorld.BaseHeightVoxels;
                }

                int childSpacing = far.SpacingForRing(0);
                int parentSpacing = far.SpacingForRing(1);
                int2 childOrigin = new(
                    -(resolution / 2) * childSpacing + childSpacing,
                    -(resolution / 2) * childSpacing);
                int2 parentOrigin = new(
                    -(resolution / 2) * parentSpacing,
                    -(resolution / 2) * parentSpacing + parentSpacing);

                InvokeRebuild(far, 0, childOrigin, childSpacing);
                InvokeRebuild(far, 1, parentOrigin, parentSpacing);

                ProjectedMesh child = Project(meshes[0]);
                ProjectedMesh parent = Project(meshes[1]);
                Assert.Greater(child.Triangles.Length, 0);
                Assert.Greater(parent.Triangles.Length, 0);

                float sampleStep = Mathf.Max(0.025f, childSpacing * VoxelSize * 0.25f);
                float maxDistance = Mathf.Max(MaxRadius(child.Bounds), MaxRadius(parent.Bounds))
                                  + parentSpacing * VoxelSize;

                for (int angle = 0; angle < 360; angle += 45)
                {
                    float radians = angle * Mathf.Deg2Rad;
                    Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
                    List<CoverageInterval> childIntervals = CoverageIntervals(
                        child, direction, maxDistance, sampleStep);
                    List<CoverageInterval> parentIntervals = CoverageIntervals(
                        parent, direction, maxDistance, sampleStep);
                    Assert.Greater(childIntervals.Count, 0,
                        $"Child ring has no published coverage along {angle} degrees.");
                    Assert.Greater(parentIntervals.Count, 0,
                        $"Parent ring has no published coverage along {angle} degrees.");

                    float overlap = LargestOverlap(childIntervals, parentIntervals);
                    Assert.GreaterOrEqual(overlap, sampleStep * 0.5f,
                        $"Published child/parent rings do not geometrically overlap along "
                      + $"{angle} degrees after independent snaps. overlap={overlap:F3}m "
                      + $"childSpacing={childSpacing} parentSpacing={parentSpacing}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator CameraMovementNeverPublishesOuterRingAheadOfCriticalRing()
        {
            yield return LoadShowcase();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.NotNull(showcase);
            Assert.NotNull(far);

            // The clipmap samples one ring at a time. Do not turn startup throughput into a
            // movement-order assertion by assuming every ring can publish in eight frames.
            yield return WaitForFarFieldIdle(far, 240);

            List<Mesh> meshes = RingMeshes(far);
            Assert.GreaterOrEqual(meshes.Count, 2);
            Vector2[] before = RingCentres(meshes);
            for (int ring = 0; ring < before.Length; ring++)
                Assert.False(float.IsNaN(before[ring].x),
                    $"Far ring {ring} was not published before the movement regression began.");

            // Walking mode writes transform.position back from the CharacterMotor every Update.
            // Enable the showcase's production fly path so this artificial camera displacement is
            // persistent and the far renderer actually observes the requested movement.
            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            // Large enough to cross every correctness-critical snap cell and at least one outer
            // snap cell. We care about publication order, not how many jobs complete per frame.
            showcase.transform.position += Vector3.right
                * Mathf.Max(512f, far.InnerRadiusMetres * 1.5f);

            int[] firstChangedFrame = new int[meshes.Count];
            for (int i = 0; i < firstChangedFrame.Length; i++) firstChangedFrame[i] = -1;
            int changedOuterRings = 0;

            for (int frame = 0; frame < 180; frame++)
            {
                yield return null;
                Vector2[] current = RingCentres(meshes);
                for (int ring = 0; ring < current.Length; ring++)
                {
                    if (firstChangedFrame[ring] >= 0 || float.IsNaN(current[ring].x)) continue;
                    if ((current[ring] - before[ring]).sqrMagnitude <= 0.0001f) continue;

                    firstChangedFrame[ring] = frame;
                    if (ring == 0) continue;
                    changedOuterRings++;
                    Assert.GreaterOrEqual(firstChangedFrame[0], 0,
                        $"Outer far ring {ring} published its moved-camera sample at frame "
                      + $"{frame} while correctness-critical ring 0 was still at the old snap.");
                    Assert.LessOrEqual(firstChangedFrame[0], frame,
                        $"Outer far ring {ring} overtook correctness-critical ring 0 after camera movement.");
                }
            }

            Assert.GreaterOrEqual(firstChangedFrame[0], 0,
                "Correctness-critical ring 0 never followed the moved camera.");
            Assert.Greater(changedOuterRings, 0,
                "No outer ring crossed a snap boundary, so the publication-order invariant was not exercised.");
        }

        private static IEnumerator LoadShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            for (int i = 0; i < 8; i++) yield return null;
        }

        private static IEnumerator WaitForFarFieldIdle(VoxelFarTerrain far, int maxFrames)
        {
            for (int frame = 0; frame < maxFrames; frame++)
            {
                bool scheduled = GetField<bool>(far, "_heightJobScheduled");
                bool allValid = AllRingHeightCachesValid(far);
                List<Mesh> meshes = RingMeshes(far);
                bool allPublished = meshes.Count > 0;
                for (int ring = 0; ring < meshes.Count && allPublished; ring++)
                {
                    Mesh mesh = meshes[ring];
                    allPublished = mesh != null && mesh.vertexCount > 0
                                && mesh.triangles.Length > 0;
                }

                if (!scheduled && allValid && allPublished)
                    yield break;
                yield return null;
            }

            Assert.Fail($"Far clipmap did not publish and become idle within {maxFrames} frames.");
        }

        private static bool TryWorld(VoxelShowcase showcase, out ShowcaseWorld world)
        {
            world = null;
            FieldInfo field = typeof(VoxelShowcase).GetField(
                "_world", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return false;
            world = field.GetValue(showcase) as ShowcaseWorld;
            return world != null;
        }

        private static bool StartupFallbackActive(VoxelFarTerrain far) =>
            GetField<int>(far, "_startupFallbackRing") >= 0;

        private static bool AllRingHeightCachesValid(VoxelFarTerrain far)
        {
            List<bool> valid = GetField<List<bool>>(far, "_ringHeightValid");
            if (valid.Count == 0) return false;
            for (int ring = 0; ring < valid.Count; ring++)
                if (!valid[ring]) return false;
            return true;
        }

        private static void AssertContinuousCoverage(ShowcaseWorld world, VoxelFarTerrain far,
                                                     Vector3 centre, string phase)
        {
            List<Bounds> near = PublishedNearBounds();
            List<ProjectedMesh> farMeshes = ProjectedRingMeshes(far);
            float maxDistance = Mathf.Max(24f, far.InnerRadiusMetres - 1f);
            var holes = new List<string>();

            for (int angle = 0; angle < 360; angle += 30)
            {
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
                for (float distance = 12.8f; distance <= maxDistance; distance += 25.6f)
                {
                    Vector2 xz = new(centre.x + direction.x * distance,
                                     centre.z + direction.y * distance);
                    if (NearCoversColumn(world, near, xz) || FarCoversColumn(farMeshes, xz))
                        continue;
                    holes.Add($"{angle}deg@{distance:F1}m");
                    if (holes.Count >= 8) break;
                }
                if (holes.Count >= 8) break;
            }

            Assert.IsEmpty(holes,
                $"{phase}: published near geometry plus published far fallback leaves world-space "
              + $"coverage holes: {string.Join(", ", holes)}. "
              + $"nearDraws={near.Count} farMeshes={farMeshes.Count} hole={far.HoleRadiusMetres:F1}m.");
        }

        private static void AssertHoleIsBackedByDrawableNearGeometry(
            ShowcaseWorld world, VoxelFarTerrain far, Vector3 centre, string phase)
        {
            float hole = far.HoleRadiusMetres;
            if (hole <= 0.05f) return;

            List<Bounds> near = PublishedNearBounds();
            var missing = new List<string>();
            const int radialSamples = 8;
            for (int angle = 0; angle < 360; angle += 22)
            {
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
                for (int radial = 1; radial <= radialSamples; radial++)
                {
                    float distance = hole * radial / radialSamples;
                    Vector2 xz = new(centre.x + direction.x * distance,
                                     centre.z + direction.y * distance);
                    if (NearCoversColumn(world, near, xz)) continue;
                    missing.Add($"{angle}deg@{distance:F1}m");
                    if (missing.Count >= 8) break;
                }
                if (missing.Count >= 8) break;
            }

            Assert.IsEmpty(missing,
                $"{phase}: ring-0 hole={hole:F1}m extends beyond actual drawable near coverage. "
              + $"Missing published near geometry at {string.Join(", ", missing)}; "
              + $"publishedNearDraws={near.Count}.");
        }

        private static List<Bounds> PublishedNearBounds()
        {
            var result = new List<Bounds>();
            PropertyInfo activePassProperty = typeof(VoxelRenderBridge).GetProperty(
                "ActivePass", BindingFlags.NonPublic | BindingFlags.Static);
            object activePass = activePassProperty?.GetValue(null);
            if (activePass == null) return result;

            FieldInfo schedulerField = typeof(VoxelRenderPass).GetField(
                "_scheduler", BindingFlags.NonPublic | BindingFlags.Instance);
            var scheduler = schedulerField?.GetValue(activePass) as VoxelSurfaceScheduler;
            if (scheduler == null) return result;

            FieldInfo workersField = typeof(VoxelSurfaceScheduler).GetField(
                "_allWorkers", BindingFlags.NonPublic | BindingFlags.Instance);
            var workers = workersField?.GetValue(scheduler) as CpuTransvoxelChunkCache[];
            if (workers == null) return result;

            FieldInfo entriesField = typeof(CpuTransvoxelChunkCache).GetField(
                "_entries", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(entriesField);
            foreach (CpuTransvoxelChunkCache worker in workers)
            {
                var entries = entriesField.GetValue(worker)
                    as Dictionary<int3, CpuTransvoxelChunkCache.Entry>;
                if (entries == null) continue;
                foreach (CpuTransvoxelChunkCache.Entry entry in entries.Values)
                {
                    if (!entry.Ready || entry.IndexCount <= 0) continue;
                    result.Add(entry.WorldBounds(VoxelSize));
                }
            }
            return result;
        }

        private static bool NearCoversColumn(ShowcaseWorld world, List<Bounds> near, Vector2 xz)
        {
            int voxelX = Mathf.FloorToInt(xz.x / VoxelSize);
            int voxelZ = Mathf.FloorToInt(xz.y / VoxelSize);
            float surfaceY = world.SurfaceHeight(voxelX, voxelZ) * VoxelSize;
            Vector3 point = new(xz.x, surfaceY, xz.y);
            foreach (Bounds bounds in near)
            {
                if (point.x < bounds.min.x || point.x > bounds.max.x
                    || point.z < bounds.min.z || point.z > bounds.max.z
                    || point.y < bounds.min.y || point.y > bounds.max.y)
                    continue;
                return true;
            }
            return false;
        }

        private static List<ProjectedMesh> ProjectedRingMeshes(VoxelFarTerrain far)
        {
            var result = new List<ProjectedMesh>();
            foreach (Mesh mesh in RingMeshes(far))
            {
                if (mesh == null || mesh.vertexCount == 0 || mesh.triangles.Length == 0) continue;
                result.Add(Project(mesh));
            }
            return result;
        }

        private static bool FarCoversColumn(List<ProjectedMesh> meshes, Vector2 xz)
        {
            foreach (ProjectedMesh mesh in meshes)
                if (mesh.Covers(xz)) return true;
            return false;
        }

        private static List<Mesh> RingMeshes(VoxelFarTerrain far) =>
            GetField<List<Mesh>>(far, "_ringMeshes");

        private static Vector2[] RingCentres(List<Mesh> meshes)
        {
            var centres = new Vector2[meshes.Count];
            for (int i = 0; i < meshes.Count; i++)
            {
                Mesh mesh = meshes[i];
                if (mesh == null || mesh.vertexCount == 0)
                {
                    centres[i] = new Vector2(float.NaN, float.NaN);
                    continue;
                }
                Vector3 centre = mesh.bounds.center;
                centres[i] = new Vector2(centre.x, centre.z);
            }
            return centres;
        }

        private static List<CoverageInterval> CoverageIntervals(
            ProjectedMesh mesh, Vector2 direction, float maxDistance, float sampleStep)
        {
            var intervals = new List<CoverageInterval>();
            bool active = false;
            float start = 0f;
            float previous = 0f;
            for (float distance = 0f; distance <= maxDistance; distance += sampleStep)
            {
                bool covered = mesh.Covers(direction * distance);
                if (covered && !active)
                {
                    active = true;
                    start = distance;
                }
                else if (!covered && active)
                {
                    intervals.Add(new CoverageInterval(start, previous));
                    active = false;
                }
                previous = distance;
            }
            if (active) intervals.Add(new CoverageInterval(start, previous));
            return intervals;
        }

        private static float LargestOverlap(List<CoverageInterval> a, List<CoverageInterval> b)
        {
            float largest = 0f;
            foreach (CoverageInterval left in a)
            foreach (CoverageInterval right in b)
                largest = Mathf.Max(largest,
                    Mathf.Min(left.End, right.End) - Mathf.Max(left.Start, right.Start));
            return Mathf.Max(0f, largest);
        }

        private static float MaxRadius(Bounds bounds)
        {
            float max = 0f;
            Vector3 min = bounds.min;
            Vector3 extent = bounds.max;
            max = Mathf.Max(max, new Vector2(min.x, min.z).magnitude);
            max = Mathf.Max(max, new Vector2(min.x, extent.z).magnitude);
            max = Mathf.Max(max, new Vector2(extent.x, min.z).magnitude);
            max = Mathf.Max(max, new Vector2(extent.x, extent.z).magnitude);
            return max;
        }

        private static ProjectedMesh Project(Mesh mesh) =>
            new(mesh.vertices, mesh.triangles, mesh.bounds);

        private static void InvokeRebuild(VoxelFarTerrain far, int ring, int2 origin, int spacing)
        {
            MethodInfo method = typeof(VoxelFarTerrain).GetMethod(
                "RebuildRingFromCachedHeights", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(far, new object[] { ring, origin, spacing });
        }

        private static void Invoke(VoxelFarTerrain far, string methodName)
        {
            MethodInfo method = typeof(VoxelFarTerrain).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(far, null);
        }

        private static T GetField<T>(VoxelFarTerrain far, string fieldName)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field.GetValue(far);
        }

        private static void SetField<T>(VoxelFarTerrain far, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(far, value);
        }

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(showcase, value);
        }

        private readonly struct CoverageInterval
        {
            public readonly float Start;
            public readonly float End;
            public CoverageInterval(float start, float end)
            {
                Start = start;
                End = end;
            }
        }

        private readonly struct ProjectedMesh
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;
            public readonly Bounds Bounds;

            public ProjectedMesh(Vector3[] vertices, int[] triangles, Bounds bounds)
            {
                Vertices = vertices;
                Triangles = triangles;
                Bounds = bounds;
            }

            public bool Covers(Vector2 point)
            {
                if (point.x < Bounds.min.x || point.x > Bounds.max.x
                    || point.y < Bounds.min.z || point.y > Bounds.max.z)
                    return false;

                for (int i = 0; i < Triangles.Length; i += 3)
                {
                    Vector3 av = Vertices[Triangles[i]];
                    Vector3 bv = Vertices[Triangles[i + 1]];
                    Vector3 cv = Vertices[Triangles[i + 2]];
                    Vector2 a = new(av.x, av.z);
                    Vector2 b = new(bv.x, bv.z);
                    Vector2 c = new(cv.x, cv.z);
                    if (PointInTriangle(point, a, b, c)) return true;
                }
                return false;
            }

            private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = Cross(p - b, a - b);
                float d2 = Cross(p - c, b - c);
                float d3 = Cross(p - a, c - a);
                bool hasNegative = d1 < -0.00001f || d2 < -0.00001f || d3 < -0.00001f;
                bool hasPositive = d1 > 0.00001f || d2 > 0.00001f || d3 > 0.00001f;
                return !(hasNegative && hasPositive);
            }

            private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        }
    }
}
