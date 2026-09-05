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
    /// Production-path browser for guild-house shells and socketed furnishings. House and prop
    /// identities come exclusively from Game.Structures queries; this component owns only showcase
    /// selection, camera, status presentation, and renderer lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class HouseShowcase : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private const int HouseWidth = 128;
        private const int HouseDepth = 128;
        private const uint StructureId = 0x48534F57u;
        private const int RegenerationSearchLimit = 64;
        private const uint RegenerationStep = 0x9E3779B9u;

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private GuildHouseDescriptor[] _houses = Array.Empty<GuildHouseDescriptor>();
        private GuildHouseFurnishingOption[] _options = Array.Empty<GuildHouseFurnishingOption>();
        private GuildHouseResolvedRoom[] _resolvedRooms = Array.Empty<GuildHouseResolvedRoom>();
        private readonly HashSet<ushort> _selected = new HashSet<ushort>();
        private readonly Dictionary<ushort, GuildHouseUnplacedReason> _unplaced =
            new Dictionary<ushort, GuildHouseUnplacedReason>();
        private int _houseIndex;
        private uint _seed = 0x484F5553u;
        private GuildHousePrototype _prototype;
        private Vector2 _propScroll;
        private string _status = "INITIALIZING";
        private float _moveSpeed = 9f;
        private bool _pointerLocked;
        private bool _built;
        private bool _captureAutomation;
        private float _captureStartedAt;
        private int _capturePhase;

        public bool IsBuilt => _built;
        public uint Seed => _seed;
        public int HouseIndex => _houseIndex;
        public int HouseCount => _houses.Length;
        public string SelectedHouseName =>
            _houses.Length == 0 ? string.Empty : _houses[_houseIndex].DisplayName;
        internal static Color ProductionSurfaceDebugTint => Color.white;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _camera = GetComponent<Camera>();
            _houses = GuildHouseCatalogQuery.Houses();
            if (_houses.Length == 0)
                throw new InvalidOperationException("HouseShowcase found no production guild houses.");
            ConfigurePresentation();

            _captureAutomation = IsPlayerCaptureHarness();
            int initial = _captureAutomation ? FindHouseIndex(GuildHouseKind.Wizards) : 0;
            SelectHouse(initial >= 0 ? initial : 0, resetSelection: true);
            if (_captureAutomation)
            {
                SetPointerLock(false);
                _capturePhase = 0;
                _captureStartedAt = Time.unscaledTime;
                Debug.Log(
                    $"HOUSE_SHOWCASE_VALIDATION start house={CurrentHouse.Key} seed={_seed} " +
                    $"houses={_houses.Length} options={_options.Length} selected={_selected.Count}");
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

            if (RenderingComposition.TryGetSurfaceBuildStatus(
                    out int known, out int dirty, out int resident, out long bytes))
            {
                _status = dirty == 0
                    ? $"READY  {resident}/{known}  {bytes / (1024f * 1024f):0.0} MB"
                    : $"MESHING  {resident}/{known}  dirty={dirty}";
            }

            UpdateCaptureAutomation();
        }

        private void UpdateManualInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.leftBracketKey.wasPressedThisFrame)
                    SelectHouse((_houseIndex + _houses.Length - 1) % _houses.Length, true);
                if (keyboard.rightBracketKey.wasPressedThisFrame)
                    SelectHouse((_houseIndex + 1) % _houses.Length, true);
                if (keyboard.rKey.wasPressedThisFrame) Regenerate();
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
                    float pitch = NormalizePitch(euler.x) - delta.y * 0.08f;
                    pitch = Mathf.Clamp(pitch, -85f, 85f);
                    _camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                }
            }

            float horizontal = 0f;
            float vertical = 0f;
            float elevation = 0f;
            float boost = 1f;
            if (keyboard != null)
            {
                horizontal = (keyboard.dKey.isPressed ? 1f : 0f) -
                             (keyboard.aKey.isPressed ? 1f : 0f);
                vertical = (keyboard.wKey.isPressed ? 1f : 0f) -
                           (keyboard.sKey.isPressed ? 1f : 0f);
                elevation = (keyboard.eKey.isPressed ? 1f : 0f) -
                            (keyboard.qKey.isPressed ? 1f : 0f);
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

        private GuildHouseDescriptor CurrentHouse => _houses[_houseIndex];

        private void SelectHouse(int index, bool resetSelection)
        {
            _houseIndex = Mathf.Clamp(index, 0, _houses.Length - 1);
            GuildHouseDescriptor house = CurrentHouse;
            if (!GuildHouseCatalogQuery.TryGetFurnishings(house.Kind, out _options))
                throw new InvalidOperationException($"Could not query furnishings for {house.DisplayName}.");

            if (resetSelection)
            {
                _selected.Clear();
                int defaults = 0;
                for (int i = 0; i < _options.Length && defaults < 8; i++)
                {
                    if (!_options[i].Selectable) continue;
                    _selected.Add(_options[i].Decoration.StableId);
                    defaults++;
                }
            }
            Rebuild();
        }

        private void Regenerate()
        {
            if (!_prototype.IsWellFormed)
                throw new InvalidOperationException("Cannot regenerate before a valid production house exists.");

            GuildHousePrototype baseline = _prototype;
            GuildHouseDescriptor house = CurrentHouse;
            uint baselineSeed = _seed;
            for (uint delta = 1; delta <= RegenerationSearchLimit; delta++)
            {
                uint candidate = unchecked(baselineSeed + delta * RegenerationStep);
                GuildHousePrototype probe = GuildHousePrototypeComposition.Build(
                    house.Kind,
                    DecorationRegionTheme.Kentridge,
                    candidate,
                    StructureId + (uint)_houseIndex,
                    new int3(0, 16, 0),
                    HouseWidth,
                    HouseDepth,
                    house.MinimumRooms);
                if (!probe.IsWellFormed || SameSpatialSignature(in baseline, in probe))
                    continue;

                _seed = candidate;
                Rebuild();
                return;
            }

            throw new InvalidOperationException(
                $"Could not find a materially different production {house.DisplayName} seed " +
                $"within {RegenerationSearchLimit} deterministic candidates.");
        }

        private void Rebuild()
        {
            ShutdownWorld();
            GuildHouseDescriptor house = CurrentHouse;
            ushort[] selected = SelectedInProductionOrder();
            if (!GuildHouseFurnishingPalette.TryCreate(house.Kind, selected, out GuildHouseFurnishingPalette palette))
                throw new InvalidOperationException("Production furnishing policy rejected the showcase selection.");

            var origin = new int3(0, 16, 0);
            _prototype = GuildHousePrototypeComposition.Build(
                house.Kind,
                DecorationRegionTheme.Kentridge,
                _seed,
                StructureId + (uint)_houseIndex,
                origin,
                HouseWidth,
                HouseDepth,
                house.MinimumRooms);
            if (!_prototype.IsWellFormed)
                throw new InvalidOperationException($"Production house composition failed for {house.DisplayName}.");
            if (!GuildHouseFurnishingResolver.TryResolvePrototype(
                    in _prototype,
                    in palette,
                    out _resolvedRooms,
                    out GuildHouseUnplacedFurnishing[] resolvedUnplaced))
                throw new InvalidOperationException("Production furnishing resolution failed.");

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

            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 8_000_000);
            if (!GuildHouseFurnishedPrototypeAuthoring.TryAuthor(
                    authoring, in _prototype, in palette, out GuildHouseUnplacedFurnishing[] unplaced))
                throw new InvalidOperationException("Production furnished-house authoring failed.");
            if (authoring.BudgetExceeded)
                throw new InvalidOperationException("HouseShowcase exceeded its production authoring budget.");
            if (!SameUnplaced(resolvedUnplaced, unplaced))
                throw new InvalidOperationException("Production furnishing diagnostics changed between resolution and authoring.");

            _unplaced.Clear();
            for (int i = 0; i < unplaced.Length; i++)
                _unplaced[unplaced[i].StableId] = unplaced[i].Reason;

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
            _status = $"AUTHORED  {authoring.TotalVoxelsWritten:N0} voxels";
            FrameExterior();
            Debug.Log(
                $"HouseShowcase ready: house={house.Key} seed={_seed} selected={selected.Length} " +
                $"unplaced={unplaced.Length} voxels={authoring.TotalVoxelsWritten}");
        }

        private ushort[] SelectedInProductionOrder()
        {
            var result = new List<ushort>(_selected.Count);
            for (int i = 0; i < _options.Length; i++)
            {
                GuildHouseFurnishingOption option = _options[i];
                if (option.Selectable && _selected.Contains(option.Decoration.StableId))
                    result.Add(option.Decoration.StableId);
            }
            return result.ToArray();
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
            _camera.fieldOfView = 48f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 220f;
            _camera.allowHDR = false;
        }

        private void FrameExterior()
        {
            if (_camera == null || !_prototype.IsWellFormed) return;
            GuildHouseSpatialPlan plan = _prototype.SpatialPlan;
            float width = plan.Width * VoxelSize;
            float depth = plan.Depth * VoxelSize;
            float structuralHeight = Mathf.Max(3f, plan.FloorCount * plan.FloorHeight * VoxelSize);
            float baseY = plan.Origin.y * VoxelSize;
            float centreX = (plan.Origin.x + plan.Width * 0.5f) * VoxelSize;
            float centreZ = (plan.Origin.z + plan.Depth * 0.5f) * VoxelSize;

            // Bias the look target left so the production house occupies the unobscured right-hand
            // preview beside the catalog panel. A modest elevated 3/4 view keeps facade, roof and
            // exterior semantic dressing visually connected in the same frame.
            Vector3 focus = new Vector3(
                centreX - width * 0.18f,
                baseY + structuralHeight * 0.48f,
                (plan.Origin.z + plan.Depth * 0.34f) * VoxelSize);
            float distance = Mathf.Max(18f, Mathf.Max(width, depth) * 1.72f);
            _camera.transform.position = new Vector3(
                centreX + width * 0.52f,
                baseY + structuralHeight * 0.83f,
                centreZ - distance - depth * 0.42f);
            _camera.fieldOfView = 48f;
            _camera.transform.LookAt(focus);
        }

        private void FrameInterior()
        {
            if (_camera == null || !_prototype.IsWellFormed || _prototype.Rooms.Length == 0) return;
            int roomIndex = FindBestInteriorRoom();
            GuildHouseSpatialRoom room = _prototype.Rooms[roomIndex].SpatialRoom;
            float3 roomCentreVoxels = ((float3)room.Min + (float3)room.MaxExclusive) * 0.5f;
            Vector3 roomCentre = ToWorld(roomCentreVoxels);
            Vector3 target = roomCentre + new Vector3(0.6f, 0f, 0.6f);

            if (_resolvedRooms.Length == _prototype.Rooms.Length)
            {
                DecorationPlacement[] placements = _resolvedRooms[roomIndex].Placements;
                DecorationPlacement chosen = default;
                bool found = false;
                for (int i = 0; i < placements.Length; i++)
                {
                    ushort stableId = DecorationCanonicalPlacementCatalog.StableIdOfVariant(placements[i].Variant);
                    if (!_selected.Contains(stableId)) continue;
                    chosen = placements[i];
                    found = true;
                    break;
                }
                if (!found && placements.Length > 0)
                {
                    chosen = placements[0];
                    found = true;
                }
                if (found)
                {
                    float3 placementCentre =
                        ((float3)chosen.Bounds.Min + (float3)chosen.Bounds.MaxExclusive) * (0.5f * VoxelSize);
                    target = new Vector3(placementCentre.x, placementCentre.y, placementCentre.z);
                }
            }

            float inset = 1.15f;
            float minX = room.Min.x * VoxelSize + inset;
            float maxX = room.MaxExclusive.x * VoxelSize - inset;
            float minZ = room.Min.z * VoxelSize + inset;
            float maxZ = room.MaxExclusive.z * VoxelSize - inset;
            Vector2[] corners =
            {
                new Vector2(minX, minZ),
                new Vector2(maxX, minZ),
                new Vector2(minX, maxZ),
                new Vector2(maxX, maxZ),
            };
            Vector2 targetXZ = new Vector2(target.x, target.z);
            Vector2 best = corners[0];
            float bestDistance = -1f;
            for (int i = 0; i < corners.Length; i++)
            {
                float distance = (corners[i] - targetXZ).sqrMagnitude;
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                best = corners[i];
            }

            Vector3 position = new Vector3(best.x, room.Min.y * VoxelSize + 1.55f, best.y);
            if (bestDistance < 4f)
            {
                target = roomCentre;
                targetXZ = new Vector2(target.x, target.z);
                bestDistance = -1f;
                for (int i = 0; i < corners.Length; i++)
                {
                    float distance = (corners[i] - targetXZ).sqrMagnitude;
                    if (distance <= bestDistance) continue;
                    bestDistance = distance;
                    best = corners[i];
                }
                position.x = best.x;
                position.z = best.y;
            }

            target.y = Mathf.Clamp(target.y, position.y - 0.45f, position.y + 0.45f);
            _camera.transform.position = position;
            _camera.fieldOfView = 58f;
            _camera.transform.LookAt(target);
        }

        private int FindBestInteriorRoom()
        {
            int fallback = 0;
            int placementFallback = -1;
            for (int i = 0; i < _prototype.Rooms.Length; i++)
            {
                GuildHouseRoomComposition room = _prototype.Rooms[i];
                if ((room.Context.Environment & DecorationEnvironmentTags.Interior) != DecorationEnvironmentTags.Interior)
                    continue;
                fallback = i;
                if (_resolvedRooms.Length != _prototype.Rooms.Length) continue;
                DecorationPlacement[] placements = _resolvedRooms[i].Placements;
                if (placements.Length > 0 && placementFallback < 0) placementFallback = i;
                for (int placementIndex = 0; placementIndex < placements.Length; placementIndex++)
                {
                    ushort stableId = DecorationCanonicalPlacementCatalog.StableIdOfVariant(
                        placements[placementIndex].Variant);
                    if (_selected.Contains(stableId)) return i;
                }
            }
            return placementFallback >= 0 ? placementFallback : fallback;
        }

        private void ShutdownWorld()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _resolvedRooms = Array.Empty<GuildHouseResolvedRoom>();
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
                if (_capturePhase == 0 && elapsed >= 12f)
                {
                    FrameInterior();
                    Debug.Log(
                        $"HOUSE_SHOWCASE_VALIDATION interior house={CurrentHouse.Key} seed={_seed} " +
                        $"selected={_selected.Count} unplaced={_unplaced.Count}");
                    _capturePhase = 1;
                }
                else if (_capturePhase == 1 && elapsed >= 22f)
                {
                    string previous = CurrentHouse.Key;
                    int previousOptions = _options.Length;
                    int knight = FindHouseIndex(GuildHouseKind.Knights);
                    if (knight < 0)
                        throw new InvalidOperationException("HouseShowcase capture could not find Knights production house.");
                    SelectHouse(knight, resetSelection: true);
                    Debug.Log(
                        $"HOUSE_SHOWCASE_VALIDATION switched from={previous} to={CurrentHouse.Key} " +
                        $"previousOptions={previousOptions} options={_options.Length} selected={_selected.Count}");
                    _capturePhase = 2;
                }
                else if (_capturePhase == 2 && elapsed >= 32f)
                {
                    uint before = _seed;
                    Regenerate();
                    Debug.Log(
                        $"HOUSE_SHOWCASE_VALIDATION regenerated house={CurrentHouse.Key} fromSeed={before} " +
                        $"toSeed={_seed} spatialChanged=true selected={_selected.Count}");
                    _capturePhase = 3;
                }
                else if (_capturePhase == 3 && elapsed >= 42f)
                {
                    FrameInterior();
                    Debug.Log(
                        $"HOUSE_SHOWCASE_VALIDATION complete house={CurrentHouse.Key} seed={_seed} " +
                        $"houses={_houses.Length} selected={_selected.Count} unplaced={_unplaced.Count}");
                    _capturePhase = 4;
                }
            }
            catch (Exception ex)
            {
                _capturePhase = int.MaxValue;
                Debug.LogError("HOUSE_SHOWCASE_VALIDATION failure: " + ex.Message);
                throw;
            }
        }

        private int FindHouseIndex(GuildHouseKind kind)
        {
            for (int i = 0; i < _houses.Length; i++)
                if (_houses[i].Kind == kind)
                    return i;
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

        private static bool SameSpatialSignature(in GuildHousePrototype first, in GuildHousePrototype second)
        {
            if (first.SpatialPlan.ShellStyle != second.SpatialPlan.ShellStyle ||
                first.SpatialPlan.Rooms.Length != second.SpatialPlan.Rooms.Length)
                return false;
            for (int i = 0; i < first.SpatialPlan.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom a = first.SpatialPlan.Rooms[i];
                GuildHouseSpatialRoom b = second.SpatialPlan.Rooms[i];
                if (a.Node.Room.Role != b.Node.Room.Role ||
                    a.FloorIndex != b.FloorIndex ||
                    a.CellIndex != b.CellIndex ||
                    !math.all(a.Min == b.Min) ||
                    !math.all(a.Size == b.Size))
                    return false;
            }
            return true;
        }

        private static bool SameUnplaced(
            GuildHouseUnplacedFurnishing[] first,
            GuildHouseUnplacedFurnishing[] second)
        {
            if (first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++)
                if (first[i].StableId != second[i].StableId || first[i].Reason != second[i].Reason)
                    return false;
            return true;
        }

        private static Vector3 ToWorld(float3 voxels) =>
            new Vector3(voxels.x * VoxelSize, voxels.y * VoxelSize, voxels.z * VoxelSize);

        private static float NormalizePitch(float degrees) => degrees > 180f ? degrees - 360f : degrees;

        private void OnGUI()
        {
            if (_houses.Length == 0) return;
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            small.normal.textColor = Color.white;

            GUI.Box(new Rect(12f, 12f, 430f, 132f), GUIContent.none);
            GUI.Label(new Rect(24f, 20f, 405f, 28f),
                $"HouseShowcase  {_houseIndex + 1:00}/{_houses.Length:00}  {SelectedHouseName}", title);
            GUI.Label(new Rect(24f, 50f, 405f, 22f), $"Seed {_seed}  •  {_status}", small);
            GUI.Label(new Rect(24f, 72f, 405f, 42f),
                "[/] house  •  R regenerate seed  •  1 exterior  •  2 interior\n" +
                "WASD move  •  Q/E down/up  •  Shift boost  •  RMB mouse-look  •  wheel speed", small);
            if (GUI.Button(new Rect(24f, 114f, 94f, 24f), "← House"))
                SelectHouse((_houseIndex + _houses.Length - 1) % _houses.Length, true);
            if (GUI.Button(new Rect(124f, 114f, 94f, 24f), "House →"))
                SelectHouse((_houseIndex + 1) % _houses.Length, true);
            if (GUI.Button(new Rect(224f, 114f, 94f, 24f), "Regenerate")) Regenerate();
            if (GUI.Button(new Rect(324f, 114f, 50f, 24f), "Ext")) FrameExterior();
            if (GUI.Button(new Rect(378f, 114f, 50f, 24f), "Int")) FrameInterior();

            float panelHeight = Mathf.Min(Screen.height - 168f, 520f);
            GUI.Box(new Rect(12f, 154f, 430f, panelHeight), GUIContent.none);
            GUI.Label(new Rect(24f, 160f, 405f, 24f),
                "Production-compatible furnishings (toggle to rebuild)", title);
            Rect view = new Rect(22f, 188f, 410f, panelHeight - 44f);
            float contentHeight = Mathf.Max(view.height, _options.Length * 27f + 8f);
            _propScroll = GUI.BeginScrollView(view, _propScroll,
                new Rect(0f, 0f, 384f, contentHeight));
            float y = 2f;
            for (int i = 0; i < _options.Length; i++)
            {
                GuildHouseFurnishingOption option = _options[i];
                ushort id = option.Decoration.StableId;
                if (option.RequiredFixture)
                {
                    GUI.Label(new Rect(4f, y, 360f, 23f),
                        $"✓ required  {option.Decoration.DisplayName}  [{id}]", small);
                }
                else
                {
                    bool selected = _selected.Contains(id);
                    string suffix = _unplaced.TryGetValue(id, out GuildHouseUnplacedReason reason)
                        ? $"  — NOT PLACED: {reason}"
                        : selected ? "  — placed" : string.Empty;
                    bool next = GUI.Toggle(new Rect(4f, y, 370f, 23f), selected,
                        $"{option.Decoration.DisplayName}  [{id}]{suffix}");
                    if (next != selected)
                    {
                        if (next) _selected.Add(id); else _selected.Remove(id);
                        Rebuild();
                        GUI.EndScrollView();
                        return;
                    }
                }
                y += 27f;
            }
            GUI.EndScrollView();
        }
    }
}
