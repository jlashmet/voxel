using Game.Composition.Materials;
using Game.Materials.Api;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Production-path side-by-side viewer for Kentridge's pre-restyle and current structures.
    /// The pair is rebuilt from one role and seed on a level stage, so presentation differences
    /// cannot come from camera, terrain, lighting, material, orientation, or placement drift.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class KentridgeStructureComparisonShowcase : MonoBehaviour
    {
        private const uint Seed = 0x4B454E54u;
        private const float VoxelSize = 0.1f;
        private const int StageDepthVoxels = 6;
        private const float LabelWidth = 180f;

        [SerializeField, Range(0, KentridgeStructureComparisonCatalogue.RoleCount - 1)]
        private int m_RoleId = 1;

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private Vector3 _focus;
        private float _yaw;
        private float _pitch = 12f;
        private float _distance;
        private Vector3 _originalCentre;
        private Vector3 _modifiedCentre;
        private bool _built;
        private string _status;

        public int SelectedRole => m_RoleId;
        public int RoleCount => KentridgeStructureComparisonCatalogue.RoleCount;
        public bool IsBuilt => _built;
        public string SelectedRoleName =>
            KentridgeStructureComparisonCatalogue.RoleDisplayName(m_RoleId);

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _camera = GetComponent<Camera>();
            RebuildComparison(m_RoleId);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            Shutdown();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                RebuildComparison((m_RoleId + RoleCount - 1) % RoleCount);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                RebuildComparison((m_RoleId + 1) % RoleCount);
            if (Input.GetKeyDown(KeyCode.R)) RebuildComparison(m_RoleId);

            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * 3.5f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 3.5f, -25f, 75f);
            }
            if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
                _distance = Mathf.Clamp(
                    _distance * Mathf.Exp(-Input.mouseScrollDelta.y * 0.1f), 12f, 120f);

            ApplyCamera();
            if (RenderingComposition.TryGetSurfaceBuildStatus(
                    out int known, out int dirty, out int resident, out long bytes))
            {
                _status = dirty == 0 && resident >= known
                    ? $"READY  {bytes / (1024f * 1024f):0.0} MB"
                    : $"MESHING  {resident}/{known}";
            }
        }

        public void RebuildComparison(int roleId)
        {
            m_RoleId = (roleId % RoleCount + RoleCount) % RoleCount;
            Shutdown();
            ConfigurePresentation();

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

            FeatureCatalogue pair = KentridgeStructureComparisonCatalogue.Build(
                Seed, BuildSettings(), m_RoleId, Allocator.Temp);
            try
            {
                AuthorStages(in pair);
                FeatureCatalogueBuildResult result =
                    StructuresComposition.BuildExplicitFeatureCatalogue(_storage, in pair, Seed);
                if (result.InstancesRasterised < 2 || result.VoxelsWritten <= 0)
                    throw new System.InvalidOperationException(
                        "The comparison scene did not rasterise both structure variants. " +
                        $"regions={result.RegionsVisited}, " +
                        $"instances={result.InstancesRasterised}, " +
                        $"voxels={result.VoxelsWritten}.");

                FramePair(in pair);
                _status = $"AUTHORED  {result.VoxelsWritten:N0} voxels";
            }
            finally
            {
                pair.Dispose();
            }

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world, _storage.Changes, Seed,
                solidBuildBudgetMs: 12.0, waterBuildBudgetMs: 0.0,
                farFieldEnabled: false);
            _built = true;
        }

        private void AuthorStages(in FeatureCatalogue pair)
        {
            IStructureAuthoringSession writer =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 4_000_000);
            for (int i = 0; i < pair.Definitions.Length; i++)
            {
                FeatureDefinition definition = pair.Definitions[i];
                ExplicitPlacement placement = pair.ExplicitPlacements[i];
                int minX = placement.Position.x - KentridgeStructureComparisonCatalogue.StageMarginVoxels / 2;
                int maxX = placement.Position.x + definition.Footprint.x
                         + KentridgeStructureComparisonCatalogue.StageMarginVoxels / 2;
                int minZ = placement.Position.z - KentridgeStructureComparisonCatalogue.StageMarginVoxels / 2;
                int maxZ = placement.Position.z + definition.Footprint.z
                         + KentridgeStructureComparisonCatalogue.StageMarginVoxels / 2;
                for (int z = minZ; z < maxZ; z++)
                for (int x = minX; x < maxX; x++)
                    writer.FillColumnBulk(
                        x,
                        KentridgeStructureComparisonCatalogue.StageAltitudeVoxels - StageDepthVoxels,
                        KentridgeStructureComparisonCatalogue.StageAltitudeVoxels - 1,
                        z,
                        GameMaterialIds.TerrainPathStone);
            }
            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Comparison stage exceeded its voxel budget.");
        }

        private void FramePair(in FeatureCatalogue pair)
        {
            FeatureDefinition original = pair.Definitions[0];
            FeatureDefinition modified = pair.Definitions[1];
            int3 originalOrigin = pair.ExplicitPlacements[0].Position;
            int3 modifiedOrigin = pair.ExplicitPlacements[1].Position;
            float baseY = KentridgeStructureComparisonCatalogue.StageAltitudeVoxels * VoxelSize;

            _originalCentre = new Vector3(
                (originalOrigin.x + original.Footprint.x * 0.5f) * VoxelSize,
                baseY + original.Footprint.y * VoxelSize,
                originalOrigin.z * VoxelSize);
            _modifiedCentre = new Vector3(
                (modifiedOrigin.x + modified.Footprint.x * 0.5f) * VoxelSize,
                baseY + modified.Footprint.y * VoxelSize,
                modifiedOrigin.z * VoxelSize);

            float maxX = modifiedOrigin.x + modified.Footprint.x
                       + KentridgeStructureComparisonCatalogue.StageMarginVoxels;
            float maxZ = math.max(original.Footprint.z, modified.Footprint.z)
                       + 2 * KentridgeStructureComparisonCatalogue.StageMarginVoxels;
            float maxY = math.max(original.Footprint.y, modified.Footprint.y);
            _focus = new Vector3(maxX * VoxelSize * 0.5f,
                baseY + maxY * VoxelSize * 0.42f,
                maxZ * VoxelSize * 0.42f);
            _yaw = 0f;
            _pitch = 12f;
            _distance = Mathf.Max(24f, maxX * VoxelSize * 0.82f);
            ApplyCamera();
        }

        private void ConfigurePresentation()
        {
            GameMaterialComposition.Install();
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetVoxelLodEnabled(false);
            RenderingComposition.SetSky(
                new Color(0.67f, 0.76f, 0.82f, 1f),
                new Color(0.31f, 0.49f, 0.67f, 1f));
            RenderingComposition.ConfigureEnvironment(
                new Color(0.80f, 0.83f, 0.86f, 1f),
                new Vector3(-0.48f, 0.80f, -0.35f).normalized,
                new Color(1.0f, 0.91f, 0.74f, 1f),
                new Color(0.48f, 0.55f, 0.62f, 1f));

            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.60f, 0.69f, 0.74f, 1f);
            _camera.fieldOfView = 34f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 180f;
            _camera.allowHDR = false;
        }

        private void ApplyCamera()
        {
            if (_camera == null) return;
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.position = _focus + orbit * (Vector3.back * _distance);
            _camera.transform.LookAt(_focus);
        }

        private void Shutdown()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _built = false;
        }

        private void OnGUI()
        {
            if (!_built || _camera == null) return;
            GUIStyle heading = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
            GUIStyle caption = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
            };
            caption.normal.textColor = Color.white;

            DrawWorldLabel(_originalCentre, "ORIGINAL", heading);
            DrawWorldLabel(_modifiedCentre, "MODIFIED", heading);
            GUI.Box(new Rect(Screen.width * 0.5f - 220f, 16f, 440f, 70f), GUIContent.none);
            GUI.Label(new Rect(Screen.width * 0.5f - 210f, 22f, 420f, 28f),
                $"{m_RoleId + 1:00}/{RoleCount:00}  {SelectedRoleName}", heading);
            GUI.Label(new Rect(Screen.width * 0.5f - 210f, 52f, 420f, 24f),
                $"←/→ or A/D change structure  •  right-drag orbit  •  wheel zoom  •  R rebuild  •  {_status}",
                caption);

            if (GUI.Button(new Rect(18f, Screen.height - 58f, 110f, 38f), "← Previous"))
                RebuildComparison((m_RoleId + RoleCount - 1) % RoleCount);
            if (GUI.Button(new Rect(Screen.width - 128f, Screen.height - 58f, 110f, 38f), "Next →"))
                RebuildComparison((m_RoleId + 1) % RoleCount);
        }

        private void DrawWorldLabel(Vector3 world, string text, GUIStyle style)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            GUI.Box(new Rect(screen.x - LabelWidth * 0.5f,
                Screen.height - screen.y - 42f, LabelWidth, 36f), text, style);
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: GameMaterialIds.MasonryLarge,
                masonry: GameMaterialIds.MasonrySmall,
                darkMasonry: GameMaterialIds.DarkStone,
                timber: GameMaterialIds.Wood,
                glass: GameMaterialIds.Glass,
                warmWindow: GameMaterialIds.LitWindow,
                roofTile: GameMaterialIds.Tile,
                slate: GameMaterialIds.Slate,
                cloth: GameMaterialIds.Cloth,
                moss: GameMaterialIds.Moss,
                water: GameMaterialIds.Water,
                roadSurface: GameMaterialIds.Dirt);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
