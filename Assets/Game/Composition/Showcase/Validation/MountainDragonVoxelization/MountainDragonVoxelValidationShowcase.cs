using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Composition;
using VoxelEngine.Terrain.Api;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Module-owned standalone-player validation consumer for the checked-in Mountain Dragon bake.
    /// It invokes the production ShowcaseWorld placement/storage and rendering seams; no source mesh,
    /// collider, test-only voxel geometry, or alternate renderer participates in world truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MountainDragonVoxelValidationShowcase : MonoBehaviour
    {
        private const uint EvidenceSeed = 0x5EED1234u;
        private const int DragonOriginX = 160;
        private const int DragonOriginZ = 160;
        private ShowcaseWorld _world;
        private Camera _camera;
        private Light _sun;

        private void Awake()
        {
            long started = Stopwatch.GetTimestamp();
            _world = new ShowcaseWorld(
                EvidenceSeed,
                brickPoolCapacity: 4096,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            int groundY = TerrainQuery.HeightAt(DragonOriginX, DragonOriginZ, EvidenceSeed) + 1;
            int3 origin = new int3(DragonOriginX, groundY, DragonOriginZ);
            MeshStructurePlacementResult placement = _world.PlaceMountainDragon(origin);

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

            ConfigureCamera(origin);
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
                + " world_origin=" + origin.x + "," + origin.y + "," + origin.z);
        }

        private void ConfigureCamera(int3 origin)
        {
            GameObject cameraObject = new GameObject("Mountain Dragon Validation Camera");
            _camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            _camera.fieldOfView = 42f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 300f;
            _camera.clearFlags = CameraClearFlags.Skybox;

            Vector3 target = new Vector3(
                (origin.x + 49.5f) * ShowcaseWorld.VoxelSize,
                (origin.y + 51f) * ShowcaseWorld.VoxelSize,
                (origin.z + 53.5f) * ShowcaseWorld.VoxelSize);
            cameraObject.transform.position = target + new Vector3(0f, 2.2f, -22f);
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
            GUI.Box(new Rect(18f, 18f, 430f, 86f), GUIContent.none);
            GUI.Label(new Rect(32f, 30f, 400f, 24f), "Mountain Dragon — voxel runtime validation");
            GUI.Label(new Rect(32f, 56f, 400f, 36f),
                "Pinned bake → ShowcaseWorld → authoritative voxel storage → production renderer");
        }

        private void OnDestroy()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.Dispose();
            _world = null;
            if (_camera != null) Destroy(_camera.gameObject);
            if (_sun != null) Destroy(_sun.gameObject);
        }
    }
}
