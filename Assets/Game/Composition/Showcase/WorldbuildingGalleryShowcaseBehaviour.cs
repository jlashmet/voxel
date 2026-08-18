using System;
using System.Collections;
using Game.Composition.WorldObjects.Runtime;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Walkable runtime gallery for the reusable worldbuilding stack. Every architectural exhibit is
    /// authored through the real structure authorers into a bounded voxel session, then meshed for
    /// presentation. Castle furniture/interactables use the real world-object runtime composition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldbuildingGalleryShowcaseBehaviour : MonoBehaviour
    {
        private const float VoxelScale = 0.1f;
        private const uint CastleSeed = 0x00C0571Eu;
        private const ulong CaveSeed = 0x000000000C0A7E55ul;

        private ShowcaseVoxelAuthoringSession _castleVolume;
        private Transform _castleMeshRoot;
        private long _castleMeshRevision;
        private float _nextCastleRefresh;

        private IEnumerator Start()
        {
            SetupEnvironment();
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;

            BuildShed(in palette);
            yield return null;
            BuildChurch(in palette);
            yield return null;
            BuildClassicalTemple(in palette);
            yield return null;
            BuildCourtyardTemple(in palette);
            yield return null;
            BuildCathedral(in palette);
            yield return null;
            BuildCastle(in palette);
            yield return null;
            BuildDecorPavilions();
            yield return null;
            BuildCave();
            yield return null;

            CreateOverviewSign();
        }

        private void Update()
        {
            if (_castleVolume == null || _castleMeshRoot == null) return;
            if (_castleVolume.Revision == _castleMeshRevision || Time.unscaledTime < _nextCastleRefresh)
                return;

            _nextCastleRefresh = Time.unscaledTime + 0.25f;
            Transform parent = _castleMeshRoot.parent;
            Vector3 position = _castleMeshRoot.localPosition;
            Quaternion rotation = _castleMeshRoot.localRotation;
            Destroy(_castleMeshRoot.gameObject);
            GameObject mesh = ShowcaseVoxelMeshBuilder.Build(
                _castleVolume, parent, "CastleGeometry", VoxelScale);
            mesh.transform.localPosition = position;
            mesh.transform.localRotation = rotation;
            _castleMeshRoot = mesh.transform;
            _castleMeshRevision = _castleVolume.Revision;
        }

        private static void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.42f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.18f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.66f, 0.72f, 0.78f);
            RenderSettings.fogDensity = 0.0025f;

            var sunObject = new GameObject("Gallery Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Gallery Plaza";
            ground.transform.position = new Vector3(0f, -0.3f, 18f);
            ground.transform.localScale = new Vector3(150f, 0.5f, 150f);
            Renderer renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = SolidMaterial(new Color(0.19f, 0.25f, 0.20f));

            GameObject cameraObject = new GameObject("Gallery Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 65f;
            cameraObject.transform.position = new Vector3(0f, 7f, -26f);
            cameraObject.transform.rotation = Quaternion.Euler(11f, 0f, 0f);
            cameraObject.AddComponent<WorldbuildingGalleryFlyCamera>();
        }

        private void BuildShed(in StructureMaterialPalette palette)
        {
            ShedConfig config = ShedPresets.Storage(in palette);
            BuildSolidExhibit(
                "Storage Shed",
                new Vector3(-4f, 0f, -12f),
                new int3(-48, -12, -48),
                new int3(96, 112, 96),
                session => ShedAuthoring.Author(session, int3.zero, in config),
                "Shared footprint / walls / doors / gable roof");
        }

        private void BuildChurch(in StructureMaterialPalette palette)
        {
            ChurchConfig config = ChurchPresets.ParishChurch(in palette);
            BuildSolidExhibit(
                "Parish Church",
                new Vector3(48f, 0f, 34f),
                new int3(-100, -12, -110),
                new int3(200, 180, 240),
                session => ChurchAuthoring.Author(session, int3.zero, in config),
                "Nave / aisles / sanctuary / apse / bell tower");
        }

        private void BuildCathedral(in StructureMaterialPalette palette)
        {
            CathedralWorldbuildingConfig config = CathedralWorldbuildingPresets.Gothic(in palette);
            BuildSolidExhibit(
                "Gothic Cathedral",
                new Vector3(49f, 0f, -16f),
                new int3(-130, -40, -170),
                new int3(260, 300, 410),
                session => CathedralWorldbuildingAuthoring.Author(session, int3.zero, in config),
                "Transept / chapels / towers / spires / flying buttresses");
        }

        private void BuildClassicalTemple(in StructureMaterialPalette palette)
        {
            TempleConfig config = TemplePresets.ClassicalColumned(in palette);
            BuildSolidExhibit(
                "Classical Temple",
                new Vector3(-45f, 0f, -22f),
                new int3(-100, -20, -100),
                new int3(200, 180, 220),
                session => TempleAuthoring.Author(session, int3.zero, in config),
                "Raised platform / monumental stairs / full colonnade");
        }

        private void BuildCourtyardTemple(in StructureMaterialPalette palette)
        {
            TempleConfig config = TemplePresets.CourtyardTemple(in palette);
            BuildSolidExhibit(
                "Courtyard Temple",
                new Vector3(-45f, 0f, 24f),
                new int3(-110, -20, -120),
                new int3(220, 180, 260),
                session => TempleAuthoring.Author(session, int3.zero, in config),
                "Courtyard / sanctuary / approach axis / perimeter columns");
        }

        private void BuildCastle(in StructureMaterialPalette palette)
        {
            var root = new GameObject("Walled Castle + Interactive Interior");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(0f, 0f, 19f);

            CastlePlan plan = CastlePlanner.Plan(int3.zero, CastleSeed);
            CastlePresetConfig preset = CastlePresets.WalledCastle(in plan, in palette);
            preset.Stages.Site = false;
            preset.Stages.Landscape = false;

            _castleVolume = new ShowcaseVoxelAuthoringSession(
                new int3(-220, -40, -220), new int3(440, 260, 440));
            var build = new CastleAuthoringBuild(_castleVolume, in plan, preset, CastleSeed);
            while (!build.IsComplete) build.Step();

            GameObject mesh = ShowcaseVoxelMeshBuilder.Build(
                _castleVolume, root.transform, "CastleGeometry", VoxelScale);
            _castleMeshRoot = mesh.transform;
            _castleMeshRevision = _castleVolume.Revision;

            var worldObjects = root.AddComponent<WorldObjectRuntimeComposition>();
            worldObjects.LoadCastle(_castleVolume, CastleSeed, 0xCA570001u, in plan);

            CreateLabel(root.transform, "WALLED CASTLE + INTERACTABLE INTERIOR",
                new Vector3(0f, 5.2f, -25f), 0.8f);
            CreateLabel(root.transform,
                "Walk inside: generated furniture, storage, workshop, dining and utility objects are live world objects",
                new Vector3(0f, 4.3f, -25f), 0.34f);
        }

        private void BuildDecorPavilions()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, CastleSeed);

            if (CastleBedroomDecorationAdapter.TryResolve(
                    in plan, out DecorationSpace bedroomSpace, out _, out _,
                    out DecorationPlacement[] bedroomPlacements))
            {
                BuildDecorationPavilion(
                    "Bedroom Decor + Interactables",
                    new Vector3(-12f, 0f, 58f),
                    0xDEC0B001u,
                    in bedroomSpace,
                    bedroomPlacements);
            }

            if (CastleDiningDecorationAdapter.TryResolve(
                    in plan, out DecorationSpace diningSpace, out _, out _,
                    out DecorationPlacement[] diningPlacements))
            {
                BuildDecorationPavilion(
                    "Dining Decor + Interactables",
                    new Vector3(12f, 0f, 58f),
                    0xDEC0D001u,
                    in diningSpace,
                    diningPlacements);
            }
        }

        private void BuildDecorationPavilion(
            string title,
            Vector3 desiredCenter,
            uint parentId,
            in DecorationSpace space,
            DecorationPlacement[] placements)
        {
            var root = new GameObject(title);
            root.transform.SetParent(transform, false);
            int3 min = space.Bounds.Min;
            int3 max = space.Bounds.MaxExclusive;
            Vector3 sourceCenter = new Vector3(
                (min.x + max.x) * 0.5f * VoxelScale,
                min.y * VoxelScale,
                (min.z + max.z) * 0.5f * VoxelScale);
            root.transform.position = desiredCenter - sourceCenter;

            float width = math.max(4f, (max.x - min.x) * VoxelScale + 2f);
            float depth = math.max(4f, (max.z - min.z) * VoxelScale + 2f);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Pavilion Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.position = new Vector3(sourceCenter.x, min.y * VoxelScale - 0.12f, sourceCenter.z);
            floor.transform.localScale = new Vector3(width, 0.2f, depth);
            floor.GetComponent<Renderer>().sharedMaterial = SolidMaterial(new Color(0.32f, 0.28f, 0.22f));

            var runtime = root.AddComponent<WorldObjectRuntimeComposition>();
            runtime.LoadDecorations(parentId, placements);
            CreateLabel(root.transform, title.ToUpperInvariant(),
                new Vector3(sourceCenter.x, min.y * VoxelScale + 3f, sourceCenter.z - depth * 0.5f), 0.5f);
        }

        private void BuildCave()
        {
            var root = new GameObject("Cave Network Cutaway");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(0f, 8f, 86f);

            CaveConfig config = CaveConfig.Default;
            config.TunnelWidth = 7;
            config.TunnelHeight = 8;
            config.SegmentLength = 8;
            config.MainSegmentCount = 9;
            config.TurnChancePercent = 55;
            config.VerticalChancePercent = 40;
            config.MaxVerticalStepPerSegment = 2;
            config.BranchChancePercent = 70;
            config.MaxBranches = 3;
            config.MaxBranchDepth = 2;
            config.BranchSegmentCount = 4;
            config.MinBranchSeparation = 0;
            config.ChamberChancePercent = 60;
            config.MinChamberRadius = 5;
            config.MaxChamberRadius = 8;
            config.MinChamberHeight = 6;
            config.MaxChamberHeight = 10;
            config.ChamberShape = CaveChamberShape.Round;
            config.WallRoughness = 1;
            config.FloorRoughness = 1;
            config.CeilingRoughness = 1;
            config.BoundsHalfExtents = new int3(72, 42, 72);
            config.MinVerticalOffset = -36;
            config.MaxVerticalOffset = 24;

            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                CaveSeed, int3.zero, Facing.East, 7, 8, 3);
            CaveMaterialPalette palette = new CaveMaterialPalette
            {
                Opening = 0,
                Rock = 2,
                Accent = 3,
                Decoration = 4,
                Water = 5,
            };
            var session = new ShowcaseVoxelAuthoringSession(
                new int3(-80, -48, -80), new int3(160, 96, 160), recordCarves: true);
            CaveAuthoring.Author(session, in request, in config, in palette);
            ShowcaseVoxelMeshBuilder.Build(session, root.transform, "CarvedTunnelVolume", VoxelScale, carvedVoid: true);
            CreateLabel(root.transform, "GENERIC CAVE NETWORK — CARVED VOID CUTAWAY",
                new Vector3(0f, 5f, -9f), 0.55f);
        }

        private void BuildSolidExhibit(
            string title,
            Vector3 worldPosition,
            int3 min,
            int3 size,
            Action<ShowcaseVoxelAuthoringSession> author,
            string subtitle)
        {
            var root = new GameObject(title);
            root.transform.SetParent(transform, false);
            root.transform.position = worldPosition;
            var session = new ShowcaseVoxelAuthoringSession(min, size);
            author(session);
            ShowcaseVoxelMeshBuilder.Build(session, root.transform, "Geometry", VoxelScale);

            float front = min.z * VoxelScale - 1.5f;
            float high = math.max(3f, (min.y + size.y) * VoxelScale + 1f);
            CreateLabel(root.transform, title.ToUpperInvariant(), new Vector3(0f, high, front), 0.55f);
            CreateLabel(root.transform, subtitle, new Vector3(0f, high - 0.7f, front), 0.26f);
        }

        private void CreateOverviewSign()
        {
            var root = new GameObject("Gallery Entrance Sign");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(0f, 0f, -20f);
            CreateLabel(root.transform, "WORLDBUILDING GALLERY", new Vector3(0f, 3.4f, 0f), 1f);
            CreateLabel(root.transform,
                "Structures  •  Decorations  •  Interactables  •  Caves     |     WASD + mouse, Shift = fast, E/Q = down/up",
                new Vector3(0f, 2.25f, 0f), 0.34f);
        }

        private static void CreateLabel(Transform parent, string text, Vector3 localPosition, float size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            TextMesh label = go.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = size * 0.12f;
            label.color = new Color(1f, 0.93f, 0.72f);
        }

        private static Material SolidMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            return material;
        }
    }

    /// <summary>Simple editor/player fly camera for the generated gallery.</summary>
    public sealed class WorldbuildingGalleryFlyCamera : MonoBehaviour
    {
        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }
            if (Cursor.lockState != CursorLockMode.Locked) return;

            _yaw += Input.GetAxisRaw("Mouse X") * 2.2f;
            _pitch -= Input.GetAxisRaw("Mouse Y") * 2.2f;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 input = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                Input.GetAxisRaw("Vertical"));
            float speed = Input.GetKey(KeyCode.LeftShift) ? 24f : 8f;
            Vector3 move = transform.right * input.x + Vector3.up * input.y + transform.forward * input.z;
            transform.position += move.normalized * speed * Time.unscaledDeltaTime;
        }
    }
}
