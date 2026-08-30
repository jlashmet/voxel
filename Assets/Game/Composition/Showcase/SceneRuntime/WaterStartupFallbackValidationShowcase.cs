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
        private const int StillPoolCentreX = 136;
        private const int StillPoolCentreZ = 160;
        private const int StillPoolShelfRadiusX = 74;
        private const int StillPoolShelfRadiusZ = 82;
        private const int StillPoolWaterRadiusX = 52;
        private const int StillPoolWaterRadiusZ = 58;

        private IVoxelStorageRuntime _storage;

        private void OnEnable()
        {
            if (Application.isPlaying)
                StartCoroutine(BuildReview());
        }

        private IEnumerator BuildReview()
        {
            Camera camera = CreateCamera();
            _storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 2,
                mixedBrickCapacity: 8192,
                changeJournalCapacity: 4096);
            RegisterGameMaterials(_storage);
            GameMaterialComposition.Install();

            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 350000);
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
            RenderingComposition.SetVoxelRingRadiusMetres(120f);
            RenderingComposition.SetBuildBudgets(4.0, 2.0);
            RenderingComposition.ConfigureEnvironment(
                Color.white,
                new Vector3(-0.45f, -0.82f, -0.35f).normalized,
                new Color(0.52f, 0.70f, 0.84f, 1f),
                new Color(0.12f, 0.28f, 0.48f, 1f));
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
                "WATER_VALIDATION ready: still pool, shallow shoreline, descending river, cascade, receiving pool, and terrain contacts use production near-field water presentation.");
            camera.transform.hasChanged = false;
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

            FillEllipse(authoring, originX, originZ,
                StillPoolCentreX, StillPoolCentreZ,
                StillPoolShelfRadiusX, StillPoolShelfRadiusZ,
                BaseY - 1, GameMaterialIds.Sand);
            FillEllipse(authoring, originX, originZ,
                StillPoolCentreX, StillPoolCentreZ,
                StillPoolWaterRadiusX, StillPoolWaterRadiusZ,
                BaseY, GameMaterialIds.Water);

            FillEllipse(authoring, originX, originZ,
                252, 440, 66, 44, BaseY - 2, GameMaterialIds.Stone);
            FillEllipse(authoring, originX, originZ,
                252, 440, 46, 32, BaseY, GameMaterialIds.Water);

            for (int z = 368; z <= 416; z++)
            {
                float t = Mathf.InverseLerp(368f, 416f, z);
                int y = Mathf.RoundToInt(Mathf.Lerp(BaseY + 5, BaseY, t));
                float centre = RiverCentreX(z);
                float halfWidth = Mathf.Lerp(40f, 34f, t);
                for (int x = 180; x <= 330; x++)
                {
                    if (Mathf.Abs(x - centre) <= halfWidth)
                        authoring.Set(originX + x, y, originZ + z, GameMaterialIds.Cascade);
                }
            }

            for (int z = 432; z <= 472; z++)
            for (int x = 272; x <= 314; x++)
            {
                int taper = (z - 432) / 4;
                if (x <= 310 - taper)
                    authoring.Set(originX + x, BaseY + 1, originZ + z, GameMaterialIds.Stone);
            }
        }

        private static void AuthorTerrainApron(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            const float centreX = 222f;
            const float centreZ = 258f;
            const float radiusX = 218f;
            const float radiusZ = 238f;

            for (int z = 18; z <= 498; z++)
            for (int x = 8; x <= 442; x++)
            {
                float nx = (x - centreX) / radiusX;
                float nz = (z - centreZ) / radiusZ;
                float d = nx * nx + nz * nz;
                if (d > 1f)
                    continue;

                float relief = Mathf.Sin(x * 0.031f) * 1.6f
                    + Mathf.Cos(z * 0.027f) * 1.2f
                    + Mathf.Sin((x + z) * 0.019f) * 0.8f;
                int y = BaseY - 5 + Mathf.RoundToInt(relief);
                byte material = d > 0.82f || relief < -1.8f
                    ? GameMaterialIds.Stone
                    : GameMaterialIds.Grass;
                authoring.Set(originX + x, y, originZ + z, material);
            }
        }

        private static void AuthorRiverBanks(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            for (int z = 48; z <= 368; z++)
            {
                float centre = RiverCentreX(z);
                float halfWater = RiverHalfWidth(z);
                float halfBank = halfWater + 18f;
                int waterY = RiverHeight(z);
                for (int x = 170; x <= 335; x++)
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
                    authoring.Set(originX + x, waterY - 2, originZ + z, GameMaterialIds.Stone);
                }
            }
        }

        private static void AuthorRiver(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            for (int z = 48; z <= 368; z++)
            {
                float centre = RiverCentreX(z);
                float halfWidth = RiverHalfWidth(z);
                int y = RiverHeight(z);
                for (int x = 170; x <= 335; x++)
                {
                    if (Mathf.Abs(x - centre) <= halfWidth)
                        authoring.Set(originX + x, y, originZ + z, GameMaterialIds.Water);
                }
            }
        }

        private static float RiverCentreX(int z)
        {
            float t = Mathf.InverseLerp(48f, 416f, z);
            return 250f
                + Mathf.Sin(t * Mathf.PI * 1.65f) * 24f
                + Mathf.Sin(t * Mathf.PI * 3.1f) * 8f;
        }

        private static float RiverHalfWidth(int z)
        {
            float t = Mathf.InverseLerp(48f, 416f, z);
            return Mathf.Lerp(38f, 30f, t) + Mathf.Sin(t * Mathf.PI * 2.2f) * 4f;
        }

        private static int RiverHeight(int z)
        {
            if (z <= 176)
                return BaseY + 7;
            if (z <= 272)
                return BaseY + 6;
            return BaseY + 5;
        }

        private static void FillEllipse(
            IStructureAuthoringSession authoring,
            int originX,
            int originZ,
            int centreX,
            int centreZ,
            int radiusX,
            int radiusZ,
            int y,
            byte material)
        {
            int minX = Mathf.Max(0, centreX - radiusX);
            int maxX = Mathf.Min(511, centreX + radiusX);
            int minZ = Mathf.Max(0, centreZ - radiusZ);
            int maxZ = Mathf.Min(511, centreZ + radiusZ);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (IsInsideEllipse(x, z, centreX, centreZ, radiusX, radiusZ))
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
            camera.backgroundColor = new Color(0.29f, 0.43f, 0.61f, 1f);
            camera.fieldOfView = 36f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 240f;

            Vector3 position = new Vector3(58f, 36f, 292f);
            Vector3 target = new Vector3(84f, BaseY * 0.1f + 0.5f, 340f);
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(target - position, Vector3.up));
            return camera;
        }

        private void OnDestroy()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
        }
    }
}
