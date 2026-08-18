using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Movement-specific near/far seam regressions. The far clipmap is snapped to its sample
    /// lattice while near-field residency follows the player continuously, so correctness must
    /// hold both inside a snap cell and while a moved ring is waiting for its replacement sample.
    /// </summary>
    public sealed class FarFieldMovementSafetyTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const float VoxelSize = 0.1f;

        [UnityTest, Timeout(900000)]
        public IEnumerator SubCellCameraMovementKeepsPublishedHoleInsideNearCoverage()
        {
            yield return LoadReadyShowcase();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.NotNull(showcase);
            Assert.NotNull(far);
            ShowcaseWorld world = GetWorld(showcase);
            Assert.NotNull(world);

            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            int spacing = far.SpacingForRing(0);
            float cellMetres = spacing * VoxelSize;
            int resolution = GetField<int>(far, "m_Resolution");
            List<int2> origins = GetField<List<int2>>(far, "_ringOrigin");
            int2 publishedOrigin = origins[0];
            Vector2 publishedCentre = RingCentre(publishedOrigin, spacing, resolution);

            // Move to the far diagonal corner of the *same* snap cell. Near coverage is centred
            // on this exact player position, while the published far hole remains centred on the
            // snapped lattice point. This is the largest offset possible without requesting a
            // new height sample and used to let the circular hole extend outside near coverage.
            Vector3 target = showcase.transform.position;
            target.x = publishedCentre.x + cellMetres - 0.05f;
            target.z = publishedCentre.y + cellMetres - 0.05f;
            showcase.transform.position = target;

            bool ready = false;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                int2 expectedOrigin = InvokeOriginFor(far, showcase.transform.position, spacing);
                int2 currentOrigin = GetField<List<int2>>(far, "_ringOrigin")[0];
                if (expectedOrigin.Equals(currentOrigin)
                    && RenderingComposition.HasCompletePublishedNearSurfaceCoverage()
                    && far.HoleRadiusMetres > 1f)
                {
                    ready = true;
                    break;
                }
            }
            Assert.True(ready,
                "The same-cell movement fixture never returned to a published, hole-open near/far state.");

            publishedOrigin = GetField<List<int2>>(far, "_ringOrigin")[0];
            publishedCentre = RingCentre(publishedOrigin, spacing, resolution);
            float centreOffset = Vector2.Distance(
                new Vector2(showcase.transform.position.x, showcase.transform.position.z),
                publishedCentre);
            float availableNearRadius = Mathf.Min(
                InvokeResidentGroundRadius(world, showcase.transform.position),
                far.InnerRadiusMetres);

            Assert.LessOrEqual(
                far.HoleRadiusMetres + centreOffset,
                availableNearRadius + 0.1f,
                $"The published far hole extends outside drawable near coverage after sub-cell "
              + $"movement. hole={far.HoleRadiusMetres:F2}m centreOffset={centreOffset:F2}m "
              + $"nearRadius={availableNearRadius:F2}m spacing={cellMetres:F2}m. "
              + "A snapped far hole must retain enough overlap for the player's continuous position.");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator LaggingRingZeroClosesPublishedHoleUntilReplacementArrives()
        {
            yield return LoadReadyShowcase();

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            VoxelFarTerrain far = Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.NotNull(showcase);
            Assert.NotNull(far);
            SetShowcaseField(showcase, "m_FlyMode", true);
            SetShowcaseField(showcase, "_mouseLook", false);

            int spacing = far.SpacingForRing(0);
            float cellMetres = spacing * VoxelSize;
            Assert.Greater(GetField<List<float>>(far, "_ringBuiltTopologyHoleMetres")[0], 1f,
                "Ring 0 never opened its normal near-field hole before the lag regression began.");

            bool observedLag = false;
            for (int step = 0; step < 12; step++)
            {
                // Cross a snap boundary every frame. Height sampling is single-flight, so at least
                // one rendered frame must retain an older published ring while the target origin
                // has already moved. That old mesh is valid fallback only if its hole is closed.
                showcase.transform.position += Vector3.right * (cellMetres * 1.25f);
                yield return null;

                int2 targetOrigin = InvokeOriginFor(far, showcase.transform.position, spacing);
                int2 publishedOrigin = GetField<List<int2>>(far, "_ringOrigin")[0];
                if (targetOrigin.Equals(publishedOrigin))
                    continue;

                observedLag = true;
                float publishedHole = GetField<List<float>>(
                    far, "_ringBuiltTopologyHoleMetres")[0];
                Assert.LessOrEqual(publishedHole, 0.05f,
                    $"Ring 0 is still drawing an open {publishedHole:F2}m hole from origin "
                  + $"{publishedOrigin} while the camera requires {targetOrigin}. The stale mesh "
                  + "must become full fallback coverage until its moved height sample publishes.");
            }

            Assert.True(observedLag,
                "Rapid movement never outran ring-0 publication, so the stale-hole invariant was not exercised.");
        }

        private static IEnumerator LoadReadyShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelFarTerrain far = null;
            bool ready = false;
            for (int frame = 0; frame < 1200; frame++)
            {
                yield return null;
                far = Object.FindFirstObjectByType<VoxelFarTerrain>();
                if (far == null) continue;
                List<bool> valid = GetField<List<bool>>(far, "_ringHeightValid");
                List<float> holes = GetField<List<float>>(far, "_ringBuiltTopologyHoleMetres");
                if (valid.Count > 0 && valid[0]
                    && holes.Count > 0 && !float.IsNaN(holes[0]) && holes[0] > 1f
                    && RenderingComposition.HasCompletePublishedNearSurfaceCoverage())
                {
                    ready = true;
                    break;
                }
            }

            Assert.True(ready,
                "Showcase never reached a complete published near surface with an authoritative open ring-0 hole.");
        }

        private static Vector2 RingCentre(int2 origin, int spacing, int resolution) =>
            new((origin.x + spacing * resolution / 2) * VoxelSize,
                (origin.y + spacing * resolution / 2) * VoxelSize);

        private static int2 InvokeOriginFor(VoxelFarTerrain far, Vector3 position, int spacing)
        {
            MethodInfo method = typeof(VoxelFarTerrain).GetMethod(
                "OriginFor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (int2)method.Invoke(far, new object[] { position, spacing });
        }

        private static float InvokeResidentGroundRadius(ShowcaseWorld world, Vector3 position)
        {
            MethodInfo method = typeof(ShowcaseWorld).GetMethod(
                "ResidentGroundRadiusMetres",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (float)method.Invoke(world, new object[] { position });
        }

        private static ShowcaseWorld GetWorld(VoxelShowcase showcase)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                "_world", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return field.GetValue(showcase) as ShowcaseWorld;
        }

        private static T GetField<T>(VoxelFarTerrain far, string fieldName)
        {
            FieldInfo field = typeof(VoxelFarTerrain).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(far);
        }

        private static void SetShowcaseField<T>(VoxelShowcase showcase, string fieldName, T value)
        {
            FieldInfo field = typeof(VoxelShowcase).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            field.SetValue(showcase, value);
        }
    }
}
