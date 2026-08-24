using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.ModelViewer
{
    /// <summary>
    /// Generic production-path viewer for authored world objects. Models are authored into canonical
    /// voxel storage and rendered through RenderingComposition; this deliberately does not maintain a
    /// parallel preview mesh path. Add new entries to the catalogue as authored objects become useful
    /// for isolated visual inspection.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ModelViewerLookdev : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private const float PanelWidth = 300f;
        private const double BuildBudgetMs = 12.0;

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private Vector3 _cameraFocus;
        private float _cameraYaw = -32f;
        private float _cameraPitch = 7f;
        private float _cameraDistance = 24f;
        private float _cameraFov = 32f;
        private bool _panelVisible = true;
        private string _status = "BUILDING";
        private int _selectedModel;

        private static readonly ModelEntry[] Catalogue =
        {
            new ModelEntry("Dragon Statue", DragonStatueAuthoring.LocalMin, DragonStatueAuthoring.LocalSize),
        };

        private readonly struct ModelEntry
        {
            public readonly string Name;
            public readonly int3 LocalMin;
            public readonly int3 LocalSize;

            public ModelEntry(string name, int3 localMin, int3 localSize)
            {
                Name = name;
                LocalMin = localMin;
                LocalSize = localSize;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            _camera = GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.965f, 0.965f, 0.95f, 1f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.allowHDR = false;

            RenderingComposition.SetSky(
                new Color(0.96f, 0.96f, 0.95f, 1f),
                new Color(0.995f, 0.995f, 0.99f, 1f));
            RenderingComposition.SetSunDirection(new Vector3(-0.55f, 0.72f, -0.42f).normalized);
            RenderingComposition.SetBuildBudgets(BuildBudgetMs, 0);

            BuildSelectedModel();
        }

        private void OnDisable()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) _panelVisible = !_panelVisible;
            if (Input.GetKeyDown(KeyCode.R)) BuildSelectedModel();
            if (Input.GetKeyDown(KeyCode.F)) FrameSelectedModel();

            HandleInspectionCamera();

            if (RenderingComposition.TryGetSurfaceBuildStatus(
                    out int knownChunks,
                    out int dirtyChunks,
                    out int residentChunks,
                    out long residentGeometryBytes))
            {
                bool ready = RenderingComposition.HasCompletePublishedNearSurfaceCoverage();
                _status = ready
                    ? $"READY · {residentGeometryBytes / (1024f * 1024f):0.0} MB"
                    : $"MESHING · {residentChunks}/{knownChunks} chunks · {dirtyChunks} dirty";
            }
        }

        private void BuildSelectedModel()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();

            _storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 24,
                mixedBrickCapacity: 36_000,
                changeJournalCapacity: 8192);
            RegisterViewerMaterials(_storage);

            IStructureAuthoringSession authoring = VoxelEngineBootstrap.CreateStructureAuthoring(
                _storage, writeBudget: 3_000_000);

            ModelEntry model = Catalogue[_selectedModel];
            int3 origin = -model.LocalMin;

            switch (_selectedModel)
            {
                case 0:
                    AuthorDragon(authoring, origin);
                    break;
                default:
                    throw new System.InvalidOperationException($"Unknown Model Viewer entry {_selectedModel}");
            }

            _storage.PublishAllResidentRegions();
            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world,
                _storage.Changes,
                terrainSeed: 0,
                solidBuildBudgetMs: BuildBudgetMs,
                waterBuildBudgetMs: 0,
                farFieldEnabled: false);

            ApplyMaterialLook();
            FrameSelectedModel();
            _status = $"AUTHORED · {authoring.TotalVoxelsWritten:N0} writes";
        }

        private static void AuthorDragon(IStructureAuthoringSession authoring, int3 origin)
        {
            var placement = DragonStatueWorldBuilderObject.CreatePlacement(
                new GeneratedPropId(0xD12A60UL),
                sceneId: 1,
                slotId: 1,
                origin: origin,
                facing: new int3(0, 0, -1));
            var context = new DecorationContext
            {
                WorldSeed = 0xD12A60u,
                StructureId = 1,
                SpaceId = 1,
                StyleId = 1,
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.ExteriorYard,
                Wealth = DecorationWealthTier.Noble,
                Condition = DecorationConditionTier.Worn,
                Environment = DecorationEnvironmentTags.Exterior,
            };

            if (!DecorationVoxelStampBackend.TryAuthor(authoring, in placement, in context))
                throw new System.InvalidOperationException("Dragon Statue World Builder backend refused its placement.");
        }

        private static void RegisterViewerMaterials(IVoxelStorageRuntime storage)
        {
            const ushort smooth = 0;
            storage.RegisterMaterial(GameMaterialIds.Stone, 200, DestructionClass.Crumble, smooth, 0);
            storage.RegisterMaterial(GameMaterialIds.DarkStone, 220, DestructionClass.Crumble, smooth, 0);
            storage.RegisterMaterial(GameMaterialIds.Slate, 210, DestructionClass.Crumble, smooth, 0);
            storage.RegisterMaterial(GameMaterialIds.Gold, 180, DestructionClass.Crumble, smooth, 0);
            storage.RegisterMaterial(GameMaterialIds.Moss, 100, DestructionClass.Crumble, smooth, 0);
        }

        private static void ApplyMaterialLook()
        {
            // Keep the object's material identity intact while presenting it like the project's
            // stylized hero props: cool carved body, warmer horn/plate accents, restrained patina.
            RenderingComposition.SetMaterialAlbedo(GameMaterialIds.Slate,
                new Vector4(0.31f, 0.37f, 0.39f, 1f));
            RenderingComposition.SetMaterialAlbedo(GameMaterialIds.DarkStone,
                new Vector4(0.19f, 0.23f, 0.25f, 1f));
            RenderingComposition.SetMaterialAlbedo(GameMaterialIds.Stone,
                new Vector4(0.58f, 0.56f, 0.49f, 1f));
            RenderingComposition.SetMaterialAlbedo(GameMaterialIds.Moss,
                new Vector4(0.23f, 0.34f, 0.20f, 1f));
            RenderingComposition.SetMaterialAlbedo(GameMaterialIds.Gold,
                new Vector4(0.78f, 0.53f, 0.13f, 1f));
        }

        private void FrameSelectedModel()
        {
            ModelEntry model = Catalogue[_selectedModel];
            float3 centreVoxels = model.LocalMin + model.LocalSize * 0.5f - model.LocalMin;
            _cameraFocus = new Vector3(
                centreVoxels.x * VoxelSize,
                centreVoxels.y * VoxelSize,
                centreVoxels.z * VoxelSize);

            _cameraYaw = -32f;
            _cameraPitch = 7f;
            _cameraFov = 32f;

            float width = model.LocalSize.x * VoxelSize;
            float height = model.LocalSize.y * VoxelSize;
            float halfVerticalFov = _cameraFov * Mathf.Deg2Rad * 0.5f;
            float verticalDistance = height * 0.5f / Mathf.Tan(halfVerticalFov);
            float aspect = _camera != null && _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;
            float halfHorizontalFov = Mathf.Atan(Mathf.Tan(halfVerticalFov) * aspect);
            float horizontalDistance = width * 0.5f / Mathf.Tan(halfHorizontalFov);
            _cameraDistance = Mathf.Max(8f, Mathf.Max(verticalDistance, horizontalDistance) * 1.18f);
            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            Quaternion orbit = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
            _camera.transform.position = _cameraFocus + orbit * (Vector3.back * _cameraDistance);
            _camera.transform.rotation = orbit;
            _camera.fieldOfView = _cameraFov;
        }

        private void HandleInspectionCamera()
        {
            bool pointerOverPanel = _panelVisible && Input.mousePosition.x < PanelWidth + 28f;
            bool changed = false;

            if (!pointerOverPanel && Input.GetMouseButton(1))
            {
                _cameraYaw += Input.GetAxis("Mouse X") * 3.5f;
                _cameraPitch = Mathf.Clamp(_cameraPitch - Input.GetAxis("Mouse Y") * 3.5f, -80f, 80f);
                changed = true;
            }

            if (!pointerOverPanel && Input.GetMouseButton(2))
            {
                float scale = _cameraDistance * 0.0025f;
                _cameraFocus -= _camera.transform.right * Input.GetAxis("Mouse X") * scale;
                _cameraFocus -= _camera.transform.up * Input.GetAxis("Mouse Y") * scale;
                changed = true;
            }

            if (!pointerOverPanel && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                _cameraDistance = Mathf.Clamp(
                    _cameraDistance * Mathf.Exp(-Input.mouseScrollDelta.y * 0.12f), 1.5f, 60f);
                changed = true;
            }

            if (changed) ApplyCameraTransform();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !_panelVisible) return;

            GUILayout.BeginArea(new Rect(14, 14, PanelWidth, 190), GUI.skin.box);
            GUILayout.Label("MODEL VIEWER");
            GUILayout.Label("PRODUCTION VOXEL SURFACE");
            GUILayout.Space(8);
            GUILayout.Label(Catalogue[_selectedModel].Name);
            GUILayout.Label(_status);
            GUILayout.Space(8);
            if (GUILayout.Button("FRAME  [F]")) FrameSelectedModel();
            if (GUILayout.Button("REBUILD  [R]")) BuildSelectedModel();
            GUILayout.Label("RMB orbit · MMB pan · wheel zoom · Tab UI");
            GUILayout.EndArea();
        }
    }
}
