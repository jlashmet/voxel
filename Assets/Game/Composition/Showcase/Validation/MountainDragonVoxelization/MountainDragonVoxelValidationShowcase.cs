using System;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Terrain.Api;
using VoxelEngine.Structures.Runtime.MeshImport;
using VoxelEngine.Tiering.Api;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Module-owned standalone-player comparison for the checked-in Mountain Dragon bake.
    /// The source mesh is presentation-only; the voxel side invokes production ShowcaseWorld
    /// placement/storage/rendering and remains the sole collision/edit/destruction authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MountainDragonVoxelValidationShowcase : MonoBehaviour
    {
        public const string SourceResourcePath =
            "VoxelShowcase/MountainDragonSource/mountain_dragon_clean";

        private const uint EvidenceSeed = 0x5EED1234u;
        private const int VoxelOriginX = 220;
        private const int ExhibitOriginZ = 160;
        private const int MinimumSourceSeparationVoxels = 700;
        // Harness screenshots arrive every three seconds. Offset transitions to the midpoint
        // between captures so each durable frame owns exactly one semantic comparison view.
        private const float FirstViewSeconds = 1.5f;
        private const float LaterViewSeconds = 3f;
        // Hold the tenth pristine view long enough for one complete harness capture before the
        // supplemental destruction proof changes authoritative voxel state.
        private const float DestructionDelayAfterFinalViewSeconds = 4.5f;
        private const int DestructionRadiusVoxels = 8;

        private ShowcaseWorld _world;
        private Camera _sourceCamera;
        private Camera _voxelCamera;
        private Light _sun;
        private GameObject _sourceDragon;
        private MeshVoxelCaptureView[] _views;
        private int _viewIndex;
        private float _nextViewTime;
        private float _destructionTime = float.PositiveInfinity;
        private bool _destructionComplete;
        private int3 _sourceOrigin;
        private int3 _voxelOrigin;
        private int3 _bakeSize;
        private int3 _destructionLocalCell;
        private int _sourceColliderCount;

        private void Awake()
        {
            GameObject sourcePrefab = Resources.Load<GameObject>(SourceResourcePath);
            if (sourcePrefab == null)
                throw new InvalidOperationException(
                    "Mountain Dragon validation source resource is missing. The Editor build preprocessor must reconstruct it.");

            long started = Stopwatch.GetTimestamp();

            // The first two built-player failures established the actual boundary: 4,096 bricks
            // exhausted while generating the production terrain region, while 16,384 completed
            // terrain and then exhausted inside BakedVoxelStructure.ReplayTo. Do not guess another
            // fixture-only slot count. Exercise the same tier-byte authority as VoxelShowcase and
            // let Storage convert that budget to the current mixed-brick layout.
            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int brickPoolCapacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                int.MaxValue, tierBytes);
            _world = new ShowcaseWorld(
                EvidenceSeed,
                brickPoolCapacity,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2,
                maxMixedBrickAllocationBytes: tierBytes);

            int groundY = TerrainQuery.HeightAt(VoxelOriginX, ExhibitOriginZ, EvidenceSeed) + 1;
            int sourceOriginX = FindEqualHeightSourceOriginX(groundY - 1);
            _sourceOrigin = new int3(sourceOriginX, groundY, ExhibitOriginZ);
            _voxelOrigin = new int3(VoxelOriginX, groundY, ExhibitOriginZ);

            // The source half needs the same production terrain contact, but no source geometry
            // is ever authored into this world. Generate only the terrain region beneath it.
            _world.GenerateRegionBlocking(VoxelToRegion(_sourceOrigin));

            BakedVoxelStructure bake = MountainDragonBakedArtifact.Load();
            _bakeSize = bake.Size;
            _destructionLocalCell = SelectTorsoDestructionCell(bake);
            MeshStructurePlacementResult placement = _world.PlaceMountainDragon(_voxelOrigin);
            StageSourceMesh(sourcePrefab, bake, _sourceOrigin);

            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ResetSurfacePassDiagnostics("mountain-dragon-module-validation");
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.ConfigureWorld(
                in renderingWorld, _world.Changes, _world.Seed, farFieldEnabled: false);

            ConfigureCameras();
            ConfigureLighting();
            _views = MeshVoxelComparisonCapturePlan.CreateRequiredViews();
            if (_views.Length != 10)
                throw new InvalidOperationException(
                    "Mountain Dragon validation requires the semantic ten-view capture plan.");
            _viewIndex = 0;
            ApplyView(_views[_viewIndex]);
            _nextViewTime = Time.realtimeSinceStartup + FirstViewSeconds;

            StoragePressure pressure = _world.StoragePressure;
            Debug.Log(
                "MOUNTAIN_DRAGON_VALIDATION_COST placement_ms=" + placement.PlacementMilliseconds.ToString("F3")
                + " requested_voxels=" + placement.VoxelsRequested
                + " written_voxels=" + placement.VoxelsWritten
                + " regions_prepared=" + placement.RegionsPrepared
                + " brick_pool_capacity=" + brickPoolCapacity
                + " tier_budget_bytes=" + tierBytes
                + " storage_used_bytes=" + pressure.UsedBytes
                + " storage_capacity_bytes=" + pressure.CapacityBytes
                + " storage_critical_bytes=" + pressure.CriticalLimitBytes
                + " storage_under_pressure=" + pressure.IsUnderPressure
                + " allocated_bytes=" + Profiler.GetTotalAllocatedMemoryLong()
                + " reserved_bytes=" + Profiler.GetTotalReservedMemoryLong()
                + " setup_ticks=" + (Stopwatch.GetTimestamp() - started));
            Debug.Log(
                "MOUNTAIN_DRAGON_VALIDATION ready: source_triangles="
                + MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount
                + " authored_voxels=" + MountainDragonBakedArtifact.ExpectedCellCount
                + " source_origin=" + _sourceOrigin.x + "," + _sourceOrigin.y + "," + _sourceOrigin.z
                + " voxel_origin=" + _voxelOrigin.x + "," + _voxelOrigin.y + "," + _voxelOrigin.z
                + " source_colliders_disabled=" + _sourceColliderCount);
        }

        private void Update()
        {
            if (_views == null) return;

            if (_viewIndex < _views.Length - 1 && Time.realtimeSinceStartup >= _nextViewTime)
            {
                _viewIndex++;
                ApplyView(_views[_viewIndex]);
                _nextViewTime += LaterViewSeconds;
                if (_viewIndex == _views.Length - 1)
                    _destructionTime = Time.realtimeSinceStartup
                                     + DestructionDelayAfterFinalViewSeconds;
                return;
            }

            if (!_destructionComplete && _viewIndex == _views.Length - 1
                && Time.realtimeSinceStartup >= _destructionTime)
                RunDestructionWorldTruthProof();
        }

        private int FindEqualHeightSourceOriginX(int targetTerrainY)
        {
            for (int offset = MinimumSourceSeparationVoxels; offset <= 4096; offset++)
            {
                int left = VoxelOriginX - offset;
                if (TerrainQuery.HeightAt(left, ExhibitOriginZ, EvidenceSeed) == targetTerrainY)
                    return left;
                int right = VoxelOriginX + offset;
                if (TerrainQuery.HeightAt(right, ExhibitOriginZ, EvidenceSeed) == targetTerrainY)
                    return right;
            }
            throw new InvalidOperationException(
                "Mountain Dragon validation could not find separated production terrain at the matched contact height.");
        }

        private void StageSourceMesh(GameObject sourcePrefab, BakedVoxelStructure bake, int3 sourceOrigin)
        {
            _sourceDragon = Instantiate(sourcePrefab);
            _sourceDragon.name = "Source Mesh — presentation only";

            float scale = ShowcaseWorld.VoxelSize / bake.VoxelSize;
            _sourceDragon.transform.localScale = Vector3.one * scale;
            int3 gridOrigin = bake.GridOrigin;
            _sourceDragon.transform.position = new Vector3(
                (sourceOrigin.x - gridOrigin.x) * ShowcaseWorld.VoxelSize,
                (sourceOrigin.y - gridOrigin.y) * ShowcaseWorld.VoxelSize,
                (sourceOrigin.z - gridOrigin.z) * ShowcaseWorld.VoxelSize);
            _sourceDragon.transform.rotation = Quaternion.identity;

            Collider[] colliders = _sourceDragon.GetComponentsInChildren<Collider>(includeInactive: true);
            _sourceColliderCount = colliders.Length;
            for (int i = 0; i < colliders.Length; i++)
            {
                // Destroy is deferred until end of frame. Disable immediately so there is never a
                // frame where presentation geometry can masquerade as collision truth.
                colliders[i].enabled = false;
                Destroy(colliders[i]);
            }
        }

        private void ConfigureCameras()
        {
            // VoxelRenderPass owns one VoxelSurfaceScheduler. The first camera rendered in a
            // frame advances clipmap discovery/build/upload; later cameras collect visibility
            // only. Make the authoritative voxel camera explicitly first so production surface
            // work is admitted around voxel truth. The source camera needs only ordinary mesh
            // rasterization and renders second in its independent left viewport.
            _voxelCamera = CreateCamera("Voxelized Camera", new Rect(0.5f, 0f, 0.5f, 1f));
            _voxelCamera.depth = -10f;
            _voxelCamera.gameObject.tag = "MainCamera";

            _sourceCamera = CreateCamera("Source Mesh Camera", new Rect(0f, 0f, 0.5f, 1f));
            _sourceCamera.depth = 10f;
        }

        private static Camera CreateCamera(string name, Rect viewport)
        {
            GameObject cameraObject = new GameObject(name);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.rect = viewport;
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.Skybox;
            return camera;
        }

        private void ApplyView(MeshVoxelCaptureView view)
        {
            float3 localTarget = CaptureLocalTarget(view.Subject);
            float distance = view.Subject == MeshVoxelCaptureSubject.Overall ? 21f : 12f;
            PositionCamera(_sourceCamera, WorldPoint(_sourceOrigin, localTarget), view.ViewDirection, distance);
            PositionCamera(_voxelCamera, WorldPoint(_voxelOrigin, localTarget), view.ViewDirection, distance);
            Debug.Log("MOUNTAIN_DRAGON_CAPTURE view=" + view.Id + " index=" + _viewIndex);
        }

        private void RunDestructionWorldTruthProof()
        {
            _destructionComplete = true;
            int3 target = _voxelOrigin + _destructionLocalCell;
            if (!IsBlockingVoxel(target, out VoxelCell before))
                throw new InvalidOperationException(
                    "Mountain Dragon destruction target was not occupied before the production edit.");

            int changedVoxels = _world.Explode(
                target,
                DestructionRadiusVoxels,
                new float3(0f, 0f, -1f));
            if (changedVoxels <= 0)
                throw new InvalidOperationException(
                    "Mountain Dragon production destruction did not mutate canonical local world state.");

            bool blockedAfter = IsBlockingVoxel(target, out VoxelCell after);
            if (blockedAfter)
                throw new InvalidOperationException(
                    "Mountain Dragon destruction target remained collision-blocking after the canonical edit.");

            Collider[] sourceColliders = _sourceDragon != null
                ? _sourceDragon.GetComponentsInChildren<Collider>(includeInactive: true)
                : Array.Empty<Collider>();
            for (int i = 0; i < sourceColliders.Length; i++)
            {
                if (sourceColliders[i] != null && sourceColliders[i].enabled)
                    throw new InvalidOperationException(
                        "Presentation-only Mountain Dragon source retained an enabled collider.");
            }

            // Keep the source intact as the explicit authoring reference while centring both
            // cameras on the edited torso. The right side must show only the derived canonical
            // voxel hole; no source mesh exists there to hide the edit.
            float3 localTarget = _destructionLocalCell;
            float3 direction = math.normalize(new float3(-0.65f, -0.18f, -1f));
            PositionCamera(_sourceCamera, WorldPoint(_sourceOrigin, localTarget), direction, 8f);
            PositionCamera(_voxelCamera, WorldPoint(_voxelOrigin, localTarget), direction, 8f);

            StoragePressure pressure = _world.StoragePressure;
            Debug.Log(
                "MOUNTAIN_DRAGON_DESTRUCTION_WORLD_TRUTH target="
                + target.x + "," + target.y + "," + target.z
                + " before_material=" + before.BaseMaterialId
                + " after_material=" + after.BaseMaterialId
                + " changed_voxels=" + changedVoxels
                + " collision_blocked_before=True"
                + " collision_blocked_after=False"
                + " source_enabled_colliders=0"
                + " storage_used_bytes=" + pressure.UsedBytes
                + " storage_capacity_bytes=" + pressure.CapacityBytes
                + " storage_under_pressure=" + pressure.IsUnderPressure);
        }

        private bool IsBlockingVoxel(int3 voxel, out VoxelCell cell)
        {
            bool exists = _world.SurfaceQuery.TryRead(voxel, out cell);
            return exists && cell.BaseMaterialId != VoxelGrid.MaterialEmpty;
        }

        private static int3 SelectTorsoDestructionCell(BakedVoxelStructure bake)
        {
            int3 min = new int3(35, 40, 35);
            int3 max = new int3(70, 75, 75);
            float3 centre = ((float3)min + max) * 0.5f;
            int bestIndex = -1;
            float bestDistanceSq = float.PositiveInfinity;
            for (int i = 0; i < bake.Cells.Length; i++)
            {
                int3 position = bake.Cells[i].Position;
                if (math.any(position < min) || math.any(position >= max)) continue;
                float distanceSq = math.lengthsq((float3)position - centre);
                if (distanceSq >= bestDistanceSq) continue;
                bestDistanceSq = distanceSq;
                bestIndex = i;
            }

            if (bestIndex < 0)
                throw new InvalidOperationException(
                    "Mountain Dragon canonical bake has no occupied torso cell for destruction validation.");
            return bake.Cells[bestIndex].Position;
        }

        private static void PositionCamera(Camera camera, Vector3 target, float3 viewDirection, float distance)
        {
            Vector3 forward = new Vector3(viewDirection.x, viewDirection.y, viewDirection.z).normalized;
            camera.transform.position = target - forward * distance;
            camera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private float3 CaptureLocalTarget(MeshVoxelCaptureSubject subject)
        {
            switch (subject)
            {
                case MeshVoxelCaptureSubject.HeadHorns:
                    return new float3(62.5f, 50f, 91f);
                case MeshVoxelCaptureSubject.Wing:
                    return new float3(17.5f, 84.5f, 81f);
                case MeshVoxelCaptureSubject.FeetClaws:
                    return new float3(34f, 26.5f, 62f);
                case MeshVoxelCaptureSubject.Tail:
                    return new float3(50f, 85f, 27.5f);
                default:
                    return new float3(_bakeSize.x, _bakeSize.y, _bakeSize.z) * 0.5f;
            }
        }

        private static Vector3 WorldPoint(int3 origin, float3 local)
        {
            float3 voxel = (float3)origin + local;
            return new Vector3(voxel.x, voxel.y, voxel.z) * ShowcaseWorld.VoxelSize;
        }

        private static int3 VoxelToRegion(int3 voxel) =>
            (int3)math.floor((float3)voxel / ShowcaseWorld.RegionVoxelEdge);

        private void ConfigureLighting()
        {
            GameObject lightObject = new GameObject("Mountain Dragon Validation Sun");
            _sun = lightObject.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 760f, 104f), GUIContent.none);
            GUI.Label(new Rect(32f, 28f, 730f, 24f), "Mesh -> Voxels — Mountain Dragon module validation");
            GUI.Label(new Rect(32f, 54f, 730f, 22f),
                "LEFT: Source Mesh (presentation only)     RIGHT: Voxelized (authoritative world data)");
            string view = _destructionComplete
                ? "post-destruction torso (canonical edit)"
                : _views != null && _viewIndex < _views.Length ? _views[_viewIndex].Id : "starting";
            GUI.Label(new Rect(32f, 78f, 730f, 28f),
                "Matched pose / scale / orientation / terrain contact — view: " + view);
        }

        private void OnDestroy()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.Dispose();
            _world = null;
            if (_sourceDragon != null) Destroy(_sourceDragon);
            if (_sourceCamera != null) Destroy(_sourceCamera.gameObject);
            if (_voxelCamera != null) Destroy(_voxelCamera.gameObject);
            if (_sun != null) Destroy(_sun.gameObject);
        }
    }
}