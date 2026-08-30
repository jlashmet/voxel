using System.Collections;
using MountingForce.WorldGen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Boots the exact worldbuilding gallery scene through its production MonoBehaviour path.
    /// CI proves the showcase enables, binds a rendering world, publishes resident geometry, and
    /// presents the reservation inspection evidence required by the spatial-reservation feature.
    /// </summary>
    public sealed class WorldbuildingGalleryShowcaseSmokeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator WorldbuildingGallerySceneBootsAndPublishesGeometry()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/WorldbuildingGalleryShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("WorldbuildingGalleryShowcase", LoadSceneMode.Single);
#endif
            yield return null;

            WorldbuildingGalleryShowcase showcase =
                Object.FindAnyObjectByType<WorldbuildingGalleryShowcase>();
            Assert.NotNull(showcase, "Worldbuilding gallery driver was not present after scene load.");

            Camera camera = showcase.GetComponent<Camera>();
            Assert.NotNull(camera, "Worldbuilding gallery driver must run on its production camera.");
            Assert.True(camera.enabled, "Worldbuilding gallery camera should be enabled after boot.");

            SpatialReservationGalleryOverlay overlay =
                showcase.GetComponent<SpatialReservationGalleryOverlay>();
            Assert.NotNull(overlay,
                "Worldbuilding gallery must install the production reservation inspection overlay.");
            Assert.True(overlay.Visible,
                "Reservation evidence must be visible on boot so unattended built-player capture records it.");
            Assert.NotNull(overlay.Report);
            Assert.That(overlay.Report.Primitives.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(overlay.Report.SourceClaimCount, Is.GreaterThan(40));
            Assert.That(overlay.Report.RejectedCandidateDescription, Does.Contain("Rejected"));

            bool sawHard = false;
            bool sawClearance = false;
            bool sawAccess = false;
            bool sawRoad = false;
            bool sawUnderground = false;
            for (int i = 0; i < overlay.Report.Primitives.Count; i++)
            {
                var primitive = overlay.Report.Primitives[i];
                sawHard |= (primitive.Semantics & ReservationSemantics.HardOccupancy) != 0;
                sawClearance |= (primitive.Semantics & ReservationSemantics.Clearance) != 0;
                sawAccess |= (primitive.Category & ReservationCategory.PublicAccess) != 0;
                sawRoad |= (primitive.Category & ReservationCategory.Road) != 0;
                sawUnderground |= (primitive.Category & ReservationCategory.Underground) != 0;
            }
            Assert.True(sawHard && sawClearance && sawAccess && sawRoad && sawUnderground,
                "Gallery evidence must include hard, clearance, public-access, road, and underground claims.");

            Debug.Log(
                $"SPATIAL_RESERVATION_COST sourceClaims={overlay.Report.SourceClaimCount} " +
                $"buildTicks={overlay.Report.BuildStopwatchTicks} " +
                $"queryBuckets={overlay.Report.RejectedCandidateMetrics.BucketsVisited} " +
                $"queryCandidates={overlay.Report.RejectedCandidateMetrics.BroadPhaseCandidates} " +
                $"queryNarrowPhase={overlay.Report.RejectedCandidateMetrics.NarrowPhaseTests}");

            bool worldBound = false;
            bool geometryPublished = false;
            for (int frame = 0; frame < 900; frame++)
            {
                if (VoxelRenderBridge.TryGetWorld(out var world))
                {
                    worldBound = world.ProfileBlocks != null && world.ProfileBlocks.Count > 0;

                    var metrics = VoxelRenderBridge.SurfaceMetrics;
                    geometryPublished = metrics.SolidKnownChunks > 0 &&
                                        metrics.SolidResidentChunks > 0;
                    if (worldBound && geometryPublished)
                        yield break;
                }

                yield return null;
            }

            var finalMetrics = VoxelRenderBridge.SurfaceMetrics;
            Assert.True(worldBound,
                "Worldbuilding gallery never bound its production rendering world.");
            Assert.True(geometryPublished,
                $"Worldbuilding gallery never published resident geometry: " +
                $"known={finalMetrics.SolidKnownChunks}, " +
                $"resident={finalMetrics.SolidResidentChunks}, " +
                $"dirty={finalMetrics.SolidDirtyChunks}.");
        }
    }
}
