using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Composition.Materials;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Debug = UnityEngine.Debug;

namespace Game.Structures.Validation
{
    /// <summary>
    /// Standalone-player proof for the production guild-house furnishing path. It deliberately
    /// exercises the same query, palette, prototype, authoring, voxel-storage and renderer surfaces
    /// used by HouseShowcase while owning no alternate house/prop/socket data.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class GuildHouseFurnishingRuntimeValidation : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private const int HouseWidth = 128;
        private const int HouseDepth = 128;
        private const uint StructureId = 0x56414C49u;

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private GuildHousePrototype _prototype;
        private GuildHouseDescriptor[] _houses = Array.Empty<GuildHouseDescriptor>();
        private float _started;
        private int _phase;
        private int _rebuilds;
        private int _activeWorlds;
        private uint _knightSeed = 0x22002200u;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _camera = GetComponent<Camera>();
            _houses = GuildHouseCatalogQuery.Houses();
            if (_houses.Length != 10)
                Fail($"expected 10 production guild houses, found {_houses.Length}");

            ConfigurePresentation();
            GuildHouseDescriptor wizard = FindHouse(GuildHouseKind.Wizards);
            GuildHouseDescriptor knight = FindHouse(GuildHouseKind.Knights);
            AssertDifferentApplicableLists(wizard.Kind, knight.Kind);

            Build(wizard, 0x11001100u, "wizard-exterior");
            _started = Time.unscaledTime;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            ShutdownWorld();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            float elapsed = Time.unscaledTime - _started;

