using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>Unattended built-player evidence for capture-less Worldbuilding Gallery SceneIssues.</summary>
    public static class WorldbuildingGalleryAuditHarness
    {
        private const string SceneIssueArgument = "-voxel-scene-issue";
        private const string ScreenshotDirectoryArgument = "-voxel-screenshot-dir";
        private const string SpatialReservationFeatureId = "20260829-050529-000-WorldBuilderSpatialReservationSystem";
        private const int ViewsPerTown = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string issuePath = Argument(SceneIssueArgument);
            string screenshotDirectory = Argument(ScreenshotDirectoryArgument);
            if (string.IsNullOrEmpty(issuePath) || string.IsNullOrEmpty(screenshotDirectory)
                || !TryReadCaptureLessIssue(issuePath, out IssueRecord issue)) return;

            var root = new GameObject("Worldbuilding Gallery Audit Harness") { hideFlags = HideFlags.DontSave };
            Reporter reporter = root.AddComponent<Reporter>();
            reporter.ScreenshotDirectory = screenshotDirectory;
            reporter.CaptureSpatialReservationEvidence = string.Equals(
                issue.id,
                SpatialReservationFeatureId,
                StringComparison.Ordinal);
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log("TOWNARCH_AUDIT armed for capture-less SceneIssue validation.");
        }

        private static bool TryReadCaptureLessIssue(string path, out IssueRecord record)
        {
            record = null;
            try
            {
                record = JsonUtility.FromJson<IssueRecord>(File.ReadAllText(path));
                return record != null && record.captures != null && record.captures.Length == 0 &&
                       string.Equals(record.sceneName, "WorldbuildingGalleryShowcase", StringComparison.Ordinal);
            }
            catch (Exception error)
            {
                Debug.LogError($"TOWNARCH_AUDIT could not read SceneIssue: {error.Message}");
                return false;
            }
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }

        [Serializable] private sealed class IssueRecord { public string id; public string sceneName; public IssueFrame[] captures; }
        [Serializable] private sealed class IssueFrame { }

        private sealed class Reporter : MonoBehaviour
        {
            internal string ScreenshotDirectory;
            internal bool CaptureSpatialReservationEvidence;
            private bool _started;
            private bool _pinCamera;
            private bool _spatialReservationEvidencePassed;
            private Transform _cameraTransform;
            private Vector3 _pinnedPosition;
            private Quaternion _pinnedRotation;

            private void Update()
            {
                if (_started) return;
                WorldbuildingGalleryShowcase showcase = UnityEngine.Object.FindFirstObjectByType<WorldbuildingGalleryShowcase>();
                if (showcase == null) return;
                _started = true;
                StartCoroutine(Capture(showcase));
            }

            private void LateUpdate()
            {
                if (_pinCamera && _cameraTransform != null)
                    _cameraTransform.SetPositionAndRotation(_pinnedPosition, _pinnedRotation);
            }

            private IEnumerator Capture(WorldbuildingGalleryShowcase showcase)
            {
                FieldInfo worldField = typeof(WorldbuildingGalleryShowcase).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
                if (worldField == null)
                {
                    Debug.LogError("TOWNARCH_AUDIT result=FAIL reason=gallery-world-contract-unavailable");
                    yield break;
                }

                ShowcaseWorld world = null;
                float waitSeconds = 0f;
                while (world == null && waitSeconds < 20f)
                {
                    world = worldField.GetValue(showcase) as ShowcaseWorld;
                    if (world != null) break;
                    yield return null;
                    waitSeconds += Time.unscaledDeltaTime;
                }
                if (world == null)
                {
                    Debug.LogError("TOWNARCH_AUDIT result=FAIL reason=gallery-world-not-ready");
                    yield break;
                }
                if (!world.HasWorldbuildingGalleryTownArchitectureContent())
                {
                    Debug.LogError("TOWNARCH_AUDIT result=FAIL reason=town-content-missing");
                    yield break;
                }

                int expectedViews = world.WorldbuildingGalleryTownDistrictCount * ViewsPerTown;
                int totalStops = world.WorldbuildingGalleryTourStopCount;
                if (expectedViews <= 0 || totalStops < expectedViews)
                {
                    Debug.LogError($"TOWNARCH_AUDIT result=FAIL reason=tour-too-short stops={totalStops} expectedViews={expectedViews}");
                    yield break;
                }

                int firstTownStop = totalStops - expectedViews;
                string auditDirectory = Path.Combine(ScreenshotDirectory, "TownArchitectureAudit");
                Directory.CreateDirectory(auditDirectory);
                foreach (string stale in Directory.GetFiles(auditDirectory, "*.png")) File.Delete(stale);
                _cameraTransform = showcase.transform;

                yield return null;
                yield return new WaitForEndOfFrame();

                for (int stop = firstTownStop; stop < totalStops; stop++)
                {
                    float3 authoredPosition = world.WorldbuildingGalleryTourSpawnPosition(stop);
                    float3 authoredTarget = world.WorldbuildingGalleryTourLookTarget(stop);
                    world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(authoredPosition));

                    _pinnedPosition = new Vector3(authoredPosition.x, authoredPosition.y, authoredPosition.z);
                    Vector3 target = new Vector3(authoredTarget.x, authoredTarget.y, authoredTarget.z);
                    Vector3 direction = target - _pinnedPosition;
                    _pinnedRotation = direction.sqrMagnitude > 1e-6f
                        ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                        : _cameraTransform.rotation;
                    _pinCamera = true;

                    yield return null;
                    yield return new WaitForSecondsRealtime(0.85f);
                    yield return new WaitForEndOfFrame();

                    string stopName = world.WorldbuildingGalleryTourStopName(stop);
                    int frame = stop - firstTownStop + 1;
                    string path = Path.Combine(auditDirectory, $"{frame:00}-{Sanitize(stopName)}.png");
                    ScreenCapture.CaptureScreenshot(path);
                    Debug.Log($"TOWNARCH_AUDIT frame={frame}/{expectedViews} stop={stop + 1}/{totalStops} name={stopName} position={_pinnedPosition}");
                    yield return new WaitForSecondsRealtime(0.35f);
                }

                _pinCamera = false;
                yield return new WaitForSecondsRealtime(1f);
                int captured = Directory.Exists(auditDirectory) ? Directory.GetFiles(auditDirectory, "*.png").Length : 0;
                if (captured < expectedViews)
                {
                    Debug.LogError($"TOWNARCH_AUDIT result=FAIL captured={captured} expected={expectedViews}");
                    yield break;
                }

                if (CaptureSpatialReservationEvidence)
                {
                    yield return CaptureSpatialReservations(showcase);
                    if (!_spatialReservationEvidencePassed)
                    {
                        Debug.LogError("TOWNARCH_AUDIT result=FAIL reason=spatial-reservation-evidence");
                        yield break;
                    }
                }

                long allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                long reservedBytes = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
                long unusedReservedBytes = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong();
                Debug.Log(
                    $"TOWNARCH_COST allocatedMB={allocatedBytes / (1024f * 1024f):0.##} " +
                    $"reservedMB={reservedBytes / (1024f * 1024f):0.##} " +
                    $"unusedReservedMB={unusedReservedBytes / (1024f * 1024f):0.##} " +
                    $"residentRegions={world.RegionsGenerated} pendingRegions={world.PendingRegionLoads} " +
                    $"{showcase.DescribeFarTerrain()}");
                Debug.Log($"TOWNARCH_AUDIT result=PASS captured={captured} expected={expectedViews}");
            }

            private IEnumerator CaptureSpatialReservations(WorldbuildingGalleryShowcase showcase)
            {
                SpatialReservationGalleryOverlay overlay = showcase.GetComponent<SpatialReservationGalleryOverlay>();
                float waitSeconds = 0f;
                while ((overlay == null || overlay.Report == null) && waitSeconds < 5f)
                {
                    yield return null;
                    waitSeconds += Time.unscaledDeltaTime;
                    overlay = showcase.GetComponent<SpatialReservationGalleryOverlay>();
                }
                if (overlay == null || overlay.Report == null)
                {
                    Debug.LogError("SPATIAL_RESERVATION_AUDIT result=FAIL reason=overlay-unavailable");
                    yield break;
                }

                string directory = Path.Combine(ScreenshotDirectory, "SpatialReservationAudit");
                Directory.CreateDirectory(directory);
                foreach (string stale in Directory.GetFiles(directory, "*.png")) File.Delete(stale);

                overlay.SetVisible(true);
                yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                yield return new WaitForEndOfFrame();
                string path = Path.Combine(directory, "01-reservation-inspection.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSecondsRealtime(0.5f);
                overlay.SetVisible(false);

                int captured = Directory.GetFiles(directory, "*.png").Length;
                if (captured < 1)
                {
                    Debug.LogError("SPATIAL_RESERVATION_AUDIT result=FAIL reason=screenshot-missing");
                    yield break;
                }

                var report = overlay.Report;
                var metrics = report.RejectedCandidateMetrics;
                Debug.Log(
                    $"SPATIAL_RESERVATION_COST sourceClaims={report.SourceClaimCount} primitives={report.Primitives.Count + 1} " +
                    $"buildTicks={report.BuildStopwatchTicks} queryBuckets={metrics.BucketsVisited} " +
                    $"broadCandidates={metrics.BroadPhaseCandidates} narrowTests={metrics.NarrowPhaseTests}");
                Debug.Log(
                    $"SPATIAL_RESERVATION_AUDIT result=PASS rejected=visible underground=visible " +
                    $"surface=visible screenshot={path}");
                _spatialReservationEvidencePassed = true;
            }

            private static string Sanitize(string value)
            {
                if (string.IsNullOrEmpty(value)) return "unnamed";
                char[] invalid = Path.GetInvalidFileNameChars();
                for (int i = 0; i < invalid.Length; i++) value = value.Replace(invalid[i], '-');
                return value.Replace(' ', '-').Replace('—', '-').ToLowerInvariant();
            }
        }
    }
}
