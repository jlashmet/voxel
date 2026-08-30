using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    /// <summary>Unattended built-player evidence for capture-less Worldbuilding Gallery SceneIssues.</summary>
    public static class WorldbuildingGalleryAuditHarness
    {
        private const string SceneIssueArgument = "-voxel-scene-issue";
        private const string ScreenshotDirectoryArgument = "-voxel-screenshot-dir";
        private const string StructuralCompositionIssueId = "20260829-034505-000-WorldBuilderTypedStructuralSocketComposition";
        private const int ViewsPerTown = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string issuePath = Argument(SceneIssueArgument);
            string screenshotDirectory = Argument(ScreenshotDirectoryArgument);
            if (string.IsNullOrEmpty(issuePath) || string.IsNullOrEmpty(screenshotDirectory)) return;

            IssueRecord issue = ReadIssue(issuePath);
            if (!IsCaptureLessGalleryIssue(issue)) return;

            var root = new GameObject("Worldbuilding Gallery Audit Harness") { hideFlags = HideFlags.DontSave };
            Reporter reporter = root.AddComponent<Reporter>();
            reporter.ScreenshotDirectory = screenshotDirectory;
            reporter.StructuralCompositionAudit = string.Equals(
                issue.id,
                StructuralCompositionIssueId,
                StringComparison.Ordinal);
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log($"TOWNARCH_AUDIT armed for capture-less SceneIssue validation structural={reporter.StructuralCompositionAudit}.");
        }

        private static IssueRecord ReadIssue(string path)
        {
            try
            {
                return JsonUtility.FromJson<IssueRecord>(File.ReadAllText(path));
            }
            catch (Exception error)
            {
                Debug.LogError($"TOWNARCH_AUDIT could not read SceneIssue: {error.Message}");
                return null;
            }
        }

        private static bool IsCaptureLessGalleryIssue(IssueRecord record) =>
            record != null && record.captures != null && record.captures.Length == 0 &&
            string.Equals(record.sceneName, "WorldbuildingGalleryShowcase", StringComparison.Ordinal);

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }

        [Serializable] private sealed class IssueRecord { public string id; public string sceneName; public IssueFrame[] captures; }
        [Serializable] private sealed class IssueFrame { }

        private readonly struct StructuralFrameSpec
        {
            public readonly string Name;
            public readonly int Proof;
            public readonly Vector3 Target01;
            public readonly Vector3 CameraDirection;
            public readonly float DistanceScale;
            public readonly float MinimumDistance;

            public StructuralFrameSpec(string name, int proof, Vector3 target01,
                Vector3 cameraDirection, float distanceScale, float minimumDistance)
            {
                Name = name;
                Proof = proof;
                Target01 = target01;
                CameraDirection = cameraDirection;
                DistanceScale = distanceScale;
                MinimumDistance = minimumDistance;
            }
        }

        private static readonly StructuralFrameSpec[] s_StructuralFrames =
        {
            // Low diagonal views keep the bridge against its authored river/bank context rather than
            // the distant far-field, while still fitting the full 122 m span in one establishing frame.
            new("bridge-wide", 0, new Vector3(0.50f, 0.78f, 0.50f), new Vector3(-0.50f, -0.05f, -0.50f), 0.50f, 58f),
            new("bridge-deck-junction", 0, new Vector3(0.16f, 0.86f, 0.50f), new Vector3(-0.10f, 0.02f, -0.75f), 0.12f, 15f),
            new("castle-wide", 1, new Vector3(0.50f, 0.55f, 0.50f), new Vector3(0.55f, 0.12f, -0.75f), 0.68f, 50f),
            new("castle-gate", 1, new Vector3(0.50f, 0.33f, 0.12f), new Vector3(0f, 0.02f, -1f), 0.18f, 16f),
            // The cliff views deliberately look uphill from below the upper landing; this makes the
            // terrain-supported level change and ramp/pedestal relationship legible in 2D evidence.
            new("cliff-wide", 2, new Vector3(0.62f, 0.70f, 0.50f), new Vector3(-0.65f, -0.15f, -0.45f), 0.65f, 38f),
            new("cliff-ramp-junction", 2, new Vector3(0.54f, 0.63f, 0.50f), new Vector3(-0.42f, -0.06f, -0.80f), 0.27f, 17f),
            new("facade-civic", 3, new Vector3(0.22f, 0.52f, 0.84f), new Vector3(-0.08f, 0.02f, 1f), 0.19f, 18f),
            new("facade-ornate", 3, new Vector3(0.78f, 0.52f, 0.84f), new Vector3(0.08f, 0.02f, 1f), 0.19f, 18f),
        };

        private sealed class Reporter : MonoBehaviour
        {
            internal string ScreenshotDirectory;
            internal bool StructuralCompositionAudit;
            private bool _started;
            private bool _pinCamera;
            private bool _structuralAuditPassed;
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

                if (StructuralCompositionAudit)
                {
                    yield return CaptureStructural(world);
                    if (!_structuralAuditPassed) yield break;
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

            private IEnumerator CaptureStructural(ShowcaseWorld world)
            {
                _structuralAuditPassed = false;
                // Normal gallery startup authors the same refined authoritative-voxel pass. Requiring
                // it here also keeps evidence robust if the harness runs against an alternate startup.
                world.EnsureWorldbuildingGalleryStructuralRefinementBlocking();
                if (!world.HasWorldbuildingGalleryStructuralCompositionContent())
                {
                    Debug.LogError("STRUCTURAL_AUDIT result=FAIL reason=structural-content-missing");
                    yield break;
                }

                int totalChildren = 0;
                int totalPrimitives = 0;
                int totalVoxelBudget = 0;
                int totalRegions = 0;
                int totalInstances = 0;
                int totalVoxelsWritten = 0;

                for (int proof = 0; proof < ShowcaseWorld.WorldbuildingGalleryStructuralProofCaseCount; proof++)
                {
                    ShowcaseWorld.GalleryStructuralProofMetrics metrics =
                        world.WorldbuildingGalleryStructuralProofMetrics(proof);
                    if (metrics.Result != StructuralCompositionResult.Ok || metrics.ChildCount <= 0 ||
                        metrics.PrimitiveCost <= 0 || metrics.VoxelCost <= 0 || metrics.RegionsVisited <= 0 ||
                        metrics.InstancesRasterised <= 0 || metrics.VoxelsWritten <= 0 || metrics.GraphHash == 0UL)
                    {
                        Debug.LogError($"STRUCTURAL_AUDIT result=FAIL reason=invalid-proof-metrics proof={proof} " +
                            $"name={metrics.Name} result={metrics.Result} children={metrics.ChildCount} " +
                            $"primitives={metrics.PrimitiveCost} voxelBudget={metrics.VoxelCost} " +
                            $"regions={metrics.RegionsVisited} instances={metrics.InstancesRasterised} " +
                            $"voxelsWritten={metrics.VoxelsWritten} graph=0x{metrics.GraphHash:X16}");
                        yield break;
                    }

                    double planningMs = world.AuditWorldbuildingGalleryStructuralPlanningMilliseconds(proof);
                    totalChildren += metrics.ChildCount;
                    totalPrimitives += metrics.PrimitiveCost;
                    totalVoxelBudget += metrics.VoxelCost;
                    totalRegions += metrics.RegionsVisited;
                    totalInstances += metrics.InstancesRasterised;
                    totalVoxelsWritten += metrics.VoxelsWritten;
                    Debug.Log($"STRUCTURAL_COST proof={proof} name={metrics.Name} planningMs={planningMs:0.###} " +
                        $"children={metrics.ChildCount} primitives={metrics.PrimitiveCost} voxelBudget={metrics.VoxelCost} " +
                        $"regions={metrics.RegionsVisited} instances={metrics.InstancesRasterised} " +
                        $"voxelsWritten={metrics.VoxelsWritten} graph=0x{metrics.GraphHash:X16} " +
                        $"bounds={metrics.BoundsMin}..{metrics.BoundsMax}");
                }

                StructuralAttachmentRejectReason bridgeOrientation =
                    world.AuditWorldbuildingGalleryStructuralBridgeOrientationReject();
                StructuralAttachmentRejectReason castleSemantic =
                    world.AuditWorldbuildingGalleryStructuralCastleSemanticReject();
                StructuralAttachmentRejectReason bridgeSemantic =
                    world.WorldbuildingGalleryStructuralBridgeNegativeReject;
                StructuralAttachmentRejectReason cliffSupport =
                    world.WorldbuildingGalleryStructuralCliffNegativeReject;
                if (bridgeOrientation != StructuralAttachmentRejectReason.OrientationMismatch ||
                    castleSemantic != StructuralAttachmentRejectReason.IncompatibleRoleOrTags ||
                    bridgeSemantic == StructuralAttachmentRejectReason.None ||
                    cliffSupport != StructuralAttachmentRejectReason.MissingTerrainSupport)
                {
                    Debug.LogError($"STRUCTURAL_AUDIT result=FAIL reason=negative-contract " +
                        $"bridgeOrientation={bridgeOrientation} bridgeSemantic={bridgeSemantic} " +
                        $"castleSemantic={castleSemantic} cliffSupport={cliffSupport}");
                    yield break;
                }

                if (world.WorldbuildingGalleryStructuralBridgeTerrainRelief <= 0 ||
                    world.WorldbuildingGalleryStructuralArchPrimitiveBaseline <= 0)
                {
                    Debug.LogError($"STRUCTURAL_AUDIT result=FAIL reason=site-or-detail-contract " +
                        $"bridgeRelief={world.WorldbuildingGalleryStructuralBridgeTerrainRelief} " +
                        $"archBaseline={world.WorldbuildingGalleryStructuralArchPrimitiveBaseline}");
                    yield break;
                }

                for (int route = 0; route < ShowcaseWorld.WorldbuildingGalleryStructuralTraversalCount; route++)
                {
                    ShowcaseWorld.GalleryStructuralTraversalReport traversal =
                        world.AuditWorldbuildingGalleryStructuralTraversal(route);
                    Debug.Log($"STRUCTURAL_TRAVERSAL route={route} reached={traversal.Reached} steps={traversal.Steps} " +
                        $"startDistance={traversal.StartDistanceMetres:0.###}m endDistance={traversal.EndDistanceMetres:0.###}m " +
                        $"finalFeet={traversal.FinalFeetPosition}");
                    if (!traversal.Reached || traversal.EndDistanceMetres >= traversal.StartDistanceMetres)
                    {
                        Debug.LogError($"STRUCTURAL_AUDIT result=FAIL reason=character-motor-traversal route={route}");
                        yield break;
                    }
                }

                string structuralDirectory = Path.Combine(ScreenshotDirectory, "StructuralCompositionAudit");
                Directory.CreateDirectory(structuralDirectory);
                foreach (string stale in Directory.GetFiles(structuralDirectory, "*.png")) File.Delete(stale);

                for (int frame = 0; frame < s_StructuralFrames.Length; frame++)
                {
                    StructuralFrameSpec spec = s_StructuralFrames[frame];
                    ShowcaseWorld.GalleryStructuralProofMetrics metrics =
                        world.WorldbuildingGalleryStructuralProofMetrics(spec.Proof);
                    Vector3 min = new Vector3(metrics.BoundsMin.x, metrics.BoundsMin.y, metrics.BoundsMin.z) * ShowcaseWorld.VoxelSize;
                    Vector3 max = new Vector3(metrics.BoundsMax.x, metrics.BoundsMax.y, metrics.BoundsMax.z) * ShowcaseWorld.VoxelSize;
                    Vector3 span = max - min;
                    Vector3 target = new Vector3(
                        Mathf.Lerp(min.x, max.x, spec.Target01.x),
                        Mathf.Lerp(min.y, max.y, spec.Target01.y),
                        Mathf.Lerp(min.z, max.z, spec.Target01.z));
                    float horizontalSpan = Mathf.Max(span.x, span.z);
                    float distance = Mathf.Max(spec.MinimumDistance, horizontalSpan * spec.DistanceScale);
                    Vector3 cameraDirection = spec.CameraDirection.normalized;
                    _pinnedPosition = target + cameraDirection * distance;
                    Vector3 direction = target - _pinnedPosition;
                    _pinnedRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    _pinCamera = true;

                    // Load a bounded strip through the actual view, not just its endpoints. This
                    // prevents a valid structure from being judged through missing neighbouring
                    // near-terrain while avoiding a second streaming radius for evidence capture.
                    world.PrepareWorldbuildingGalleryStructuralEvidence(_pinnedPosition, target);
                    yield return null;
                    yield return new WaitForSecondsRealtime(1.0f);
                    yield return new WaitForEndOfFrame();

                    string path = Path.Combine(structuralDirectory, $"{frame + 1:00}-{spec.Name}.png");
                    ScreenCapture.CaptureScreenshot(path);
                    Debug.Log($"STRUCTURAL_AUDIT frame={frame + 1}/{s_StructuralFrames.Length} " +
                        $"name={spec.Name} proof={spec.Proof} camera={_pinnedPosition} target={target}");
                    yield return new WaitForSecondsRealtime(0.35f);
                }

                _pinCamera = false;
                yield return new WaitForSecondsRealtime(1f);
                int captured = Directory.Exists(structuralDirectory)
                    ? Directory.GetFiles(structuralDirectory, "*.png").Length : 0;
                if (captured < s_StructuralFrames.Length)
                {
                    Debug.LogError($"STRUCTURAL_AUDIT result=FAIL captured={captured} expected={s_StructuralFrames.Length}");
                    yield break;
                }

                long allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                Debug.Log($"STRUCTURAL_COST totalAuthoringMs={world.WorldbuildingGalleryStructuralAuthoringMilliseconds:0.###} " +
                    $"presentationMs={world.WorldbuildingGalleryStructuralPresentationAuthoringMilliseconds:0.###} " +
                    $"children={totalChildren} primitives={totalPrimitives} voxelBudget={totalVoxelBudget} " +
                    $"regions={totalRegions} instances={totalInstances} voxelsWritten={totalVoxelsWritten} " +
                    $"residentRegions={world.RegionsGenerated} allocatedMB={allocatedBytes / (1024f * 1024f):0.##} " +
                    $"renderProxyRegions={totalRegions} bridgeRelief={world.WorldbuildingGalleryStructuralBridgeTerrainRelief} " +
                    $"archBaselinePrimitives={world.WorldbuildingGalleryStructuralArchPrimitiveBaseline}");
                Debug.Log($"STRUCTURAL_AUDIT result=PASS captured={captured} traversals={ShowcaseWorld.WorldbuildingGalleryStructuralTraversalCount} " +
                    $"bridgeOrientationReject={bridgeOrientation} castleSemanticReject={castleSemantic} cliffSupportReject={cliffSupport}");
                _structuralAuditPassed = true;
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
