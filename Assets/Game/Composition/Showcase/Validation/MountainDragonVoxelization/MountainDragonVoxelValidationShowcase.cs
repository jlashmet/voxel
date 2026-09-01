using System;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Composition;
using VoxelEngine.Terrain.Api;
using VoxelEngine.Structures.Runtime.MeshImport;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Module-owned standalone-player validation consumer for the checked-in Mountain Dragon bake.
    /// The source mesh is presentation-only; the voxel side invokes production ShowcaseWorld
    /// placement/storage/rendering and remains the sole collision/edit/destruction authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MountainDragonVoxelValidationShowcase : MonoBehaviour
    {
        private const uint EvidenceSeed = 0x5EED1234u;
        private const int SourceOriginX = 90;
        private const int VoxelOriginX = 235;
        private const int ExhibitOriginZ = 160;

        [SerializeField] private GameObject m_SourceDragonPrefab;

        private ShowcaseWorld _world;
        private Camera _camera;
        private Light _sun;
        private GameObject _sourceDragon;

        private void Awake()
        {
            if (m_SourceDragonPrefab == null)
                throw new InvalidOperationException(
                    "Mountain Dragon validation requires the exact Editor-reconstructed source OBJ.");

            long started = Stopwatch.GetTimestamp();
            _world = new ShowcaseWorld(
                EvidenceSeed,
                brickPoolCapacity: 4096,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            int groundY = Math.Max(
                TerrainQuery.HeightAt(SourceOriginX, ExhibitOriginZ, EvidenceSeed),
                TerrainQuery.HeightAt(VoxelOriginX, ExhibitOriginZ, EvidenceSeed)) + 1;
            int3 sourceOrigin = new int3(SourceOriginX, groundY, ExhibitOriginZ);
            int3 voxelOrigin = new int3(VoxelOriginX, groundY, ExhibitOriginZ);

            BakedVoxelStructure bake = MountainDragonBakedArtifact.Load();
            MeshStructurePlacementResult placement = _world.PlaceMountainDragon(voxelOrigin);
            StageSourceMesh(bake, sourceOrigin);

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

            ConfigureCamera(sourceOrigin, voxelOrigin, bake.Size);
            ConfigureLighting();

            Debug.Log(
                "MOUNTAIN_DRAGON_VALIDATION_COST placement_ms=" + placement.PlacementMilliseconds.ToString("F3")
                + " requested_voxels=" + placement.VoxelsRequested
                + " written_voxels=" + placement.VoxelsWritten
                + " regions_prepared=" + placement.RegionsPrepared
                + " allocated_bytes=" + Profiler.GetTotalAllocatedMemoryLong()
                + " reserved_bytes=" + Profiler.GetTotalReservedMemoryLong()
                + " setup_ticks=" + (Stopwatch.GetTimestamp() - started));
            Debug.Log(
                "MOUNTAIN_DRAGON_VALIDATION ready: source_triangles="
                + MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount
                + " authored_voxels=" + MountainDragonBakedArtifact.ExpectedCellCount
                + " source_origin=" + sourceOrigin.x + "," + sourceOrigin.y + "," + sourceOrigin.z
                + " voxel_origin=" + voxelOrigin.x + "," + voxelOrigin.y + "," + voxelOrigin.z);
        }

        private void StageSourceMesh(BakedVoxelStructure bake, int3 sourceOrigin)
        {
            _sourceDragon = Instantiate(m_SourceDragonPrefab);
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
            for (int i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);
        }

        private void ConfigureCamera(int3 sourceOrigin, int3 voxelOrigin, int3 bakeSize)
        {
            GameObject cameraObject = new GameObject("Mountain Dragon Validation Camera");
            _camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            _camera.fieldOfView = 42f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 300f;
            _camera.clearFlags = CameraClearFlags.Skybox;

            float centreX = (sourceOrigin.x + voxelOrigin.x + bakeSize.x) * 0.5f * ShowcaseWorld.VoxelSize;
            Vector3 target = new Vector3(
                centreX,
                (voxelOrigin.y + bakeSize.y * 0.48f) * ShowcaseWorld.VoxelSize,
                (voxelOrigin.z + bakeSize.z * 0.5f) * ShowcaseWorld.VoxelSize);
            cameraObject.transform.position = target + new Vector3(0f, 2.8f, -29f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                (target - cameraObject.transform.position).normalized, Vector3.up);
        }

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
            GUI.Box(new Rect(18f, 18f, 650f, 96f), GUIContent.none);
            GUI.Label(new Rect(32f, 28f, 620f, 24f), "Mesh -> Voxels — Mountain Dragon module validation");
            GUI.Label(new Rect(32f, 54f, 620f, 22f),
                "LEFT: Source Mesh (presentation only)     RIGHT: Voxelized (authoritative world data)");
            GUI.Label(new Rect(32f, 78f, 620f, 24f),
                "Matched pose, effective scale, orientation and contact height; source colliders removed.");
        }

        private void OnDestroy()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.Dispose();
            _world = null;
            if (_sourceDragon != null) Destroy(_sourceDragon);
            if (_camera != null) Destroy(_camera.gameObject);
            if (_sun != null) Destroy(_sun.gameObject);
        }
    }
}
