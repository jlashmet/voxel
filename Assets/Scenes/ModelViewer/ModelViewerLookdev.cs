using System;
using System.Collections.Generic;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.ModelViewer
{
    [RequireComponent(typeof(Camera))]
    public sealed class ModelViewerLookdev : MonoBehaviour
    {
        private const uint ViewerSeed = 0xD12A60u;
        private const float VoxelSize = 0.1f;
        private const float PanelWidth = 340f;
        private const double BuildBudgetMs = 12.0;

        private IVoxelStorageRuntime _storage;
        private IProfileBlockReadSource _profileBlocks;
        private FeatureCatalogue _showcaseCatalogue;
        private readonly List<int> _featureDefinitionIds = new List<int>();
        private Camera _camera;
        private Vector3 _cameraFocus;
        private float _cameraYaw = -42f;
        private float _cameraPitch = 5f;
        private float _cameraDistance = 24f;
        private float _cameraFov = 30f;
        private bool _panelVisible = true;
        private string _status = "BUILDING";
        private int _selectedModel;
        private int3 _activeLocalMin;
        private int3 _activeLocalSize;

        public int SelectedModel => _selectedModel;

        private readonly struct ModelEntry
        {
            public readonly string Name;
            public readonly int3 LocalMin;
            public readonly int3 LocalSize;
            public readonly FeatureKind? Kind;

            public ModelEntry(string name, int3 localMin, int3 localSize, FeatureKind? kind = null)
            {
                Name = name;
                LocalMin = localMin;
                LocalSize = localSize;
                Kind = kind;
            }
        }

        private int ModelCount => 3 + _featureDefinitionIds.Count;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _camera = GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.965f, 0.965f, 0.95f, 1f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 180f;
            _camera.allowHDR = false;

            RenderingComposition.SetSky(
                new Color(0.96f, 0.96f, 0.95f, 1f),
                new Color(0.995f, 0.995f, 0.99f, 1f));
            RenderingComposition.SetSunDirection(new Vector3(-0.55f, 0.72f, -0.42f).normalized);
            RenderingComposition.SetBuildBudgets(BuildBudgetMs, 0);

#pragma warning disable 0618
            _showcaseCatalogue = ShowcaseCatalogue.Build(ViewerSeed, Allocator.Persistent);
#pragma warning restore 0618
            BuildFeatureDefinitionIndex();
            BuildSelectedModel();
        }

        private void OnDisable()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _profileBlocks = null;
            if (_showcaseCatalogue.IsCreated) _showcaseCatalogue.Dispose();
            _featureDefinitionIds.Clear();
        }

        private void BuildFeatureDefinitionIndex()
        {
            _featureDefinitionIds.Clear();
            for (int definitionId = 0; definitionId < _showcaseCatalogue.DefinitionCount; definitionId++)
            {
                FeatureDefinition definition = _showcaseCatalogue.Definitions[definitionId];
                if (definition.Kind == FeatureKind.Structure || definition.Kind == FeatureKind.Infrastructure)
                    _featureDefinitionIds.Add(definitionId);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) _panelVisible = !_panelVisible;
            if (Input.GetKeyDown(KeyCode.R)) BuildSelectedModel();
            if (Input.GetKeyDown(KeyCode.F)) FrameSelectedModel();
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.LeftBracket)) SelectRelative(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.RightBracket)) SelectRelative(1);
            HandleInspectionCamera();

            if (RenderingComposition.TryGetSurfaceBuildStatus(
                    out int knownChunks, out int dirtyChunks, out int residentChunks, out long residentGeometryBytes))
            {
                bool ready = RenderingComposition.HasCompletePublishedNearSurfaceCoverage();
                _status = ready
                    ? $"READY · {residentGeometryBytes / (1024f * 1024f):0.0} MB"
                    : $"MESHING · {residentChunks}/{knownChunks} chunks · {dirtyChunks} dirty";
            }
        }

        public void SelectModelForAutomation(int index)
        {
            if ((uint)index >= (uint)ModelCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Model Viewer has {ModelCount} entries.");
            _selectedModel = index;
            BuildSelectedModel();
        }

        private void SelectRelative(int delta)
        {
            int count = ModelCount;
            if (count <= 0) return;
            _selectedModel = (_selectedModel + delta) % count;
            if (_selectedModel < 0) _selectedModel += count;
            BuildSelectedModel();
        }

        private ModelEntry GetModelEntry(int index)
        {
            if (index == 0)
                return new ModelEntry(
                    "Dragon A · World Builder Production",
                    DragonStatueWorldBuilderObject.LocalMin,
                    DragonStatueWorldBuilderObject.LocalSize,
                    FeatureKind.Structure);
            if (index == 1)
                return new ModelEntry(
                    "Dragon B · Organic Sculpt",
                    DragonStatueAuthoring.LocalMin,
                    DragonStatueAuthoring.LocalSize,
                    FeatureKind.Structure);
            if (index == 2)
                return new ModelEntry(
                    "Hero Arch",
                    new int3(-32, 0, -ModelViewerArchAdapter.Depth / 2),
                    new int3(64, 80, ModelViewerArchAdapter.Depth),
                    FeatureKind.Structure);

            int featureListIndex = index - 3;
            if ((uint)featureListIndex >= (uint)_featureDefinitionIds.Count)
                throw new InvalidOperationException($"Unknown Model Viewer entry {index}");
            FeatureDefinition definition = _showcaseCatalogue.Definitions[_featureDefinitionIds[featureListIndex]];
            return new ModelEntry(definition.Name.ToString(), int3.zero, definition.Footprint, definition.Kind);
        }

        private void BuildSelectedModel()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _profileBlocks = null;

            _storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 64,
                mixedBrickCapacity: 96_000,
                changeJournalCapacity: 32768);
            RegisterViewerMaterials(_storage);

            ModelEntry model = GetModelEntry(_selectedModel);
            long voxelsWritten = -1;

            if (_selectedModel == 0)
            {
                IStructureAuthoringSession authoring = VoxelEngineBootstrap.CreateStructureAuthoring(
                    _storage, writeBudget: 5_000_000);
                AuthorProductionDragon(authoring, -model.LocalMin);
                voxelsWritten = authoring.TotalVoxelsWritten;
                _activeLocalMin = int3.zero;
                _activeLocalSize = model.LocalSize;
            }
            else if (_selectedModel == 1)
            {
                IStructureAuthoringSession authoring = VoxelEngineBootstrap.CreateStructureAuthoring(
                    _storage, writeBudget: 3_000_000);
                AuthorLegacyDragon(authoring, -model.LocalMin);
                voxelsWritten = authoring.TotalVoxelsWritten;
                _activeLocalMin = int3.zero;
                _activeLocalSize = model.LocalSize;
            }
            else if (_selectedModel == 2)
            {
                ArchLookdevBuildResult arch = ModelViewerArchAdapter.Author(_storage);
                _profileBlocks = arch.ProfileBlocks;
                _activeLocalMin = new int3(-arch.Width / 2, 0, -ModelViewerArchAdapter.Depth / 2);
                _activeLocalSize = new int3(arch.Width, arch.Height, ModelViewerArchAdapter.Depth);
            }
            else
            {
                voxelsWritten = AuthorShowcaseFeature(_selectedModel - 3);
                _activeLocalMin = int3.zero;
                _activeLocalSize = model.LocalSize;
            }

            _storage.PublishAllResidentRegions();
            RenderingWorldBinding world = _profileBlocks != null
                ? new RenderingWorldBinding(
                    _storage.Reads, _storage.MaterialPresentation, _storage.SurfacePresentation,
                    _storage.CoatingPresentation, _profileBlocks)
                : new RenderingWorldBinding(
                    _storage.Reads, _storage.MaterialPresentation, _storage.SurfacePresentation,
                    _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world, _storage.Changes, terrainSeed: ViewerSeed,
                solidBuildBudgetMs: BuildBudgetMs, waterBuildBudgetMs: 0, farFieldEnabled: false);

            ApplyMaterialLook();
            FrameSelectedModel();
            _status = voxelsWritten >= 0 ? $"AUTHORED · {voxelsWritten:N0} voxels" : "AUTHORED · retained profiles";
        }

        private int AuthorShowcaseFeature(int featureListIndex)
        {
            int definitionId = _featureDefinitionIds[featureListIndex];
            FeatureDefinition definition = _showcaseCatalogue.Definitions[definitionId];
            var placement = new ExplicitPlacement
            {
                Position = int3.zero,
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
            ParameterSet parameters = FeatureGeneration.ResolveParameters(
                in _showcaseCatalogue, in definition, in placement, definitionId, int3.zero, ViewerSeed);
            using var primitives = new NativeList<Primitive>(math.max(1, definition.MaxPrimitives), Allocator.Temp);
            using var anchors = new NativeList<ResolvedAnchor>(math.max(1, definition.AnchorCount), Allocator.Temp);
            EvaluationResult evaluation = ShapeProgram.Evaluate(
                in _showcaseCatalogue, definitionId, in parameters, int3.zero, 0, ViewerSeed,
                FeatureGeneration.InstanceSeed(ViewerSeed, definitionId, int3.zero), primitives, anchors);
            if (evaluation != EvaluationResult.Ok)
                throw new InvalidOperationException($"Model Viewer could not evaluate '{definition.Name}': {evaluation}.");
            bool hardSurface = definition.Kind == FeatureKind.Structure || definition.Kind == FeatureKind.Infrastructure;
            RasterResult raster = PrimitiveRasteriser.Rasterise(
                primitives.AsArray(), int3.zero, definition.Footprint,
                _storage.Reads, _storage.Mutations, hardSurface);
            if (raster.BudgetExceeded)
                throw new InvalidOperationException($"Model Viewer raster budget exceeded for '{definition.Name}'.");
            return raster.VoxelsWritten;
        }

        private static void AuthorProductionDragon(IStructureAuthoringSession authoring, int3 origin)
        {
            var placement = DragonStatueWorldBuilderObject.CreatePlacement(
                new GeneratedPropId(0xD12A60UL), 1, 1, origin, new int3(0, 0, -1));
            var context = new DecorationContext
            {
                WorldSeed = ViewerSeed,
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
                throw new InvalidOperationException("Dragon Statue World Builder backend refused its placement.");
        }

        private static void AuthorLegacyDragon(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueSculptAuthoring.Author(authoring, origin);
            DragonStatueDetailPass.Apply(authoring, origin);
        }

        private static void RegisterViewerMaterials(IVoxelStorageRuntime storage)
        {
            const ushort smooth = 0;
            for (byte material = 1; material <= 15; material++)
                storage.RegisterMaterial(material, 190, DestructionClass.Crumble, smooth, 0);
        }

        private static void ApplyMaterialLook()
        {
            RenderingComposition.SetMaterialAlbedo(1, new Vector4(0.72f, 0.68f, 0.57f, 1f));
            RenderingComposition.SetMaterialAlbedo(2, new Vector4(0.42f, 0.25f, 0.14f, 1f));
            RenderingComposition.SetMaterialAlbedo(4, new Vector4(0.42f, 0.58f, 0.64f, 1f));
            RenderingComposition.SetMaterialAlbedo(6, new Vector4(0.19f, 0.23f, 0.25f, 1f));
            RenderingComposition.SetMaterialAlbedo(7, new Vector4(0.39f, 0.43f, 0.46f, 1f));
            RenderingComposition.SetMaterialAlbedo(8, new Vector4(0.48f, 0.25f, 0.18f, 1f));
            RenderingComposition.SetMaterialAlbedo(9, new Vector4(0.56f, 0.41f, 0.30f, 1f));
            RenderingComposition.SetMaterialAlbedo(11, new Vector4(0.24f, 0.43f, 0.52f, 1f));
            RenderingComposition.SetMaterialAlbedo(12, new Vector4(0.95f, 0.57f, 0.10f, 1f));
            RenderingComposition.SetMaterialAlbedo(13, new Vector4(0.77f, 0.67f, 0.48f, 1f));
            RenderingComposition.SetMaterialAlbedo(14, new Vector4(0.23f, 0.34f, 0.20f, 1f));
            RenderingComposition.SetMaterialAlbedo(15, new Vector4(0.98f, 0.58f, 0.08f, 1f));
        }

        private void FrameSelectedModel()
        {
            float3 centreVoxels = (float3)_activeLocalMin + (float3)_activeLocalSize * 0.5f;
            _cameraFocus = new Vector3(
                centreVoxels.x * VoxelSize, centreVoxels.y * VoxelSize, centreVoxels.z * VoxelSize);
            if (_selectedModel == 2)
            {
                _cameraYaw = 14.5f;
                _cameraPitch = 3.2f;
                _cameraFov = 34f;
            }
            else if (_selectedModel == 0)
            {
                _cameraYaw = -42f;
                _cameraPitch = 4f;
                _cameraFov = 30f;
                _cameraFocus += new Vector3(0f, -0.25f, -0.25f);
            }
            else
            {
                _cameraYaw = -32f;
                _cameraPitch = 7f;
                _cameraFov = 32f;
            }

            float width = math.max(_activeLocalSize.x, _activeLocalSize.z) * VoxelSize;
            float height = _activeLocalSize.y * VoxelSize;
            float halfVerticalFov = _cameraFov * Mathf.Deg2Rad * 0.5f;
            float verticalDistance = height * 0.5f / Mathf.Tan(halfVerticalFov);
            float aspect = _camera != null && _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;
            float halfHorizontalFov = Mathf.Atan(Mathf.Tan(halfVerticalFov) * aspect);
            float horizontalDistance = width * 0.5f / Mathf.Tan(halfHorizontalFov);
            float margin = _selectedModel == 0 ? 1.26f : 1.16f;
            _cameraDistance = Mathf.Max(8f, Mathf.Max(verticalDistance, horizontalDistance) * margin);
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
                    _cameraDistance * Mathf.Exp(-Input.mouseScrollDelta.y * 0.12f), 1.5f, 120f);
                changed = true;
            }
            if (changed) ApplyCameraTransform();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !_panelVisible) return;
            ModelEntry model = GetModelEntry(_selectedModel);
            GUILayout.BeginArea(new Rect(14, 14, PanelWidth, 245), GUI.skin.box);
            GUILayout.Label("MODEL VIEWER");
            GUILayout.Label("PRODUCTION VOXEL SURFACE");
            GUILayout.Space(7);
            GUILayout.Label($"{_selectedModel + 1} / {ModelCount}  ·  {model.Name}");
            if (model.Kind.HasValue) GUILayout.Label(model.Kind.Value.ToString().ToUpperInvariant());
            GUILayout.Label(_status);
            GUILayout.Space(7);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ PREV")) SelectRelative(-1);
            if (GUILayout.Button("NEXT ▶")) SelectRelative(1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("FRAME  [F]")) FrameSelectedModel();
            if (GUILayout.Button("REBUILD  [R]")) BuildSelectedModel();
            GUILayout.Label("←/→ cycle · RMB orbit · MMB pan · wheel zoom · Tab UI");
            GUILayout.EndArea();
        }
    }
}