            if (_phase == 0 && elapsed >= 3f)
            {
                FrameInterior();
                Debug.Log($"HOUSE_FURNISHING_VALIDATION interior house={FindHouse(GuildHouseKind.Wizards).Key} seed=285217024 activeWorlds={_activeWorlds}");
                _phase = 1;
            }
            else if (_phase == 1 && elapsed >= 6f)
            {
                Build(FindHouse(GuildHouseKind.Knights), _knightSeed, "knight-exterior");
                _phase = 2;
            }
            else if (_phase == 2 && elapsed >= 10f)
            {
                GuildHousePrototype baseline = _prototype;
                uint changedSeed = FindDifferentSeed(GuildHouseKind.Knights, _knightSeed, in baseline);
                _knightSeed = changedSeed;
                Build(FindHouse(GuildHouseKind.Knights), changedSeed, "knight-regenerated");
                bool changed = !SameSpatialSignature(in baseline, in _prototype);
                if (!changed) Fail("regeneration did not change the production spatial signature");
                Debug.Log($"HOUSE_FURNISHING_VALIDATION regenerated house=knights seed={changedSeed} changed=true activeWorlds={_activeWorlds}");
                _phase = 3;
            }
            else if (_phase == 3 && elapsed >= 13f)
            {
                FrameInterior();
                Debug.Log($"HOUSE_FURNISHING_VALIDATION complete rebuilds={_rebuilds} activeWorlds={_activeWorlds} cleanup=true");
                _phase = 4;
            }
        }

        private void Build(GuildHouseDescriptor house, uint seed, string phase)
        {
            ShutdownWorld();
            if (_activeWorlds != 0 || _storage != null)
                Fail("previous production world survived teardown");

            if (!GuildHouseCatalogQuery.TryGetFurnishings(house.Kind, out GuildHouseFurnishingOption[] options))
                Fail($"furnishing query failed for {house.Key}");
            ushort[] selected = SelectFirstOptional(options, 6);
            if (selected.Length < 2)
                Fail($"{house.Key} exposes too few optional furnishings for multi-select validation");
            if (!GuildHouseFurnishingPalette.TryCreate(house.Kind, selected, out GuildHouseFurnishingPalette palette))
                Fail($"production palette rejected {house.Key} validation selection");

            _prototype = GuildHousePrototypeComposition.Build(
                house.Kind,
                DecorationRegionTheme.Kentridge,
                seed,
                StructureId + (uint)_rebuilds,
                new int3(0, 16, 0),
                HouseWidth,
                HouseDepth,
                house.PreferredRooms);
            if (!_prototype.IsWellFormed)
                Fail($"production prototype failed for {house.Key}");

            Stopwatch timer = Stopwatch.StartNew();
            _storage = VoxelEngineBootstrap.CreateStorage(16, 64_000);
            RegisterMaterials(_storage);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 8_000_000);
            if (!GuildHouseFurnishedPrototypeAuthoring.TryAuthor(
                    authoring,
                    in _prototype,
                    in palette,
                    out GuildHouseUnplacedFurnishing[] unplaced))
                Fail($"production furnished authoring failed for {house.Key}");
            if (authoring.BudgetExceeded)
                Fail($"production authoring budget exceeded for {house.Key}");

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world,
                _storage.Changes,
                seed,
                solidBuildBudgetMs: 12.0,
                waterBuildBudgetMs: 0.0,
                farFieldEnabled: false);
            _activeWorlds = 1;
            _rebuilds++;
            timer.Stop();
            FrameExterior();

            for (int i = 0; i < unplaced.Length; i++)
                if (!unplaced[i].IsWellFormed)
                    Fail($"malformed unplaced diagnostic for {house.Key}");

            Debug.Log(
                $"HOUSE_FURNISHING_VALIDATION build phase={phase} house={house.Key} seed={seed} " +
                $"options={options.Length} selected={selected.Length} unplaced={unplaced.Length} " +
                $"voxels={authoring.TotalVoxelsWritten} rebuildMs={timer.ElapsedMilliseconds} activeWorlds={_activeWorlds}");
        }

        private void ShutdownWorld()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _activeWorlds = 0;
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
            if (_camera == null || !_prototype.IsWellFormed) return;
            Bounds3 bounds = PrototypeBounds(in _prototype);
            Vector3 focus = ToWorld((bounds.Min + bounds.MaxExclusive) * 0.5f);
            _camera.transform.position = focus + new Vector3(-16f, 10f, -16f);
            _camera.transform.LookAt(focus);
        }

        private void FrameInterior()
        {
            if (_camera == null || !_prototype.IsWellFormed || _prototype.Rooms.Length == 0) return;
            GuildHouseSpatialRoom room = _prototype.Rooms[0].SpatialRoom;
            float3 centre = ((float3)room.Min + (float3)room.MaxExclusive) * (0.5f * VoxelSize);
            _camera.transform.position = new Vector3(
                centre.x,
                room.Min.y * VoxelSize + 1.45f,
                centre.z);
            _camera.transform.rotation = Quaternion.Euler(0f, 42f, 0f);
        }

        private static void RegisterMaterials(IVoxelStorageRuntime storage)
        {
            MaterialDefinition[] materials = GameMaterialComposition.SimulationDefinitions();
            for (int i = 0; i < materials.Length; i++)
            {
                MaterialDefinition material = materials[i];
                storage.RegisterMaterial(
                    material.MaterialId,
                    material.Hardness,
                    material.DestructionClass,
                    material.DefaultSurfaceStyle,
                    material.AllowedCoatings);
            }
        }

        private GuildHouseDescriptor FindHouse(GuildHouseKind kind)
        {
            for (int i = 0; i < _houses.Length; i++)
                if (_houses[i].Kind == kind)
                    return _houses[i];
            Fail($"missing production house registration for {kind}");
            return default;
        }

        private static ushort[] SelectFirstOptional(GuildHouseFurnishingOption[] options, int limit)
        {
            var selected = new List<ushort>(limit);
            for (int i = 0; i < options.Length && selected.Count < limit; i++)
                if (options[i].Selectable)
                    selected.Add(options[i].Decoration.StableId);
            return selected.ToArray();
        }

        private static void AssertDifferentApplicableLists(GuildHouseKind first, GuildHouseKind second)
        {
            if (!GuildHouseCatalogQuery.TryGetFurnishings(first, out GuildHouseFurnishingOption[] a) ||
                !GuildHouseCatalogQuery.TryGetFurnishings(second, out GuildHouseFurnishingOption[] b))
                Fail("could not query production applicability lists");

            var aIds = new HashSet<ushort>();
            var bIds = new HashSet<ushort>();
            for (int i = 0; i < a.Length; i++) if (a[i].Selectable) aIds.Add(a[i].Decoration.StableId);
            for (int i = 0; i < b.Length; i++) if (b[i].Selectable) bIds.Add(b[i].Decoration.StableId);
            if (aIds.SetEquals(bIds))
                Fail($"{first} and {second} unexpectedly expose identical optional furnishing sets");
            Debug.Log($"HOUSE_FURNISHING_VALIDATION applicability first={first} count={aIds.Count} second={second} count={bIds.Count} different=true");
        }

        private static uint FindDifferentSeed(
            GuildHouseKind kind,
            uint baselineSeed,
            in GuildHousePrototype baseline)
        {
            for (uint delta = 1; delta <= 64; delta++)
            {
                uint candidateSeed = unchecked(baselineSeed + delta * 0x9E3779B9u);
                GuildHousePrototype candidate = GuildHousePrototypeComposition.Build(
                    kind,
                    DecorationRegionTheme.Kentridge,
                    candidateSeed,
                    StructureId + 99u,
                    new int3(0, 16, 0),
                    HouseWidth,
                    HouseDepth,
                    baseline.SpatialPlan.Rooms.Length);
                if (candidate.IsWellFormed && !SameSpatialSignature(in baseline, in candidate))
                    return candidateSeed;
            }
            Fail("could not find seed-driven production spatial variation within 64 candidates");
            return baselineSeed;
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

        private readonly struct Bounds3
        {
            public readonly float3 Min;
            public readonly float3 MaxExclusive;
            public Bounds3(float3 min, float3 maxExclusive)
            {
                Min = min;
                MaxExclusive = maxExclusive;
            }
        }

        private static Bounds3 PrototypeBounds(in GuildHousePrototype prototype)
        {
            int3 min = prototype.Rooms[0].SpatialRoom.Min;
            int3 max = prototype.Rooms[0].SpatialRoom.MaxExclusive;
            for (int i = 1; i < prototype.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom room = prototype.Rooms[i].SpatialRoom;
                min = math.min(min, room.Min);
                max = math.max(max, room.MaxExclusive);
            }
            return new Bounds3((float3)min, (float3)max);
        }

        private static Vector3 ToWorld(float3 voxels) =>
            new Vector3(voxels.x * VoxelSize, voxels.y * VoxelSize, voxels.z * VoxelSize);

        private static void Fail(string message)
        {
            Debug.LogError("HOUSE_FURNISHING_VALIDATION failure: " + message);
            throw new InvalidOperationException(message);
        }
    }
}
