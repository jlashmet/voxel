using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Unattended built-player evidence for capture-less Worldbuilding Gallery feature issues.
    /// Normal play is untouched: the harness only arms when the canonical SceneIssue argument is
    /// present, that issue has no recorded captures, and the active scene owns the gallery driver.
    /// It reuses the production tour positions/look targets rather than inventing validation poses.
    /// </summary>
    public static class WorldbuildingGalleryAuditHarness
    {
        private const string SceneIssueArgument = "-voxel-scene-issue";
        private const string ScreenshotDirectoryArgument = "-voxel-screenshot-dir";
        private const int TownAuditViewCount = 18;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string issuePath = Argument(SceneIssueArgument);
            string screenshotDirectory = Argument(ScreenshotDirectoryArgument);
            if (string.IsNullOrEmpty(issuePath) || string.IsNullOrEmpty(screenshotDirectory))
                return;
            if (!IsCaptureLessIssue(issuePath))
                return;

            var root = new GameObject("Worldbuilding Gallery Audit Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            Reporter reporter = root.AddComponent<Reporter>();
            reporter.ScreenshotDirectory = screenshotDirectory;
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log("TOWNARCH_AUDIT armed for capture-less SceneIssue validation.");
        }

        private static bool IsCaptureLessIssue(string path)
        {
            try
            {
                IssueRecord record = JsonUtility.FromJson<IssueRecord>(File.ReadAllText(path));
                return record != null &&
                       record.captures != null &&
                       record.captures.Length == 0 &&
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
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }

        [Serializable]
        private sealed class IssueRecord
        {
            public string sceneName;
            public IssueFrame[] captures;
        }

        [Serializable]
        private sealed class IssueFrame
        {
        }

        private sealed class Reporter : MonoBehaviour
        {
            internal string ScreenshotDirectory;
            private bool _started;
            private bool _pinCamera;
            private Transform _cameraTransform;
            private Vector3 _pinnedPosition;
            private Quaternion _pinnedRotation;

            private void Update()
            {
                if (_started) return;
                WorldbuildingGalleryShowcase showcase =
                    UnityEngine.Object.FindFirstObjectByType<WorldbuildingGalleryShowcase>();
                if (showcase == null) return;

                _started = true;
                StartCoroutine(Capture(showcase));
            }

            private void LateUpdate()
            {
                if (!_pinCamera || _cameraTransform == null) return;
                _cameraTransform.SetPositionAndRotation(_pinnedPosition, _pinnedRotation);
            }

            private IEnumerator Capture(WorldbuildingGalleryShowcase showcase)
            {
                FieldInfo worldField = typeof(WorldbuildingGalleryShowcase).GetField(
                    "_world", BindingFlags.Instance | BindingFlags.NonPublic);
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

                int totalStops = world.WorldbuildingGalleryTourStopCount;
                if (totalStops < TownAuditViewCount)
                {
                    Debug.LogError($"TOWNARCH_AUDIT result=FAIL reason=tour-too-short stops={totalStops}");
                    yield break;
                }

                int firstTownStop = totalStops - TownAuditViewCount;
                string auditDirectory = Path.Combine(ScreenshotDirectory, "TownArchitectureAudit");
                Directory.CreateDirectory(auditDirectory);
                _cameraTransform = showcase.transform;

                // Give the baked/generated world one presented frame before the first evidence pose.
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
                    string fileName = $"{stop - firstTownStop + 1:00}-{Sanitize(stopName)}.png";
                    string path = Path.Combine(auditDirectory, fileName);
                    ScreenCapture.CaptureScreenshot(path);
                    Debug.Log($"TOWNARCH_AUDIT frame={stop - firstTownStop + 1}/{TownAuditViewCount} " +
                              $"stop={stop + 1}/{totalStops} name={stopName} position={_pinnedPosition}");

                    // CaptureScreenshot writes asynchronously; allow the frame to leave the render
                    // pipeline before moving the production camera to the next deterministic view.
                    yield return new WaitForSecondsRealtime(0.35f);
                }

                _pinCamera = false;
                yield return new WaitForSecondsRealtime(1f);
                int captured = Directory.Exists(auditDirectory)
                    ? Directory.GetFiles(auditDirectory, "*.png").Length
                    : 0;
                if (captured < TownAuditViewCount)
                {
                    Debug.LogError($"TOWNARCH_AUDIT result=FAIL captured={captured} expected={TownAuditViewCount}");
                    yield break;
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
                Debug.Log($"TOWNARCH_AUDIT result=PASS captured={captured} expected={TownAuditViewCount}");
            }

            private static string Sanitize(string value)
            {
                if (string.IsNullOrEmpty(value)) return "unnamed";
                char[] invalid = Path.GetInvalidFileNameChars();
                for (int i = 0; i < invalid.Length; i++)
                    value = value.Replace(invalid[i], '-');
                return value.Replace(' ', '-').Replace('—', '-').ToLowerInvariant();
            }
        }
    }
}
