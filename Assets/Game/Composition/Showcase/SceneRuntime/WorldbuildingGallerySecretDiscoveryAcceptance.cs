using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Collision.Api;
using VoxelEngine.Rendering.Runtime;

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
        private const float BreakableRendererConvergenceTimeoutSeconds = 10f;

        private Transform _cameraTransform;
        private CharacterMotor _motor;
        private bool _pinCamera;
        private bool _rendererConvergenceFailed;
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
                "authored-breakable-boundary",
                requireSolidRendererConvergence: true);

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
                "natural-cave-approach",
                requireSolidRendererConvergence: false);

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
            string label,
            bool requireSolidRendererConvergence)
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
            if (requireSolidRendererConvergence)
            {
                yield return WaitForSolidRendererConvergence(label);
                if (_rendererConvergenceFailed) yield break;
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.25f);
            }

            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            UnityEngine.Debug.Log($"SECRET_DISCOVERY_ACCEPTANCE frame={label} position={_pinnedPosition} target={target}");
            yield return new WaitForSecondsRealtime(0.45f);
        }

        /// <summary>
        /// The Gallery player can finish its blocking bake/bootstrap before URP has built the
        /// camera's visible solid chunks. Capturing an underground clue during that cold interval
        /// photographs the world underside even though authoritative cave voxels are correct. For
        /// SceneIssue/offline evidence only, spend a larger share of the existing renderer budgets
        /// and wait for the production surface metrics to report no missing visible solid chunks.
        /// No geometry, camera placement, storage state or renderer implementation is substituted.
        /// </summary>
        private IEnumerator WaitForSolidRendererConvergence(string label)
        {
            double originalBuildBudgetMs = VoxelRenderBridge.SolidBuildBudgetMs;
            int originalUploadBudgetBytes = VoxelRenderBridge.SolidUploadBudgetBytes;
            double originalUploadBudgetMs = VoxelRenderBridge.SolidUploadBudgetMs;
            double originalDiscoveryBudgetMs = VoxelRenderBridge.SurfaceDiscoveryBudgetMs;
            double originalConvergenceScale = VoxelRenderBridge.SurfaceConvergenceBudgetScale;
            int initialPassCount = VoxelRenderBridge.SurfacePassRecordCount;
            float waited = 0f;
            int stableFrames = 0;
            _rendererConvergenceFailed = false;

            try
            {
                // VoxelRenderBridge explicitly reserves these knobs for loading/offline capture
                // convergence. Raise only the validation replay's spend; production defaults are
                // restored in finally even when convergence times out.
                VoxelRenderBridge.SolidBuildBudgetMs = Math.Max(originalBuildBudgetMs, 2.0);
                VoxelRenderBridge.SolidUploadBudgetBytes = Math.Max(originalUploadBudgetBytes, 8 * 1024 * 1024);
                VoxelRenderBridge.SolidUploadBudgetMs = Math.Max(originalUploadBudgetMs, 1.0);
                VoxelRenderBridge.SurfaceDiscoveryBudgetMs = Math.Max(originalDiscoveryBudgetMs, 0.75);
                VoxelRenderBridge.SurfaceConvergenceBudgetScale = Math.Max(originalConvergenceScale, 16.0);

                while (waited < BreakableRendererConvergenceTimeoutSeconds)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;

                    var metrics = VoxelRenderBridge.SurfaceMetrics;
                    bool renderedAfterPin = VoxelRenderBridge.SurfacePassRecordCount > initialPassCount;
                    bool completeVisibleSurface = renderedAfterPin
                        && metrics.VisibleSolidChunks > 0
                        && metrics.MissingVisibleSolidChunks == 0;
                    stableFrames = completeVisibleSurface ? stableFrames + 1 : 0;
                    if (stableFrames < 2) continue;

                    UnityEngine.Debug.Log(
                        $"SECRET_DISCOVERY_RENDER_CONVERGENCE result=PASS frame={label} " +
                        $"waited={waited:0.###} visible={metrics.VisibleSolidChunks} " +
                        $"missing={metrics.MissingVisibleSolidChunks} dirty={metrics.SolidDirtyChunks} " +
                        $"running={metrics.RunningGeometryJobs} pendingUploads={metrics.SolidMeshesAwaitingUpload}");
                    yield break;
                }

                var finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                _rendererConvergenceFailed = true;
                UnityEngine.Debug.LogError(
                    $"SECRET_DISCOVERY_RENDER_CONVERGENCE result=FAIL frame={label} " +
                    $"waited={waited:0.###} passes={VoxelRenderBridge.SurfacePassRecordCount - initialPassCount} " +
                    $"visible={finalMetrics.VisibleSolidChunks} missing={finalMetrics.MissingVisibleSolidChunks} " +
                    $"dirty={finalMetrics.SolidDirtyChunks} running={finalMetrics.RunningGeometryJobs} " +
                    $"pendingUploads={finalMetrics.SolidMeshesAwaitingUpload}");
            }
            finally
            {
                VoxelRenderBridge.SolidBuildBudgetMs = originalBuildBudgetMs;
                VoxelRenderBridge.SolidUploadBudgetBytes = originalUploadBudgetBytes;
                VoxelRenderBridge.SolidUploadBudgetMs = originalUploadBudgetMs;
                VoxelRenderBridge.SurfaceDiscoveryBudgetMs = originalDiscoveryBudgetMs;
                VoxelRenderBridge.SurfaceConvergenceBudgetScale = originalConvergenceScale;
            }
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
