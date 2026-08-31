using System.Collections;
using Game.Composition.Materials;
using Game.Materials.Api;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Deterministic built-player review tableau for production Water presentation.
    /// Scene-specific content is authored into authoritative storage, then rendered through the
    /// same RenderingComposition/VoxelRenderPass path used by gameplay. Water and Cascade therefore
    /// come from the production CpuWaterSurfaceChunkCache rather than a scene-only proxy or the
    /// distant VoxelFarTerrain clipmap.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Water Validation Tableau")]
    [DisallowMultipleComponent]
    public sealed class WaterStartupFallbackValidationShowcase : MonoBehaviour
    {
        private const uint ShowcaseSeed = 0x5EED1234u;
        private const int RegionX = 1;
        private const int RegionZ = 6;
        private const int BaseY = 180;
        private const int StillPoolCentreX = 170;
        private const int StillPoolCentreZ = 220;
        private const int StillPoolShelfRadiusX = 42;
        private const int StillPoolShelfRadiusZ = 36;
        private const int StillPoolWaterRadiusX = 30;
        private const int StillPoolWaterRadiusZ = 24;
        private const int RiverStartZ = 180;
        private const int RiverEndZ = 330;
        private const int CascadeStartZ = 330;
        private const int CascadeEndZ = 366;
        private const int ReceivingPoolCentreX = 250;
        private const int ReceivingPoolCentreZ = 390;

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private float _reviewStartedAt;
        private int _cameraShot = -1;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _reviewStartedAt = Time.unscaledTime;
            StartCoroutine(BuildReview());
        }

        private void Update()
        {
            if (!Application.isPlaying || _camera == null)
                return;

            float elapsed = Time.unscaledTime - _reviewStartedAt;
            int shot = elapsed < 12f ? 0 : elapsed < 20f ? 1 : 2;
            if (shot == _cameraShot)
                return;

            ApplyCameraShot(_camera, shot);
            _cameraShot = shot;
        }

        private IEnumerator BuildReview()
        {
            _camera = CreateCamera();
            ApplyCameraShot(_camera, 0);
            _cameraShot = 0;

            _storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 2,
                mixedBrickCapacity: 8192,
                changeJournalCapacity: 4096);
            RegisterGameMaterials(_storage);
            GameMaterialComposition.Install();

            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 180000);
            int originX = RegionX * 512;
            int originZ = RegionZ * 512;
            AuthorTableau(authoring, originX, originZ);
            _storage.PublishAllResidentRegions();

            var renderingWorld = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);

            RenderingComposition.ResetSurfacePassDiagnostics("water-validation-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(BaseY);
            RenderingComposition.SetVoxelRingRadiusMetres(80f);
            RenderingComposition.SetBuildBudgets(4.0, 2.0);
            RenderingComposition.ConfigureEnvironment(
                Color.white,
                new Vector3(-0.35f, 0.82f, -0.45f).normalized,
                new Color(0.78f, 0.84f, 0.88f, 1f),
                new Color(0.34f, 0.44f, 0.54f, 1f));
            RenderingComposition.ConfigureWorld(
                in renderingWorld,
                _storage.Changes,
                ShowcaseSeed,
                farFieldEnabled: false);
            RenderingComposition.SetSurfaceBuildEnabled(true);

            const int requiredStableFrames = 20;
            const int maximumWaitFrames = 900;
            int stableFrames = 0;
            int waitedFrames = 0;
            while (waitedFrames++ < maximumWaitFrames && stableFrames < requiredStableFrames)
            {
                RenderingComposition.GetVoxelSurfaceCounts(out int visible, out int missing);
                if (visible > 0
                    && missing == 0
                    && RenderingComposition.HasCompletePublishedNearSurfaceCoverage())
                {
                    stableFrames++;
                }
                else
                {
                    stableFrames = 0;
                }
                yield return null;
            }

            if (stableFrames < requiredStableFrames)
            {
                RenderingComposition.GetVoxelSurfaceCounts(out int visible, out int missing);
                Debug.LogError(
                    $"WATER_VALIDATION production near renderer did not converge: visible={visible}, missing={missing}.");
                yield break;
            }

            Debug.Log(
                "WATER_VALIDATION ready: still pool, shallow shoreline, meandering river, stepped cascade, receiving pool, and terrain contacts use production near-field water presentation.");
            _camera.transform.hasChanged = false;
        }

        private static void RegisterGameMaterials(IVoxelStorageRuntime storage)
        {
            MaterialDefinition[] definitions = GameMaterialComposition.SimulationDefinitions();
            for (int i = 0; i < definitions.Length; i++)
            {
                MaterialDefinition definition = definitions[i];
                storage.RegisterMaterial(
                    definition.MaterialId,
                    definition.Hardness,
                    definition.DestructionClass,
                    definition.DefaultSurfaceStyle,
                    definition.AllowedCoatings);
            }
        }

        private static void AuthorTableau(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            AuthorTerrainApron(authoring, originX, originZ);
            AuthorRiverBanks(authoring, originX, originZ);
            AuthorRiver(authoring, originX, originZ);

            FillOrganicEllipse(authoring, originX, originZ,
                StillPoolCentreX, StillPoolCentreZ,
                StillPoolShelfRadiusX, StillPoolShelfRadiusZ,
                BaseY - 1, GameMaterialIds.Sand, 0.10f);
            FillOrganicEllipse(authoring, originX, originZ,
                StillPoolCentreX, StillPoolCentreZ,
                StillPoolWaterRadiusX, StillPoolWaterRadiusZ,
                BaseY, GameMaterialIds.Water, 0.08f);

            FillOrganicEllipse(authoring, originX, originZ,
                ReceivingPoolCentreX, ReceivingPoolCentreZ,
                36, 28, BaseY - 1, GameMaterialIds.Sand, 0.09f);
            FillOrganicEllipse(authoring, originX, originZ,
                ReceivingPoolCentreX, ReceivingPoolCentreZ,
                26, 19, BaseY, GameMaterialIds.Water, 0.07f);

            AuthorCascade(authoring, originX, originZ);
        }

        private static void AuthorCascade(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            const int cascadeDrop = 3;
            const float cascadeSpan = CascadeEndZ - CascadeStartZ + 1f;

            for (int z = CascadeStartZ; z <= CascadeEndZ; z++)
            {
                float t = Mathf.InverseLerp(CascadeStartZ, CascadeEndZ, z);
                float centre = RiverCentreX(z) + Mathf.Sin(z * 0.31f) * 0.8f;
                float halfWidth = Mathf.Lerp(6.5f, 4.5f, t) + Mathf.Sin(z * 0.43f) * 0.55f;
                for (int x = 195; x <= 295; x++)
                {
                    float dx = Mathf.Abs(x - centre);
                    int stagger = ((x * 11 + z * 7) & 3) - 1;
                    int step = Mathf.Clamp(
                        Mathf.FloorToInt(((z - CascadeStartZ) + stagger) / cascadeSpan * (cascadeDrop + 1)),
                        0,
                        cascadeDrop);
                    int y = BaseY + cascadeDrop - step;

                    if (dx <= halfWidth)
                    {
                        FillColumn(authoring, originX + x, originZ + z, BaseY - 3, y - 1, GameMaterialIds.Stone);
                        authoring.Set(originX + x, y, originZ + z, GameMaterialIds.Cascade);
                    }
                    else if (dx <= halfWidth + 2.5f)
                    {
                        FillColumn(authoring, originX + x, originZ + z, BaseY - 3, y - 1, GameMaterialIds.Sand);
                    }
                    else if (dx <= halfWidth + 10f)
                    {
                        float bankT = Mathf.InverseLerp(halfWidth + 2.5f, halfWidth + 10f, dx);
                        int bankY = Mathf.RoundToInt(Mathf.Lerp(y - 1, BaseY - 2, bankT));
                        FillColumn(authoring, originX + x, originZ + z, BaseY - 3, bankY, GameMaterialIds.Grass);
                    }
                }
            }
        }

        private static void AuthorTerrainApron(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            const float centreX = 220f;
            const float centreZ = 290f;
            const float radiusX = 165f;
            const float radiusZ = 190f;

            for (int z = 100; z <= 480; z++)
            for (int x = 50; x <= 390; x++)
            {
                float nx = (x - centreX) / radiusX;
                float nz = (z - centreZ) / radiusZ;
                float d = nx * nx + nz * nz;
                if (d > 1f)
                    continue;

                float relief = Mathf.Sin(x * 0.041f) * 0.7f
                    + Mathf.Cos(z * 0.035f) * 0.6f
                    + Mathf.Sin((x + z) * 0.024f) * 0.4f;
                int y = BaseY - 2 + Mathf.RoundToInt(relief);
                byte material = d > 0.9f || relief < -1.1f
                    ? GameMaterialIds.Stone
                    : GameMaterialIds.Grass;
                FillColumn(authoring, originX + x, originZ + z, BaseY - 5, y, material);
            }
        }

        private static void AuthorRiverBanks(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            for (int z = RiverStartZ; z <= RiverEndZ; z++)
            {
                float centre = RiverCentreX(z);
                float halfWater = RiverHalfWidth(z);
                float halfBank = halfWater + 16f;
                int waterY = RiverHeight(z);
                for (int x = 195; x <= 295; x++)
                {
                    float dx = Mathf.Abs(x - centre);
                    if (dx <= halfWater || dx > halfBank)
                        continue;
                    if (IsInsideEllipse(
                        x, z,
                        StillPoolCentreX, StillPoolCentreZ,
                        StillPoolShelfRadiusX, StillPoolShelfRadiusZ))
                    {
                        continue;
                    }

                    float bankT = Mathf.InverseLerp(halfWater, halfBank, dx);
                    int bankY = Mathf.RoundToInt(Mathf.Lerp(waterY - 1, BaseY - 2, bankT));
                    byte material = bankT < 0.28f ? GameMaterialIds.Sand : GameMaterialIds.Grass;
                    FillColumn(authoring, originX + x, originZ + z, BaseY - 3, bankY, material);
                }
            }
        }

        private static void AuthorRiver(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            for (int z = RiverStartZ; z <= RiverEndZ; z++)
            {
                float centre = RiverCentreX(z);
                float halfWidth = RiverHalfWidth(z);
                int y = RiverHeight(z);
                for (int x = 195; x <= 295; x++)
                {
                    if (Mathf.Abs(x - centre) > halfWidth)
                        continue;

                    FillColumn(authoring, originX + x, originZ + z, BaseY - 3, y - 1, GameMaterialIds.Sand);
                    authoring.Set(originX + x, y, originZ + z, GameMaterialIds.Water);
                }
            }
        }

        private static float RiverCentreX(int z)
        {
            float t = Mathf.InverseLerp(RiverStartZ, CascadeEndZ, z);
            return 238f
                + Mathf.Sin(t * Mathf.PI * 1.8f) * 19f
                + Mathf.Sin(t * Mathf.PI * 4.2f) * 7f;
        }

        private static float RiverHalfWidth(int z)
        {
            float t = Mathf.InverseLerp(RiverStartZ, CascadeEndZ, z);
            return Mathf.Lerp(6.5f, 4.8f, t)
                + Mathf.Sin(t * Mathf.PI * 2.2f)
                + Mathf.Sin(z * 0.37f) * 0.65f;
        }

        private static int RiverHeight(int z)
        {
            return BaseY + 3;
        }

        private static void FillColumn(
            IStructureAuthoringSession authoring,
            int x,
            int z,
            int minY,
            int maxY,
            byte material)
        {
            if (maxY < minY)
                return;

            if (maxY > minY)
                authoring.FillColumnBulk(x, minY, maxY, z, material);
            authoring.Set(x, maxY, z, material);
        }

        private static void FillOrganicEllipse(
            IStructureAuthoringSession authoring,
            int originX,
            int originZ,
            int centreX,
            int centreZ,
            int radiusX,
            int radiusZ,
            int y,
            byte material,
            float edgeNoise)
        {
            int padding = Mathf.CeilToInt(Mathf.Max(radiusX, radiusZ) * edgeNoise) + 1;
            int minX = Mathf.Max(0, centreX - radiusX - padding);
            int maxX = Mathf.Min(511, centreX + radiusX + padding);
            int minZ = Mathf.Max(0, centreZ - radiusZ - padding);
            int maxZ = Mathf.Min(511, centreZ + radiusZ + padding);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - centreX;
                float dz = z - centreZ;
                float angle = Mathf.Atan2(dz, dx);
                float ripple = Mathf.Sin(angle * 3f + centreX * 0.01f) * 0.55f
                    + Mathf.Sin(angle * 7f + centreZ * 0.013f) * 0.30f
                    + Mathf.Sin((x + z) * 0.19f) * 0.15f;
                float scale = 1f + ripple * edgeNoise;
                float nx = dx / (radiusX * scale);
                float nz = dz / (radiusZ * scale);
                if (nx * nx + nz * nz <= 1f)
                    authoring.Set(originX + x, y, originZ + z, material);
            }
        }

        private static bool IsInsideEllipse(
            int x,
            int z,
            int centreX,
            int centreZ,
            int radiusX,
            int radiusZ)
        {
            float dx = x - centreX;
            float dz = z - centreZ;
            float radiusXSquared = radiusX * radiusX;
            float radiusZSquared = radiusZ * radiusZ;
            return dx * dx / radiusXSquared + dz * dz / radiusZSquared <= 1f;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Water Validation Review Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.78f, 1f);
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            return camera;
        }

        private static void ApplyCameraShot(Camera camera, int shot)
        {
            Vector3 position;
            Vector3 target;
            switch (shot)
            {
                case 0:
                    position = new Vector3(60.5f, 22.5f, 322f);
                    target = new Vector3(68.2f, BaseY * 0.1f + 0.1f, 329.2f);
                    break;
                case 1:
                    position = new Vector3(64f, 22f, 329.5f);
                    target = new Vector3(74.2f, BaseY * 0.1f + 0.2f, 335.5f);
                    break;
                default:
                    position = new Vector3(66f, 22.5f, 337.5f);
                    target = new Vector3(76f, BaseY * 0.1f + 0.1f, 344.5f);
                    break;
            }

            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(target - position, Vector3.up));
        }

        private void OnDestroy()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _camera = null;
        }
    }
}
