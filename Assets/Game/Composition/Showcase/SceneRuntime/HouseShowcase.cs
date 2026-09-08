using System;
using System.Collections.Generic;
using Game.Composition.Materials;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Canonical production-path browser for procedural voxel architecture. Authoritative generation,
    /// materials, SDF presentation and occupancy remain in owning production modules; this component
    /// owns only semantic selection, reviewer controls, camera poses and renderer lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class HouseShowcase : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private const uint SeedStep = 0x9E3779B9u;
        private const int DistinctSeedSearchLimit = 64;

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private ArchitectureStyleProfileDescriptor[] _profiles = Array.Empty<ArchitectureStyleProfileDescriptor>();
        private ArchitectureProviderDescriptor[] _providers = Array.Empty<ArchitectureProviderDescriptor>();
        private ArchitectureCanonicalReviewDescriptor _canonicalReview;
        private ArchitectureGenerationParameters _parameters;
        private ArchitectureGenerationResult _result;
        private readonly HashSet<ulong> _seenRealizations = new HashSet<ulong>();
        private int _profileIndex;
        private int _providerIndex;
        private int _archetypeIndex;
        private uint _seed;
        private string _seedText = string.Empty;
        private string _status = "INITIALIZING";
        private float _moveSpeed = 9f;
        private bool _pointerLocked;
        private bool _built;
        private bool _captureAutomation;
        private float _captureStartedAt;
        private int _capturePhase;

        public bool IsBuilt => _built;
        public uint Seed => _seed;
        public int HouseIndex => _archetypeIndex;
        public int HouseCount => _providers.Length == 0 ? 0 : CurrentProviderDescriptor.ArchetypeKeys.Length;
        public string SelectedHouseName => HouseCount == 0 ? string.Empty : CurrentArchetypeKey;
        public string SelectedStyleProfileKey => _profiles.Length == 0 ? string.Empty : CurrentProfile.Key;
        public string SelectedProviderKey => _providers.Length == 0 ? string.Empty : CurrentProviderDescriptor.Key;
        public ArchitectureGenerationParameters Parameters => _parameters;
        public ArchitectureGenerationResult GenerationResult => _result;
        internal static Color ProductionSurfaceDebugTint => Color.white;

        private ArchitectureStyleProfileDescriptor CurrentProfile => _profiles[_profileIndex];
        private ArchitectureProviderDescriptor CurrentProviderDescriptor => _providers[_providerIndex];
        private string CurrentArchetypeKey => CurrentProviderDescriptor.ArchetypeKeys[_archetypeIndex];

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            _camera = GetComponent<Camera>();
            _profiles = ArchitectureRegistry.StyleProfiles();
            _providers = ArchitectureRegistry.ProviderDescriptors();
            if (_profiles.Length == 0 || _providers.Length == 0)
                throw new InvalidOperationException("HouseShowcase found no registered production architecture profiles/providers.");

            _canonicalReview = HouseShowcaseArchitectureReviewCatalog.EnsureRegistered();
            ConfigurePresentation();
            ApplyCanonicalSelection();

            _captureAutomation = IsPlayerCaptureHarness();
            _capturePhase = 0;
            _captureStartedAt = Time.unscaledTime;
            Rebuild();
            _seenRealizations.Clear();
            _seenRealizations.Add(_result.Identity.RealizationHash);

            if (_captureAutomation)
            {
                SetPointerLock(false);
                Debug.Log(
                    $"HOUSE_SHOWCASE_VALIDATION start profile={CurrentProfile.Key} provider={CurrentProviderDescriptor.Key} " +
                    $"archetype={CurrentArchetypeKey} seed={_seed} requestHash={_result.Identity.RequestHash:X16} " +
                    $"realizationHash={_result.Identity.RealizationHash:X16}");
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            SetPointerLock(false);
            ShutdownWorld();
        }

        private void Update()
        {
            if (!_built || _camera == null) return;
            if (!_captureAutomation)
                UpdateManualInput();

            if (RenderingComposition.TryGetSurfaceBuildStatus(out int known, out int dirty, out int resident, out long bytes))
            {
                _status = dirty == 0
                    ? $"READY {resident}/{known} {bytes / (1024f * 1024f):0.0} MB"
                    : $"MESHING {resident}/{known} dirty={dirty}";
            }

            UpdateCaptureAutomation();
        }

        private void UpdateManualInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.leftBracketKey.wasPressedThisFrame) SelectArchetype(-1);
                if (keyboard.rightBracketKey.wasPressedThisFrame) SelectArchetype(1);
                if (keyboard.pKey.wasPressedThisFrame) SelectProfile(1);
                if (keyboard.oKey.wasPressedThisFrame) SelectProvider(1);
                if (keyboard.rKey.wasPressedThisFrame) RebuildDeterministically();
                if (keyboard.nKey.wasPressedThisFrame) AdvanceToDistinctSeed();
                if (keyboard.digit1Key.wasPressedThisFrame) FrameExterior();
                if (keyboard.digit2Key.wasPressedThisFrame) FrameInterior();
                if (keyboard.escapeKey.wasPressedThisFrame) SetPointerLock(false);
            }

            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) SetPointerLock(true);
                if (mouse.rightButton.wasReleasedThisFrame) SetPointerLock(false);
                if (_pointerLocked)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    Vector3 euler = _camera.transform.eulerAngles;
                    float yaw = euler.y + delta.x * 0.08f;
                    float pitch = Mathf.Clamp(NormalizePitch(euler.x) - delta.y * 0.08f, -85f, 85f);
                    _camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                }
            }

            float horizontal = 0f;
            float vertical = 0f;
            float elevation = 0f;
            float boost = 1f;
            if (keyboard != null)
            {
                horizontal = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                vertical = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                elevation = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
                boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? 3f : 1f;
            }

            Vector3 local = new Vector3(horizontal, elevation, vertical);
            if (local.sqrMagnitude > 1f) local.Normalize();
            _camera.transform.position += _camera.transform.TransformDirection(local) *
                                          (_moveSpeed * boost * Time.unscaledDeltaTime);

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                _moveSpeed = Mathf.Clamp(_moveSpeed * Mathf.Exp(scroll * 0.001f), 2f, 40f);
            }
        }

        private void ApplyCanonicalSelection()
        {
            ArchitectureGenerationRequest request = _canonicalReview.Request;
            _profileIndex = FindProfileIndex(request.StyleProfileKey);
            _providerIndex = FindProviderIndex(request.ProviderKey);
            if (_profileIndex < 0 || _providerIndex < 0)
                throw new InvalidOperationException("Canonical HouseShowcase review references an unregistered profile/provider.");

            _archetypeIndex = FindArchetypeIndex(CurrentProviderDescriptor, request.ArchetypeKey);
            if (_archetypeIndex < 0)
                throw new InvalidOperationException("Canonical HouseShowcase review references an unregistered archetype.");

            _seed = request.Seed;
            _seedText = _seed.ToString();
            _parameters = request.Parameters;
        }

        private ArchitectureGenerationRequest CurrentRequest =>
            new ArchitectureGenerationRequest(
                CurrentProfile.Key,
                CurrentProviderDescriptor.Key,
                CurrentArchetypeKey,
                _seed,
                HouseShowcaseArchitectureReviewCatalog.StructureId,
                new int3(0, 16, 0),
                _parameters);

        private IArchitectureProvider CurrentProvider()
        {
            if (!ArchitectureRegistry.TryGetProvider(CurrentProviderDescriptor.Key, out IArchitectureProvider provider))
                throw new InvalidOperationException($"Architecture provider '{CurrentProviderDescriptor.Key}' disappeared from the registry.");
            return provider;
        }

        private void Rebuild()
        {
            ArchitectureGenerationRequest request = CurrentRequest;
            IArchitectureProvider provider = CurrentProvider();
            if (!provider.TryPlan(in request, out _, out string planningError))
                throw new InvalidOperationException($"Architecture request rejected: {planningError}");

            ShutdownWorld();
            _storage = VoxelEngineBootstrap.CreateStorage(16, 64_000);
            MaterialDefinition[] materials = GameMaterialComposition.SimulationDefinitions();
            for (int i = 0; i < materials.Length; i++)
            {
                MaterialDefinition material = materials[i];
                _storage.RegisterMaterial(
                    material.MaterialId,
                    material.Hardness,
                    material.DestructionClass,
                    material.DefaultSurfaceStyle,
                    material.AllowedCoatings);
            }

            IStructureAuthoringSession authoring = VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 8_000_000);
            if (!provider.TryAuthor(authoring, in request, out _result, out string authoringError))
            {
                ShutdownWorld();
                throw new InvalidOperationException($"Architecture production authoring failed: {authoringError}");
            }
            if (authoring.BudgetExceeded)
            {
                ShutdownWorld();
                throw new InvalidOperationException("HouseShowcase exceeded its production authoring budget.");
            }

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world,
                _storage.Changes,
                _seed,
                solidBuildBudgetMs: 12.0,
                waterBuildBudgetMs: 0.0,
                farFieldEnabled: false);

            _built = true;
            _seedText = _seed.ToString();
            _status = $"AUTHORED {authoring.TotalVoxelsWritten:N0} voxels";
            FrameExterior();
            Debug.Log(
                $"HOUSE_SHOWCASE_READY profile={CurrentProfile.Key} provider={CurrentProviderDescriptor.Key} " +
                $"archetype={CurrentArchetypeKey} seed={_seed} storeys={_result.Summary.StoreyCount} " +
                $"floorHeight={_result.Summary.FloorHeight} bounds={_result.Summary.Min}->{_result.Summary.MaxExclusive} " +
                $"requestHash={_result.Identity.RequestHash:X16} realizationHash={_result.Identity.RealizationHash:X16}");
        }

        private void RebuildDeterministically()
        {
            ArchitectureGenerationIdentity before = _result.Identity;
            Rebuild();
            if (before.IsWellFormed &&
                (before.RequestHash != _result.Identity.RequestHash || before.RealizationHash != _result.Identity.RealizationHash))
                throw new InvalidOperationException("Same-input HouseShowcase regeneration changed deterministic architecture identity.");
        }

        private void AdvanceToDistinctSeed()
        {
            IArchitectureProvider provider = CurrentProvider();
            uint baseline = _seed;
            for (uint delta = 1; delta <= DistinctSeedSearchLimit; delta++)
            {
                uint candidate = unchecked(baseline + delta * SeedStep);
                var request = new ArchitectureGenerationRequest(
                    CurrentProfile.Key,
                    CurrentProviderDescriptor.Key,
                    CurrentArchetypeKey,
                    candidate,
                    HouseShowcaseArchitectureReviewCatalog.StructureId,
                    new int3(0, 16, 0),
                    _parameters);
                if (!provider.TryPlan(in request, out ArchitectureGenerationResult probe, out _))
                    continue;
                if (_seenRealizations.Contains(probe.Identity.RealizationHash))
                    continue;

                _seed = candidate;
                Rebuild();
                _seenRealizations.Add(_result.Identity.RealizationHash);
                return;
            }

            throw new InvalidOperationException(
                $"Could not find a distinct {CurrentProviderDescriptor.Key}/{CurrentArchetypeKey} realization within {DistinctSeedSearchLimit} deterministic seeds.");
        }

        private void SelectProfile(int delta)
        {
            _profileIndex = Wrap(_profileIndex + delta, _profiles.Length);
            Rebuild();
        }

        private void SelectProvider(int delta)
        {
            _providerIndex = Wrap(_providerIndex + delta, _providers.Length);
            _archetypeIndex = 0;
            _parameters = default;
            Rebuild();
        }

        private void SelectArchetype(int delta)
        {
            _archetypeIndex = Wrap(_archetypeIndex + delta, CurrentProviderDescriptor.ArchetypeKeys.Length);
            _parameters = default;
            Rebuild();
        }

        private bool TryApplyParameters(ArchitectureGenerationParameters candidate)
        {
            var request = new ArchitectureGenerationRequest(
                CurrentProfile.Key,
                CurrentProviderDescriptor.Key,
                CurrentArchetypeKey,
                _seed,
                HouseShowcaseArchitectureReviewCatalog.StructureId,
                new int3(0, 16, 0),
                candidate);
            if (!CurrentProvider().TryPlan(in request, out _, out string error))
            {
                _status = "REJECTED: " + error;
                return false;
            }

            _parameters = candidate;
            Rebuild();
            return true;
        }

        private void ConfigurePresentation()
        {
            GameMaterialComposition.Install();
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetVoxelLodEnabled(false);
            RenderingComposition.SetSky(
                new Color(0.66f, 0.76f, 0.84f, 1f),
                new Color(0.30f, 0.48f, 0.66f, 1f));
            RenderingComposition.ConfigureEnvironment(
                ProductionSurfaceDebugTint,
                new Vector3(-0.48f, 0.80f, -0.35f).normalized,
                new Color(1.0f, 0.91f, 0.74f, 1f),
                new Color(0.45f, 0.53f, 0.60f, 1f));
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.60f, 0.69f, 0.75f, 1f);
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 240f;
            _camera.allowHDR = false;
        }

        private void FrameExterior()
        {
            if (_camera == null || !_result.IsWellFormed) return;
            if (TryApplyCanonicalPose(ArchitectureReviewPoseKind.Exterior)) return;

            ArchitectureRealizationSummary summary = _result.Summary;
            Vector3 min = ToWorld(summary.Min);
            Vector3 max = ToWorld(summary.MaxExclusive);
            Vector3 size = max - min;
            Vector3 focus = min + new Vector3(size.x * 0.48f, size.y * 0.42f, size.z * 0.45f);
            float distance = Mathf.Max(18f, Mathf.Max(size.x, size.z) * 1.75f);
            _camera.transform.position = new Vector3(
                min.x + size.x * 0.95f,
                min.y + size.y * 0.78f,
                min.z - distance);
            _camera.fieldOfView = 48f;
            _camera.transform.LookAt(focus);
        }

        private void FrameInterior()
        {
            if (_camera == null || !_result.IsWellFormed) return;
            if (TryApplyCanonicalPose(ArchitectureReviewPoseKind.Interior)) return;

            ArchitectureRealizationSummary summary = _result.Summary;
            Vector3 min = ToWorld(summary.Min);
            Vector3 max = ToWorld(summary.MaxExclusive);
            Vector3 size = max - min;
            float eyeY = min.y + Mathf.Clamp(summary.FloorHeight * VoxelSize * 0.55f, 1.5f, 2.1f);
            Vector3 position = new Vector3(min.x + size.x * 0.50f, eyeY, min.z + Mathf.Min(2.2f, size.z * 0.22f));
            Vector3 target = new Vector3(min.x + size.x * 0.50f, eyeY, min.z + size.z * 0.70f);
            _camera.transform.position = position;
            _camera.fieldOfView = 60f;
            _camera.transform.LookAt(target);
        }

        private bool TryApplyCanonicalPose(ArchitectureReviewPoseKind kind)
        {
            if (!MatchesCanonicalRequest()) return false;
            ArchitectureReviewPose[] poses = _canonicalReview.Poses;
            for (int i = 0; i < poses.Length; i++)
            {
                ArchitectureReviewPose pose = poses[i];
                if (pose.Kind != kind) continue;
                Vector3 origin = ToWorld(CurrentRequest.Origin);
                _camera.transform.position = origin + ToVector3(pose.EyeOffsetMeters);
                _camera.fieldOfView = pose.FieldOfView;
                _camera.transform.LookAt(origin + ToVector3(pose.LookAtOffsetMeters));
                return true;
            }
            return false;
        }

        private void LogReviewPair(ArchitectureReviewPoseKind kind, string captureName)
        {
            if (!MatchesCanonicalRequest())
                throw new InvalidOperationException("Reference comparison capture must use the canonical registered request.");
            if (_canonicalReview.References.Length == 0)
                throw new InvalidOperationException("Canonical architecture review has no reference image descriptor.");

            ArchitectureReviewPose pose = default;
            bool found = false;
            for (int i = 0; i < _canonicalReview.Poses.Length; i++)
            {
                if (_canonicalReview.Poses[i].Kind != kind) continue;
                pose = _canonicalReview.Poses[i];
                found = true;
                break;
            }
            if (!found)
                throw new InvalidOperationException($"Canonical architecture review has no {kind} pose.");

            ArchitectureReferenceImageDescriptor reference = _canonicalReview.References[0];
            var metadata = new ArchitectureReviewCaptureMetadata(
                _canonicalReview.Key,
                reference.Key,
                pose.Key,
                CurrentProfile.Key,
                CurrentProviderDescriptor.Key,
                CurrentArchetypeKey,
                _seed,
                _parameters,
                _result.Identity,
                captureName);
            if (!metadata.IsWellFormed)
                throw new InvalidOperationException("Architecture review capture metadata was not well formed.");

            Debug.Log(
                "HOUSE_SHOWCASE_REVIEW_PAIR " + metadata.ToDiagnosticString() +
                $" referenceAsset={reference.AssetPath} caption={reference.Caption}");
        }

        private bool MatchesCanonicalRequest()
        {
            ArchitectureGenerationRequest a = CurrentRequest;
            ArchitectureGenerationRequest b = _canonicalReview.Request;
            return string.Equals(a.StyleProfileKey, b.StyleProfileKey, StringComparison.Ordinal) &&
                   string.Equals(a.ProviderKey, b.ProviderKey, StringComparison.Ordinal) &&
                   string.Equals(a.ArchetypeKey, b.ArchetypeKey, StringComparison.Ordinal) &&
                   a.Seed == b.Seed && ParametersEqual(in a.Parameters, in b.Parameters);
        }

        private void ShutdownWorld()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _built = false;
        }

        private void SetPointerLock(bool locked)
        {
            _pointerLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void UpdateCaptureAutomation()
        {
            if (!_captureAutomation) return;
            float elapsed = Time.unscaledTime - _captureStartedAt;
            try
            {
                if (_capturePhase == 0 && elapsed >= 8f)
                {
                    FrameExterior();
                    LogReviewPair(ArchitectureReviewPoseKind.Exterior, "house-foundation-canonical-exterior");
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION canonical exterior seed={_seed} realizationHash={_result.Identity.RealizationHash:X16}");
                    _capturePhase = 1;
                }
                else if (_capturePhase == 1 && elapsed >= 16f)
                {
                    FrameInterior();
                    LogReviewPair(ArchitectureReviewPoseKind.Interior, "house-foundation-canonical-interior");
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION interior profile={CurrentProfile.Key} archetype={CurrentArchetypeKey} seed={_seed} storeys={_result.Summary.StoreyCount} stairs={_result.Summary.StairCount}");
                    _capturePhase = 2;
                }
                else if (_capturePhase == 2 && elapsed >= 24f)
                {
                    ArchitectureGenerationIdentity before = _result.Identity;
                    RebuildDeterministically();
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION deterministic requestHash={before.RequestHash:X16} realizationHash={before.RealizationHash:X16}");
                    _capturePhase = 3;
                }
                else if (_capturePhase == 3 && elapsed >= 32f)
                {
                    AdvanceToDistinctSeed();
                    FrameExterior();
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION seed-variant index=1 seed={_seed} realizationHash={_result.Identity.RealizationHash:X16} bounds={_result.Summary.Min}->{_result.Summary.MaxExclusive}");
                    _capturePhase = 4;
                }
                else if (_capturePhase == 4 && elapsed >= 40f)
                {
                    AdvanceToDistinctSeed();
                    FrameExterior();
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION seed-variant index=2 seed={_seed} realizationHash={_result.Identity.RealizationHash:X16} bounds={_result.Summary.Min}->{_result.Summary.MaxExclusive}");
                    _capturePhase = 5;
                }
                else if (_capturePhase == 5 && elapsed >= 48f)
                {
                    AdvanceToDistinctSeed();
                    FrameExterior();
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION seed-variant index=3 seed={_seed} realizationHash={_result.Identity.RealizationHash:X16} bounds={_result.Summary.Min}->{_result.Summary.MaxExclusive}");
                    _capturePhase = 6;
                }
                else if (_capturePhase == 6 && elapsed >= 56f)
                {
                    var configured = new ArchitectureGenerationParameters(
                        width: 136,
                        depth: 120,
                        storeyCount: 3,
                        floorHeight: 32,
                        roomCount: 6);
                    if (!TryApplyParameters(configured))
                        throw new InvalidOperationException("HouseShowcase automated configuration switch was rejected: " + _status);
                    FrameExterior();
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION config-switched width=136 depth=120 storeys=3 floorHeight=32 rooms=6 realizationHash={_result.Identity.RealizationHash:X16}");
                    _capturePhase = 7;
                }
                else if (_capturePhase == 7 && elapsed >= 64f)
                {
                    int hightown = FindProfileIndex("hightown");
                    if (hightown < 0)
                        throw new InvalidOperationException("HouseShowcase automated profile switch could not resolve 'hightown'.");
                    _profileIndex = hightown;
                    Rebuild();
                    FrameInterior();
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION profile-switched profile={CurrentProfile.Key} provider={CurrentProviderDescriptor.Key} archetype={CurrentArchetypeKey} seed={_seed}");
                    _capturePhase = 8;
                }
                else if (_capturePhase == 8 && elapsed >= 72f)
                {
                    ApplyCanonicalSelection();
                    Rebuild();
                    FrameExterior();
                    LogReviewPair(ArchitectureReviewPoseKind.Exterior, "house-foundation-final-reference-comparison");
                    Debug.Log($"HOUSE_SHOWCASE_VALIDATION complete profile={CurrentProfile.Key} provider={CurrentProviderDescriptor.Key} archetype={CurrentArchetypeKey} seed={_seed} variants={_seenRealizations.Count} reference={HouseShowcaseArchitectureReviewCatalog.ReferenceAssetPath}");
                    _capturePhase = 9;
                }
            }
            catch (Exception ex)
            {
                _capturePhase = int.MaxValue;
                Debug.LogError("HOUSE_SHOWCASE_VALIDATION failure: " + ex.Message);
                throw;
            }
        }

        private static bool IsPlayerCaptureHarness()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], "-voxel-screenshot-dir", StringComparison.Ordinal) ||
                    string.Equals(args[i], "-voxel-scene-issue", StringComparison.Ordinal))
                    return true;
            return false;
        }

        private void OnGUI()
        {
            if (_profiles.Length == 0 || _providers.Length == 0) return;
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            small.normal.textColor = Color.white;

            GUI.Box(new Rect(12f, 12f, 476f, 360f), GUIContent.none);
            GUI.Label(new Rect(24f, 20f, 448f, 28f), "HouseShowcase — Procedural Architecture", title);
            GUI.Label(new Rect(24f, 48f, 448f, 22f), $"{_status}", small);

            DrawSelectorRow(24f, 76f, "Profile", CurrentProfile.DisplayName, () => SelectProfile(-1), () => SelectProfile(1));
            DrawSelectorRow(24f, 106f, "Provider", CurrentProviderDescriptor.DisplayName, () => SelectProvider(-1), () => SelectProvider(1));
            DrawSelectorRow(24f, 136f, "Archetype", CurrentArchetypeKey, () => SelectArchetype(-1), () => SelectArchetype(1));

            GUI.Label(new Rect(24f, 170f, 55f, 22f), "Seed", small);
            _seedText = GUI.TextField(new Rect(84f, 168f, 150f, 24f), _seedText);
            if (GUI.Button(new Rect(240f, 168f, 64f, 24f), "Apply"))
            {
                if (uint.TryParse(_seedText, out uint parsed))
                {
                    _seed = parsed;
                    Rebuild();
                }
                else _status = "REJECTED: seed must be an unsigned integer";
            }
            if (GUI.Button(new Rect(310f, 168f, 72f, 24f), "Next seed")) AdvanceToDistinctSeed();
            if (GUI.Button(new Rect(388f, 168f, 82f, 24f), "Canonical"))
            {
                ApplyCanonicalSelection();
                Rebuild();
            }

            float y = 202f;
            DrawParameterRow(ref y, "Width", _parameters.Width, 8, 64, 256, 128, ArchitectureParameterSupport.Footprint, ParameterKind.Width);
            DrawParameterRow(ref y, "Depth", _parameters.Depth, 8, 64, 256, 128, ArchitectureParameterSupport.Footprint, ParameterKind.Depth);
            DrawParameterRow(ref y, "Storeys", _parameters.StoreyCount, 1, 1, 4, 2, ArchitectureParameterSupport.StoreyCount, ParameterKind.Storeys);
            DrawParameterRow(ref y, "Floor height", _parameters.FloorHeight, 2, 24, 48, 30, ArchitectureParameterSupport.FloorHeight, ParameterKind.FloorHeight);
            DrawParameterRow(ref y, "Rooms", _parameters.RoomCount, 1, 1, 16, 5, ArchitectureParameterSupport.RoomCount, ParameterKind.Rooms);

            GUI.Label(new Rect(24f, 334f, 448f, 22f),
                "[/] archetype • P profile • O provider • R same-input rebuild • N next seed • 1 exterior • 2 interior • WASD/QE + RMB inspect",
                small);
        }

        private void DrawSelectorRow(float x, float y, string label, string value, Action previous, Action next)
        {
            GUI.Label(new Rect(x, y + 2f, 72f, 22f), label);
            if (GUI.Button(new Rect(x + 74f, y, 28f, 24f), "<")) previous();
            GUI.Label(new Rect(x + 108f, y + 2f, 286f, 22f), value);
            if (GUI.Button(new Rect(x + 398f, y, 28f, 24f), ">")) next();
        }

        private enum ParameterKind : byte { Width, Depth, Storeys, FloorHeight, Rooms }

        private void DrawParameterRow(
            ref float y,
            string label,
            int value,
            int step,
            int min,
            int max,
            int defaultValue,
            ArchitectureParameterSupport support,
            ParameterKind kind)
        {
            if ((CurrentProviderDescriptor.SupportedParameters & support) == 0) return;
            GUI.Label(new Rect(24f, y + 2f, 104f, 22f), label);
            if (GUI.Button(new Rect(132f, y, 30f, 24f), "-"))
                AdjustParameter(kind, value == 0 ? defaultValue - step : Mathf.Max(min, value - step));
            GUI.Label(new Rect(170f, y + 2f, 90f, 22f), value == 0 ? "Auto" : value.ToString());
            if (GUI.Button(new Rect(264f, y, 30f, 24f), "+"))
                AdjustParameter(kind, value == 0 ? defaultValue : Mathf.Min(max, value + step));
            if (GUI.Button(new Rect(302f, y, 54f, 24f), "Auto")) AdjustParameter(kind, 0);
            y += 26f;
        }

        private void AdjustParameter(ParameterKind kind, int value)
        {
            ArchitectureGenerationParameters p = _parameters;
            var candidate = new ArchitectureGenerationParameters(
                width: kind == ParameterKind.Width ? value : p.Width,
                depth: kind == ParameterKind.Depth ? value : p.Depth,
                storeyCount: kind == ParameterKind.Storeys ? value : p.StoreyCount,
                floorHeight: kind == ParameterKind.FloorHeight ? value : p.FloorHeight,
                roomCount: kind == ParameterKind.Rooms ? value : p.RoomCount,
                roofForm: p.RoofForm,
                foundation: p.Foundation,
                openings: p.Openings,
                trimLevel: p.TrimLevel,
                detailLevel: p.DetailLevel);
            TryApplyParameters(candidate);
        }

        private int FindProfileIndex(string key)
        {
            for (int i = 0; i < _profiles.Length; i++)
                if (string.Equals(_profiles[i].Key, key, StringComparison.Ordinal)) return i;
            return -1;
        }

        private int FindProviderIndex(string key)
        {
            for (int i = 0; i < _providers.Length; i++)
                if (string.Equals(_providers[i].Key, key, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static int FindArchetypeIndex(ArchitectureProviderDescriptor provider, string key)
        {
            for (int i = 0; i < provider.ArchetypeKeys.Length; i++)
                if (string.Equals(provider.ArchetypeKeys[i], key, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0) return 0;
            int result = value % count;
            return result < 0 ? result + count : result;
        }

        private static bool ParametersEqual(in ArchitectureGenerationParameters a, in ArchitectureGenerationParameters b) =>
            a.Width == b.Width && a.Depth == b.Depth && a.StoreyCount == b.StoreyCount &&
            a.FloorHeight == b.FloorHeight && a.RoomCount == b.RoomCount &&
            a.RoofForm == b.RoofForm && a.Foundation == b.Foundation && a.Openings == b.Openings &&
            a.TrimLevel == b.TrimLevel && a.DetailLevel == b.DetailLevel;

        private static Vector3 ToWorld(int3 voxels) => new Vector3(voxels.x * VoxelSize, voxels.y * VoxelSize, voxels.z * VoxelSize);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static float NormalizePitch(float degrees) => degrees > 180f ? degrees - 360f : degrees;
    }
}
