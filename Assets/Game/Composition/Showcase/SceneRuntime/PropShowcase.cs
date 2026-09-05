using System;
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
    /// Production-path browser for the canonical decoration/world-object catalogue. This class owns
    /// only UI, selection, neutral support surfaces, camera framing, and lifecycle. Content identity,
    /// geometry, materials and world-object presentation remain in their production owners.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PropShowcase : MonoBehaviour, IDecorationProceduralMaterialResolver
    {
        public const int ExpectedCatalogueCount = 529;
        private const float VoxelSize = 0.1f;
        private const uint WorldSeed = 0x50524F50u; // PROP
        private const uint StructureId = 0x50525348u; // PRSH
        private const uint SpaceId = 0x50525657u; // PRVW
        private const uint StyleId = 2u;
        private const int PanelWidth = 390;
        private const float RowHeight = 27f;

        private Camera _camera;
        private IVoxelStorageRuntime _storage;
        private DecorationShowcaseEntry[] _entries = Array.Empty<DecorationShowcaseEntry>();
        private DecorationContext _context;
        private GameObject _presentationRoot;
        private DecorationProceduralMeshPresenter _procedural;
        private DecorationThinSurfacePresenter _thin;
        private UnityWorldObjectPresentationSink _worldObjects;
        private GameObject _floor;
        private GameObject _support;
        private Light _keyLight;
        private Vector2 _scroll;
        private int _selectedIndex = -1;
        private string _status = "INITIALIZING";
        private bool _captureAutomation;
        private float _captureStartedAt;
        private int _capturePhase;
        private int _switchCount;
        private int _peakOwnedPresenters;
        private long _lastVoxelCount;
        private float _lastSwitchMs;

        public int EntryCount => _entries.Length;
        public int SelectedIndex => _selectedIndex;
        public int SwitchCount => _switchCount;
        public int OwnedPresentationCount =>
            (_procedural?.ActiveCount ?? 0) +
            ((_thin != null && _thin.HasActiveSurface) ? 1 : 0) +
            (_worldObjects?.ProxyCount ?? 0);
        public string SelectedStableId =>
            _selectedIndex >= 0 && _selectedIndex < _entries.Length ? _entries[_selectedIndex].StableId : string.Empty;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _camera = GetComponent<Camera>();
            _context = new DecorationContext
            {
                WorldSeed = WorldSeed,
                StructureId = StructureId,
                SpaceId = SpaceId,
                StyleId = StyleId,
                StructureKind = DecorationStructureKind.House,
                SpaceKind = DecorationSpaceKind.Study,
                Wealth = DecorationWealthTier.Comfortable,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
            };
            if (!_context.IsWellFormed)
                throw new InvalidOperationException("PropShowcase production decoration context is invalid.");

            _entries = DecorationShowcaseCatalog.CreateEntries();
            if (_entries.Length != DecorationShowcaseCatalog.Count || _entries.Length != ExpectedCatalogueCount)
                throw new InvalidOperationException(
                    $"PropShowcase catalogue parity failed: entries={_entries.Length} source={DecorationShowcaseCatalog.Count} expected={ExpectedCatalogueCount}.");

            ConfigurePresentation();
            CreateOwnedPresenters();
            CreateNeutralEnvironment();
            _captureAutomation = IsPlayerCaptureHarness();
            Select(0);
            if (_captureAutomation)
            {
                _captureStartedAt = Time.unscaledTime;
                _capturePhase = 0;
                Debug.Log(
                    $"PROP_SHOWCASE_VALIDATION start count={_entries.Length} selected={SelectedStableId} " +
                    $"owned={OwnedPresentationCount}");
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            ClearSelectionPresentation();
            RenderingComposition.ClearWorld();
            if (_presentationRoot != null)
                Destroy(_presentationRoot);
            _presentationRoot = null;
            _procedural = null;
            _thin = null;
            _worldObjects = null;
            _floor = null;
            _support = null;
            _keyLight = null;
        }

        private void Update()
        {
            if (_entries.Length == 0) return;
            Keyboard keyboard = Keyboard.current;
            if (!_captureAutomation && keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                    Select((_selectedIndex + _entries.Length - 1) % _entries.Length);
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    Select((_selectedIndex + 1) % _entries.Length);
                if (keyboard.homeKey.wasPressedThisFrame)
                    Select(0);
                if (keyboard.endKey.wasPressedThisFrame)
                    Select(_entries.Length - 1);
            }
            UpdateCaptureAutomation();
        }

        public bool TryResolve(byte materialId, out Material material) =>
            GameMaterialComposition.TryGetProceduralMaterial(materialId, out material);

        public bool Select(int index)
        {
            if (index < 0 || index >= _entries.Length)
                return false;

            float started = Time.realtimeSinceStartup;
            ClearSelectionPresentation();
            DecorationShowcaseEntry entry = _entries[index];
            if (!DecorationShowcaseRealizer.TryCreate(in entry, in _context, out DecorationShowcaseRealization realization))
            {
                _selectedIndex = index;
                _status = "ERROR · production realization unavailable";
                Debug.LogError($"PROP_SHOWCASE_VALIDATION failure: realization {entry.StableId}");
                return false;
            }

            bool ok;
            switch (realization.Kind)
            {
                case DecorationShowcaseRealizationKind.Decoration:
                    ok = PresentDecoration(in realization);
                    break;
                case DecorationShowcaseRealizationKind.MineCave:
                    ok = PresentMineCave(in realization);
                    break;
                case DecorationShowcaseRealizationKind.NaturalCave:
                    ok = PresentNaturalCave(in realization);
                    break;
                case DecorationShowcaseRealizationKind.WorldObject:
                    ok = PresentWorldObject(in realization);
                    break;
                default:
                    ok = false;
                    break;
            }

            _selectedIndex = index;
            _switchCount++;
            _lastSwitchMs = (Time.realtimeSinceStartup - started) * 1000f;
            _peakOwnedPresenters = Mathf.Max(_peakOwnedPresenters, OwnedPresentationCount);
            UpdateSupportSurface(in realization);
            Frame(in realization.Bounds);
            _status = ok
                ? $"READY · {entry.Source} · {realization.DecorationBackend} · {_lastSwitchMs:0.0} ms"
                : "ERROR · production presentation failed";
            if (!ok)
                Debug.LogError($"PROP_SHOWCASE_VALIDATION failure: presentation {entry.StableId}");
            return ok;
        }

        private bool PresentDecoration(in DecorationShowcaseRealization realization)
        {
            DecorationPlacement placement = realization.Decoration;
            bool isPreset = realization.Entry.Source == DecorationShowcaseEntrySource.Preset;
            switch (placement.Backend)
            {
                case DecorationRenderBackend.ProceduralMesh:
                {
                    DecorationProceduralMeshRequest[] requests =
                        DecorationProceduralMeshHookPlanner.Collect(new[] { placement });
                    return requests.Length == 1 && _procedural.TryPresent(in requests[0], this);
                }
                case DecorationRenderBackend.ThinSurface:
                    return _thin.TryPresent(new[] { placement }, in _context, this, VoxelSize);
                case DecorationRenderBackend.BoxAssembly:
                case DecorationRenderBackend.VoxelStamp:
                    return PresentVoxel(authoring =>
                    {
                        if (isPreset)
                            return DecorationReusablePresetAuthoringEmitter.TryAuthor(
                                authoring, in placement, in _context);
                        return DecorationCanonicalAuthoringEmitter.TryAuthorGeometry(
                            authoring,
                            new[] { placement },
                            in _context,
                            DecorationRegionTheme.Kentridge);
                    });
                default:
                    return false;
            }
        }

        private bool PresentMineCave(in DecorationShowcaseRealization realization)
        {
            MineCaveDecorationInstance instance = realization.MineCave;
            var instances = new[] { instance };
            if (instance.Backend == DecorationRenderBackend.ProceduralMesh)
            {
                MineCaveMeshRequest[] requests = MineCaveDecorationPresentation.CollectMeshRequests(instances);
                return requests.Length == 1 && _procedural.TryPresent(in requests[0], this);
            }
            return PresentVoxel(authoring =>
                MineCaveDecorationPresentation.TryAuthorGeometry(authoring, instances, in _context));
        }

        private bool PresentNaturalCave(in DecorationShowcaseRealization realization)
        {
            NaturalCaveDecorationInstance instance = realization.NaturalCave;
            var instances = new[] { instance };
            if (instance.Backend == DecorationRenderBackend.ProceduralMesh)
            {
                NaturalCaveMeshRequest[] requests = NaturalCaveDecorationPresentation.CollectMeshRequests(instances);
                return requests.Length == 1 && _procedural.TryPresent(in requests[0], this);
            }
            if (instance.Backend == DecorationRenderBackend.ThinSurface)
            {
                NaturalCaveThinSurfaceRequest[] requests = NaturalCaveDecorationPresentation.CollectThinSurfaces(instances);
                return requests.Length == 1 && _thin.TryPresent(in requests[0], this, VoxelSize);
            }
            return PresentVoxel(authoring =>
                NaturalCaveDecorationPresentation.TryAuthorVoxelStamps(authoring, instances));
        }

        private bool PresentWorldObject(in DecorationShowcaseRealization realization)
        {
            WorldObjectResolvedState state = realization.WorldObject;
            WorldObjectPresentationPlan plan = WorldObjectPresentationPlanner.Plan(in state);
            if (plan.UsesDynamicProxy)
            {
                _worldObjects.CreateOrUpdate(in plan);
                return _worldObjects.ProxyCount == 1;
            }
            return PresentVoxel(authoring => WorldObjectGeometryEmitter.Emit(authoring, in state));
        }

        private bool PresentVoxel(Func<IStructureAuthoringSession, bool> author)
        {
            _storage = VoxelEngineBootstrap.CreateStorage(16, 24_000);
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

            IStructureAuthoringSession authoring = VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 1_000_000);
            if (!author(authoring) || authoring.BudgetExceeded)
                return false;
            _lastVoxelCount = authoring.TotalVoxelsWritten;

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world,
                _storage.Changes,
                WorldSeed,
                solidBuildBudgetMs: 8.0,
                waterBuildBudgetMs: 4.0,
                farFieldEnabled: false);
            return true;
        }

        private void ClearSelectionPresentation()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _lastVoxelCount = 0;
            _procedural?.Clear();
            _thin?.Clear();
            _worldObjects?.Clear();
        }

        private void ConfigurePresentation()
        {
            GameMaterialComposition.Install();
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetVoxelLodEnabled(false);
            RenderingComposition.SetSky(
                new Color(0.56f, 0.64f, 0.71f, 1f),
                new Color(0.20f, 0.27f, 0.34f, 1f));
            RenderingComposition.ConfigureEnvironment(
                new Color(0.72f, 0.74f, 0.76f, 1f),
                new Vector3(-0.45f, 0.82f, -0.35f).normalized,
                new Color(1.0f, 0.93f, 0.82f, 1f),
                new Color(0.42f, 0.46f, 0.50f, 1f));
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.52f, 0.59f, 0.64f, 1f);
            _camera.fieldOfView = 42f;
            _camera.nearClipPlane = 0.02f;
            _camera.farClipPlane = 150f;
            _camera.allowHDR = false;
        }

        private void CreateOwnedPresenters()
        {
            _presentationRoot = new GameObject("PropShowcase Presentation Root");
            _presentationRoot.transform.position = Vector3.zero;
            _presentationRoot.transform.rotation = Quaternion.identity;
            _presentationRoot.transform.localScale = Vector3.one;
            _procedural = _presentationRoot.AddComponent<DecorationProceduralMeshPresenter>();
            _thin = _presentationRoot.AddComponent<DecorationThinSurfacePresenter>();
            _worldObjects = _presentationRoot.AddComponent<UnityWorldObjectPresentationSink>();
        }

        private void CreateNeutralEnvironment()
        {
            Transform presentationTransform = _presentationRoot.transform;

            _floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floor.name = "PropShowcase Neutral Floor";
            _floor.transform.SetParent(presentationTransform, false);
            if (TryResolve(GameMaterialIds.Stone, out Material floorMaterial))
                _floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

            _support = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _support.name = "PropShowcase Neutral Support";
            _support.transform.SetParent(presentationTransform, false);
            if (TryResolve(GameMaterialIds.MasonrySmall, out Material supportMaterial))
                _support.GetComponent<MeshRenderer>().sharedMaterial = supportMaterial;
            _support.SetActive(false);

            var lightObject = new GameObject("PropShowcase Key Light");
            lightObject.transform.SetParent(presentationTransform, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            _keyLight = lightObject.AddComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.intensity = 1.15f;
            _keyLight.color = new Color(1f, 0.95f, 0.86f, 1f);
            _keyLight.shadows = LightShadows.Soft;
        }

        private void UpdateSupportSurface(in DecorationShowcaseRealization realization)
        {
            DecorationBounds b = realization.Bounds;
            Vector3 centre = BoundsCentre(b);
            float span = Mathf.Max(2.5f, Mathf.Max(b.Size.x, b.Size.z) * VoxelSize * 2.5f);
            _floor.transform.position = new Vector3(centre.x, b.Min.y * VoxelSize - 0.01f, centre.z);
            _floor.transform.rotation = Quaternion.identity;
            _floor.transform.localScale = new Vector3(span / 10f, 1f, span / 10f);
            _support.SetActive(false);

            int3 facing = FacingOf(in realization);
            if (math.abs(facing.y) == 0)
            {
                _support.SetActive(true);
                float height = Mathf.Max(2.5f, b.Size.y * VoxelSize * 2.3f);
                float width = Mathf.Max(2.5f, math.max(b.Size.x, b.Size.z) * VoxelSize * 2.3f);
                _support.transform.position = new Vector3(
                    centre.x,
                    b.Min.y * VoxelSize + height * 0.5f,
                    b.Min.z * VoxelSize - 0.015f);
                _support.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _support.transform.localScale = new Vector3(width / 10f, 1f, height / 10f);
            }
            else if (facing.y < 0)
            {
                _support.SetActive(true);
                _support.transform.position = new Vector3(centre.x, b.MaxExclusive.y * VoxelSize + 0.01f, centre.z);
                _support.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                _support.transform.localScale = new Vector3(span / 10f, 1f, span / 10f);
            }
        }

        private static int3 FacingOf(in DecorationShowcaseRealization realization)
        {
            switch (realization.Kind)
            {
                case DecorationShowcaseRealizationKind.Decoration: return realization.Decoration.Facing;
                case DecorationShowcaseRealizationKind.MineCave: return realization.MineCave.Facing;
                case DecorationShowcaseRealizationKind.NaturalCave: return realization.NaturalCave.Facing;
                case DecorationShowcaseRealizationKind.WorldObject: return realization.WorldObject.Descriptor.Facing;
                default: return new int3(0, 1, 0);
            }
        }

        private void Frame(in DecorationBounds bounds)
        {
            Vector3 centre = BoundsCentre(bounds);
            Vector3 size = new Vector3(bounds.Size.x, bounds.Size.y, bounds.Size.z) * VoxelSize;
            float radius = Mathf.Max(0.45f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.65f);
            float distance = Mathf.Max(1.8f, radius / Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.5f);
            Vector3 focus = centre + new Vector3(-radius * 0.18f, size.y * 0.05f, 0f);
            _camera.transform.position = centre + new Vector3(radius * 1.15f, radius * 0.78f, -distance);
            _camera.transform.LookAt(focus);
        }

        private static Vector3 BoundsCentre(in DecorationBounds bounds)
        {
            float3 centre = ((float3)bounds.Min + (float3)bounds.MaxExclusive) * 0.5f * VoxelSize;
            return new Vector3(centre.x, centre.y, centre.z);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(0, 0, PanelWidth, Screen.height), string.Empty);
            GUI.Label(new Rect(18, 12, PanelWidth - 36, 28), $"PROP SHOWCASE · {_entries.Length} PRODUCTION ENTRIES");
            GUI.Label(new Rect(18, 39, PanelWidth - 36, 24), _status);
            Rect scrollArea = new Rect(12, 70, PanelWidth - 24, Mathf.Max(100, Screen.height - 90));
            Rect content = new Rect(0, 0, PanelWidth - 46, _entries.Length * RowHeight + 8f);
            _scroll = GUI.BeginScrollView(scrollArea, _scroll, content);
            for (int i = 0; i < _entries.Length; i++)
            {
                DecorationShowcaseEntry entry = _entries[i];
                Rect row = new Rect(2, i * RowHeight, content.width - 6, RowHeight - 2);
                string label = $"{i + 1:000}  {entry.DisplayName}   [{entry.Category}]";
                bool selected = i == _selectedIndex;
                if (GUI.Toggle(row, selected, label, GUI.skin.button) && !selected)
                    Select(i);
            }
            GUI.EndScrollView();

            if (_selectedIndex >= 0 && _selectedIndex < _entries.Length)
            {
                DecorationShowcaseEntry selected = _entries[_selectedIndex];
                float width = Mathf.Max(250f, Screen.width - PanelWidth - 30f);
                GUI.Box(new Rect(PanelWidth + 14, 14, width, 64), string.Empty);
                GUI.Label(new Rect(PanelWidth + 28, 22, width - 28, 24), selected.DisplayName);
                GUI.Label(new Rect(PanelWidth + 28, 47, width - 28, 22),
                    $"{selected.StableId} · {selected.Category} · switch {_switchCount} · voxels {_lastVoxelCount:N0}");
            }
        }

        private void UpdateCaptureAutomation()
        {
            if (!_captureAutomation || _entries.Length == 0) return;
            float elapsed = Time.unscaledTime - _captureStartedAt;
            if (_capturePhase == 0 && elapsed >= 5f)
            {
                int target = FindFirst(e => e.Source == DecorationShowcaseEntrySource.RegisteredDecoration &&
                                            e.DisplayName.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0);
                if (target < 0) target = _entries.Length / 4;
                Select(target);
                Debug.Log($"PROP_SHOWCASE_VALIDATION medium selected={SelectedStableId} owned={OwnedPresentationCount}");
                _capturePhase = 1;
            }
            else if (_capturePhase == 1 && elapsed >= 11f)
            {
                int target = FindFirst(e => e.Source == DecorationShowcaseEntrySource.RegisteredDecoration &&
                                            e.DisplayName.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0);
                if (target < 0) target = FindFirst(e => e.Source == DecorationShowcaseEntrySource.NaturalCave);
                Select(target >= 0 ? target : 0);
                Debug.Log($"PROP_SHOWCASE_VALIDATION thin selected={SelectedStableId} owned={OwnedPresentationCount}");
                _capturePhase = 2;
            }
            else if (_capturePhase == 2 && elapsed >= 17f)
            {
                int target = FindFirst(e => e.Source == DecorationShowcaseEntrySource.MineCave &&
                                            e.DisplayName.IndexOf("Rope", StringComparison.OrdinalIgnoreCase) >= 0);
                Select(target >= 0 ? target : _entries.Length / 2);
                Debug.Log($"PROP_SHOWCASE_VALIDATION procedural selected={SelectedStableId} owned={OwnedPresentationCount}");
                _capturePhase = 3;
            }
            else if (_capturePhase == 3 && elapsed >= 23f)
            {
                int target = FindFirst(e => e.Source == DecorationShowcaseEntrySource.WorldObject &&
                                            e.DisplayName.IndexOf("Elevator", StringComparison.OrdinalIgnoreCase) >= 0);
                Select(target >= 0 ? target : _entries.Length - 1);
                Debug.Log($"PROP_SHOWCASE_VALIDATION world-object selected={SelectedStableId} owned={OwnedPresentationCount}");
                _capturePhase = 4;
            }
            else if (_capturePhase == 4 && elapsed >= 29f)
            {
                // Deterministic lifecycle stress: many replacements in one frame sequence, then leave
                // one valid representative selected for capture/evidence.
                int stride = Mathf.Max(1, _entries.Length / 31);
                for (int i = 0; i < _entries.Length; i += stride)
                    Select(i);
                Select(_entries.Length - 1);
                if (OwnedPresentationCount > 1)
                    Debug.LogError($"PROP_SHOWCASE_VALIDATION failure: stale-owned-presenters count={OwnedPresentationCount}");
                Debug.Log(
                    $"PROP_SHOWCASE_VALIDATION stress switches={_switchCount} owned={OwnedPresentationCount} " +
                    $"peakOwned={_peakOwnedPresenters} lastSwitchMs={_lastSwitchMs:0.0}");
                _capturePhase = 5;
            }
            else if (_capturePhase == 5 && elapsed >= 36f)
            {
                Debug.Log(
                    $"PROP_SHOWCASE_VALIDATION complete selected={SelectedStableId} switches={_switchCount} " +
                    $"owned={OwnedPresentationCount} peakOwned={_peakOwnedPresenters}");
                _capturePhase = 6;
            }
        }

        private int FindFirst(Predicate<DecorationShowcaseEntry> predicate)
        {
            for (int i = 0; i < _entries.Length; i++)
                if (predicate(_entries[i])) return i;
            return -1;
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
    }
}