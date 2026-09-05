using System;
using System.Collections.Generic;
using Game.Composition.Materials;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
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

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private GuildHouseDescriptor[] _houses = Array.Empty<GuildHouseDescriptor>();
        private GuildHouseFurnishingOption[] _options = Array.Empty<GuildHouseFurnishingOption>();
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

            if (Input.GetKeyDown(KeyCode.LeftBracket))
                SelectHouse((_houseIndex + _houses.Length - 1) % _houses.Length, true);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                SelectHouse((_houseIndex + 1) % _houses.Length, true);
            if (Input.GetKeyDown(KeyCode.R)) Regenerate();
            if (Input.GetKeyDown(KeyCode.Alpha1)) FrameExterior();
            if (Input.GetKeyDown(KeyCode.Alpha2)) FrameInterior();
            if (Input.GetKeyDown(KeyCode.Escape)) SetPointerLock(false);

            if (Input.GetMouseButtonDown(1)) SetPointerLock(true);
            if (Input.GetMouseButtonUp(1)) SetPointerLock(false);
            if (_pointerLocked)
            {
                Vector3 euler = _camera.transform.eulerAngles;
                float yaw = euler.y + Input.GetAxis("Mouse X") * 2.6f;
                float pitch = NormalizePitch(euler.x) - Input.GetAxis("Mouse Y") * 2.6f;
                pitch = Mathf.Clamp(pitch, -85f, 85f);
                _camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            float boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 3f : 1f;
            Vector3 local = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                Input.GetAxisRaw("Vertical"));
            if (local.sqrMagnitude > 1f) local.Normalize();
            _camera.transform.position += _camera.transform.TransformDirection(local) *
                                          (_moveSpeed * boost * Time.unscaledDeltaTime);
            _moveSpeed = Mathf.Clamp(_moveSpeed * Mathf.Exp(Input.mouseScrollDelta.y * 0.12f), 2f, 40f);

            if (RenderingComposition.TryGetSurfaceBuildStatus(
                    out int known, out int dirty, out int resident, out long bytes))
            {
                _status = dirty == 0 && resident >= known
                    ? $"READY  {bytes / (1024f * 1024f):0.0} MB"
                    : $"MESHING  {resident}/{known}";
            }

            UpdateCaptureAutomation();
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
            uint candidate = _seed;
            for (int attempt = 0; attempt < RegenerationSearchLimit; attempt++)
            {
                candidate = NextSeed(candidate);
                GuildHousePrototype probe = GuildHousePrototypeComposition.Build(
                    house.Kind,
                    DecorationRegionTheme.Kentridge,
                    candidate,
                    StructureId + (uint)_houseIndex,
                    new int3(0, 16, 0),
                    HouseWidth,
                    HouseDepth,
                    house.PreferredRooms);
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
                house.PreferredRooms);
            if (!_prototype.IsWellFormed)
                throw new InvalidOperationException($"Production house composition failed for {house.DisplayName}.");

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
                new Color(0.78f, 0.81f, 0.84f, 1f),
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
            if (_camera == null || !_prototype.IsWellFormed || _prototype.Rooms.Length == 0) return;
            VoxelBounds bounds = PrototypeBounds(in _prototype);
            float3 centre = ((float3)bounds.Min + (float3)bounds.MaxExclusive) * 0.5f;
            float3 size = (float3)(bounds.MaxExclusive - bounds.Min);
            Vector3 focus = ToWorld(centre);
            float distance = Mathf.Max(14f, math.max(size.x, size.z) * VoxelSize * 1.25f);
            float height = Mathf.Max(7f, size.y * VoxelSize * 0.75f + 3f);
            _camera.transform.position = focus + new Vector3(-distance * 0.72f, height, -distance * 0.72f);
            _camera.transform.LookAt(focus);
        }

        private void FrameInterior()
        {
            if (_camera == null || !_prototype.IsWellFormed || _prototype.Rooms.Length == 0) return;
            int roomIndex = FindBestInteriorRoom();
            GuildHouseSpatialRoom room = _prototype.Rooms[roomIndex].SpatialRoom;
            float3 centre = ((float3)room.Min + (float3)room.MaxExclusive) * (0.5f * VoxelSize);
            _camera.transform.position = new Vector3(centre.x, room.Min.y * VoxelSize + 1.55f, centre.z);
            Vector3 target = _camera.transform.position + new Vector3(1f, 0f, 1f);
            _camera.transform.LookAt(target);
        }

        private int FindBestInteriorRoom()
        {
            int fallback = 0;
            for (int i = 0; i < _prototype.Rooms.Length; i++)
            {
                GuildHouseRoomComposition room = _prototype.Rooms[i];
                if ((room.Context.Environment & DecorationEnvironmentTags.Interior) != DecorationEnvironmentTags.Interior)
                    continue;
                fallback = i;
                for (int optionIndex = 0; optionIndex < room.OptionalArchetypes.Length; optionIndex++)
                    if (_selected.Contains(room.OptionalArchetypes[optionIndex]))
                        return i;
            }
            return fallback;
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

            if (_capturePhase == 0 && elapsed >= 3f)
            {
                FrameInterior();
                Debug.Log(
                    $"HOUSE_SHOWCASE_VALIDATION interior house={CurrentHouse.Key} seed={_seed} " +
                    $"selected={_selected.Count} unplaced={_unplaced.Count}");
                _capturePhase = 1;
            }
            else if (_capturePhase == 1 && elapsed >= 6f)
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
            else if (_capturePhase == 2 && elapsed >= 10f)
            {
                uint before = _seed;
                Regenerate();
                Debug.Log(
                    $"HOUSE_SHOWCASE_VALIDATION regenerated house={CurrentHouse.Key} fromSeed={before} " +
                    $"toSeed={_seed} spatialChanged=true selected={_selected.Count}");
                _capturePhase = 3;
            }
            else if (_capturePhase == 3 && elapsed >= 13f)
            {
                FrameInterior();
                Debug.Log(
                    $"HOUSE_SHOWCASE_VALIDATION complete house={CurrentHouse.Key} seed={_seed} " +
                    $"houses={_houses.Length} selected={_selected.Count} unplaced={_unplaced.Count}");
                _capturePhase = 4;
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

        private static uint NextSeed(uint seed)
        {
            unchecked { return seed * 1664525u + 1013904223u; }
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

        private readonly struct VoxelBounds
        {
            public readonly int3 Min;
            public readonly int3 MaxExclusive;

            public VoxelBounds(int3 min, int3 maxExclusive)
            {
                Min = min;
                MaxExclusive = maxExclusive;
            }
        }

        private static VoxelBounds PrototypeBounds(in GuildHousePrototype prototype)
        {
            int3 min = prototype.Rooms[0].SpatialRoom.Min;
            int3 max = prototype.Rooms[0].SpatialRoom.MaxExclusive;
            for (int i = 1; i < prototype.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom room = prototype.Rooms[i].SpatialRoom;
                min = math.min(min, room.Min);
                max = math.max(max, room.MaxExclusive);
            }
            return new VoxelBounds(min, max);
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
