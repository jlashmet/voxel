using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Collision.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final thin acceptance consumer for WorldBuilder secret discovery in the production
    /// Worldbuilding Gallery. It composes the reusable feature into the existing generated cave.
    /// Camera pinning and screenshots are enabled only for this SceneIssue's built-player replay.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class WorldbuildingGallerySecretDiscoveryAcceptance : MonoBehaviour
    {
        private const string GalleryScene = "WorldbuildingGalleryShowcase";
        private const string IssueId = "20260830-164351-000-WorldBuilderSecretDiscoveryClueGeneration";
        private const string SceneIssueArgument = "-voxel-scene-issue";
        private const string ScreenshotDirectoryArgument = "-voxel-screenshot-dir";

        private Transform _cameraTransform;
        private CharacterMotor _motor;
        private bool _pinCamera;
        private Vector3 _pinnedPosition;
        private Quaternion _pinnedRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, GalleryScene, StringComparison.Ordinal))
                return;

            var root = new GameObject("Worldbuilding Gallery Secret Discovery Acceptance")
            {
                hideFlags = HideFlags.DontSave,
            };
            root.AddComponent<WorldbuildingGallerySecretDiscoveryAcceptance>();
        }

        private void Start()
        {
            StartCoroutine(ComposeAndCapture());
        }

        private void LateUpdate()
        {
            if (!_pinCamera || _cameraTransform == null) return;
            _cameraTransform.SetPositionAndRotation(_pinnedPosition, _pinnedRotation);
            if (_motor == null) return;
            _motor.Position = _pinnedPosition - Vector3.up * _motor.EyeHeight;
            _motor.Velocity = Vector3.zero;
        }

        private IEnumerator ComposeAndCapture()
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            WorldbuildingGalleryShowcase showcase = null;
            float waited = 0f;
            while (showcase == null && waited < 30f)
            {
                showcase = FindFirstObjectByType<WorldbuildingGalleryShowcase>();
                if (showcase != null) break;
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            if (showcase == null)
            {
                UnityEngine.Debug.LogError("SECRET_DISCOVERY_ACCEPTANCE result=FAIL reason=gallery-controller-not-ready");
                yield break;
            }

            FieldInfo worldField = typeof(WorldbuildingGalleryShowcase).GetField("_world", privateInstance);
            FieldInfo motorField = typeof(WorldbuildingGalleryShowcase).GetField("_motor", privateInstance);
            ShowcaseWorld world = null;
            waited = 0f;
            while (world == null && waited < 30f)
            {
                world = worldField?.GetValue(showcase) as ShowcaseWorld;
                if (world != null) break;
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            if (world == null)
            {
                UnityEngine.Debug.LogError("SECRET_DISCOVERY_ACCEPTANCE result=FAIL reason=gallery-world-not-ready");
                yield break;
            }

            var stopwatch = Stopwatch.StartNew();
            world.EnsureWorldbuildingGallerySecretDiscoveryBlocking();
            stopwatch.Stop();
            if (!world.HasWorldbuildingGallerySecretDiscoveryContent)
            {
                UnityEngine.Debug.LogError("SECRET_DISCOVERY_ACCEPTANCE result=FAIL reason=secret-content-missing");
                yield break;
            }

            UnityEngine.Debug.Log(
                $"SECRET_DISCOVERY_ACCEPTANCE composedMs={stopwatch.Elapsed.TotalMilliseconds:0.###} " +
                $"boundaryClueVoxels={world.WorldbuildingGallerySecretBoundaryClueVoxels} " +
                $"naturalClueVoxels={world.WorldbuildingGalleryNaturalApproachClueVoxels}");

            string issuePath = Argument(SceneIssueArgument);
            string screenshotDirectory = Argument(ScreenshotDirectoryArgument);
            if (string.IsNullOrEmpty(issuePath) ||
                issuePath.IndexOf(IssueId, StringComparison.Ordinal) < 0 ||
                string.IsNullOrEmpty(screenshotDirectory))
                yield break;

            _cameraTransform = showcase.transform;
            _motor = motorField?.GetValue(showcase) as CharacterMotor;
            if (_motor == null)
            {
                UnityEngine.Debug.LogError("SECRET_DISCOVERY_ACCEPTANCE result=FAIL reason=gallery-motor-unavailable");
                yield break;
            }

            string directory = Path.Combine(screenshotDirectory, "SecretDiscoveryAudit");
            Directory.CreateDirectory(directory);
            foreach (string stale in Directory.GetFiles(directory, "*.png")) File.Delete(stale);

            Camera cameraComponent = showcase.GetComponent<Camera>();
            float originalFieldOfView = cameraComponent != null ? cameraComponent.fieldOfView : 60f;
            if (cameraComponent != null) cameraComponent.fieldOfView = 75f;

            // The Gallery helper sits at the far edge of the final 18-voxel segment. The previous
            // exact-SHA replay proved that this edge position can still land outside the reliably
            // carved interior when the preceding segment turns. Move only the acceptance camera
            // toward the authored barrier, retaining a gameplay-scale view while keeping the eye
            // well inside the terminal segment instead of changing cave or pocket topology.
            float3 breakablePosition = world.WorldbuildingGalleryBreakableSecretCameraPosition();
            float3 breakableTarget = world.WorldbuildingGalleryBreakableSecretLookTarget();
            breakablePosition = math.lerp(breakablePosition, breakableTarget, 0.35f);
            RequireMinimumFramingDistance(breakablePosition, breakableTarget, 1.1f, "authored-breakable-boundary");
            yield return CaptureView(
                world,
                breakablePosition,
                breakableTarget,
                Path.Combine(directory, "02-authored-breakable-boundary.png"),
                "authored-breakable-boundary");

            // The deterministic moss trail spans entrance.z-12 through entrance.z-64. The helper
            // camera sits at entrance.z-72, so +3.2 m targets the trail midpoint (entrance.z-40)
            // directly instead of looking past the cave mouth. Shift west and slightly upward to
            // keep the clue in a close oblique view without Gallery architecture occluding it.
            float3 naturalPosition = world.WorldbuildingGalleryNaturalSecretCameraPosition();
            float3 naturalReferenceTarget = world.WorldbuildingGalleryNaturalSecretLookTarget();
            naturalPosition += new float3(-4.5f, 1.2f, 0f);
            float3 naturalTarget = new float3(
                naturalPosition.x + 4.5f,
                naturalReferenceTarget.y,
                naturalPosition.z + 3.2f);
            RequireMinimumFramingDistance(naturalPosition, naturalTarget, 5f, "natural-cave-approach");
            yield return CaptureView(
                world,
                naturalPosition,
                naturalTarget,
                Path.Combine(directory, "01-natural-cave-approach.png"),
                "natural-cave-approach");

            if (cameraComponent != null) cameraComponent.fieldOfView = originalFieldOfView;
            _pinCamera = false;

            int captured = Directory.GetFiles(directory, "*.png").Length;
            long allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            UnityEngine.Debug.Log(
                $"SECRET_DISCOVERY_COST allocatedMB={allocated / (1024f * 1024f):0.##} " +
                $"residentRegions={world.RegionsGenerated} pendingRegions={world.PendingRegionLoads}");
            if (captured != 2)
                UnityEngine.Debug.LogError($"SECRET_DISCOVERY_ACCEPTANCE result=FAIL captured={captured} expected=2");
            else
                UnityEngine.Debug.Log("SECRET_DISCOVERY_ACCEPTANCE result=PASS captured=2 expected=2");
        }

        private IEnumerator CaptureView(
            ShowcaseWorld world,
            float3 authoredPosition,
            float3 authoredTarget,
            string path,
            string label)
        {
            _pinnedPosition = new Vector3(authoredPosition.x, authoredPosition.y, authoredPosition.z);
            Vector3 target = new Vector3(authoredTarget.x, authoredTarget.y, authoredTarget.z);
            Vector3 direction = target - _pinnedPosition;
            if (direction.sqrMagnitude <= 1e-6f)
                throw new InvalidOperationException("Secret-discovery acceptance camera has no look direction.");
            _pinnedRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _pinCamera = true;

            world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(authoredPosition));
            yield return null;
            yield return new WaitForSecondsRealtime(1.25f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            UnityEngine.Debug.Log($"SECRET_DISCOVERY_ACCEPTANCE frame={label} position={_pinnedPosition} target={target}");
            yield return new WaitForSecondsRealtime(0.45f);
        }

        private static void RequireMinimumFramingDistance(
            float3 position,
            float3 target,
            float minimumMetres,
            string label)
        {
            float distance = math.distance(position, target);
            if (distance < minimumMetres)
                throw new InvalidOperationException(
                    $"Secret-discovery acceptance framing collapsed for {label}: " +
                    $"distance={distance:0.###}m minimum={minimumMetres:0.###}m.");
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
    }
}
