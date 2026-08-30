using System.Collections;
using System.Reflection;
using Game.Materials.Api;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Deterministic built-player review tableau for production Water presentation.
    ///
    /// The composition authors representative water/terrain relationships into authoritative
    /// storage, lets the production VoxelFarTerrain render its authored-water fallback, then
    /// freezes copies of those production meshes for stable external screenshots. Scene-specific
    /// layout lives here; the shared validation harness remains unaware of Water policy.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Water Validation Tableau")]
    [DisallowMultipleComponent]
    public sealed class WaterStartupFallbackValidationShowcase : MonoBehaviour
    {
        private const uint ShowcaseSeed = 0x5EED1234u;
        private const int RegionX = 1;
        private const int RegionZ = 6;
        // Far-field authored content is recorded only when it rises materially above analytic
        // terrain. Keep the deterministic review pedestal well clear of terrain variation so every
        // representative relationship is present independent of seed-local ground height.
        private const int BaseY = 180;
        private const float InnerRadiusMetres = 30f;
        private const float OuterRadiusMetres = 180f;
        private const int CoarseColumnStep = 32;
        private const int CoarseColumnCentre = 16;
        private const int StillPoolCentreX = 136;
        private const int StillPoolCentreZ = 160;
        private const int StillPoolShelfRadiusX = 104;
        private const int StillPoolShelfRadiusZ = 112;
        private const int StillPoolWaterRadiusX = 72;
        private const int StillPoolWaterRadiusZ = 80;

        private IVoxelStorageRuntime _storage;
        private Material _frozenMaterial;
        private Transform _frozenRoot;

        private void OnEnable()
        {
            if (Application.isPlaying)
                StartCoroutine(BuildReview());
        }

        private IEnumerator BuildReview()
        {
            Camera camera = CreateCamera();
            CreateSun();

            _storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 2,
                mixedBrickCapacity: 8192,
                changeJournalCapacity: 64);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 4096);
            var farField = new FarFieldStructureStore();

            int originX = RegionX * 512;
            int originZ = RegionZ * 512;
            AuthorTableau(authoring, originX, originZ);

            farField.CaptureRegion(new int3(RegionX, 0, RegionZ), _storage.Reads, ShowcaseSeed);
            if (!RequireProbe(farField, originX, originZ, 112, 112, BaseY, GameMaterialIds.Water, "still pool")
                || !RequireProbe(farField, originX, originZ, 208, 112, BaseY - 1, GameMaterialIds.Sand, "shallow shoreline")
                || !RequireProbe(farField, originX, originZ, 240, 80, BaseY + 5, GameMaterialIds.Water, "upper river")
                || !RequireProbe(farField, originX, originZ, 240, 400, BaseY + 6, GameMaterialIds.Cascade, "cascade")
                || !RequireProbe(farField, originX, originZ, 240, 432, BaseY - 1, GameMaterialIds.Water, "receiving pool")
                || !RequireProbe(farField, originX, originZ, 272, 432, BaseY, GameMaterialIds.Stone, "terrain contact"))
            {
                yield break;
            }

            VoxelFarTerrain far = VoxelFarTerrain.Create(
                parent: transform,
                seed: ShowcaseSeed,
                innerRadiusMetres: InnerRadiusMetres,
                outerRadiusMetres: OuterRadiusMetres);
            far.Structures = farField;

            // The first LateUpdate creates ring zero synchronously and builds the semantic
            // authored-water fallback. Freeze at end-of-frame so every external capture reviews
            // the same deterministic production presentation rather than a moving convergence state.
            yield return new WaitForEndOfFrame();

            if (!FreezeProductionMeshes(far))
            {
                Debug.LogError("WATER_VALIDATION production meshes could not be frozen.");
                yield break;
            }

            far.enabled = false;
            Destroy(far.gameObject);
            Debug.Log(
                "WATER_VALIDATION ready: still pool, shallow shoreline, descending river, cascade, receiving pool, and terrain contacts use production authored-water presentation.");

            camera.transform.hasChanged = false;
        }

        private static bool RequireProbe(
            FarFieldStructureStore farField,
            int originX,
            int originZ,
            int localX,
            int localZ,
            int expectedHeight,
            byte expectedMaterial,
            string relationship)
        {
            int worldX = originX + localX;
            int worldZ = originZ + localZ;
            int actualHeight = farField.AuthoredTerrainHeightAt(worldX, worldZ);
            byte actualMaterial = farField.AuthoredTerrainMaterialAt(worldX, worldZ);
            if (actualHeight == expectedHeight && actualMaterial == expectedMaterial)
                return true;

            Debug.LogError(
                $"WATER_VALIDATION {relationship} probe mismatch at ({localX},{localZ}): " +
                $"expected height/material {expectedHeight}/{expectedMaterial}, got {actualHeight}/{actualMaterial}.");
            return false;
        }

        private static void AuthorTableau(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            // Shape a rolling, irregular review island rather than a rectangular pedestal. The
            // validation still uses the production far-field surface, but the composition now reads
            // as terrain with banks and relief instead of a diagram made from orthogonal plates.
            AuthorTerrainApron(authoring, originX, originZ);

            // Descending river. Its rock banks explicitly yield to the still-pool shelf at the
            // confluence. Structure authoring is additive by voxel height, so semantic ownership of
            // overlapping columns must be expressed by the masks rather than by write order.
            AuthorRiverBanks(authoring, originX, originZ);
            AuthorRiver(authoring, originX, originZ);

            // Still water. The broad organic sand shelf owns its full footprint at the confluence;
            // the calm water then occupies the interior.
            FillEllipse(authoring, originX, originZ,
                StillPoolCentreX, StillPoolCentreZ, StillPoolShelfRadiusX, StillPoolShelfRadiusZ,
                BaseY - 1, GameMaterialIds.Sand);
            FillEllipse(authoring, originX, originZ,
                StillPoolCentreX, StillPoolCentreZ, StillPoolWaterRadiusX, StillPoolWaterRadiusZ,
                BaseY, GameMaterialIds.Water);

            // Waterfall/cascade. The semantic cascade drops between rock shoulders into an organic
            // receiving pool. This remains coarse by design because these are the actual production
            // fallback meshes under validation, not a scene-only proxy surface.
            FillEllipse(authoring, originX, originZ, 252, 440, 84, 52, BaseY - 4, GameMaterialIds.Stone);
            FillEllipse(authoring, originX, originZ, 252, 440, 62, 42, BaseY - 1, GameMaterialIds.Water);
            FillRiverBand(authoring, originX, originZ, 368, 384, BaseY + 7, GameMaterialIds.Water, 42f);
            FillRiverBand(authoring, originX, originZ, 384, 416, BaseY + 6, GameMaterialIds.Cascade, 38f);

            // A dry rock tongue intrudes into the receiving pool to expose water/solid contact.
            // The representative probe at (272,432) deliberately sits on the tongue.
            for (int z = CoarseColumnCentre; z < 512; z += CoarseColumnStep)
            for (int x = CoarseColumnCentre; x < 512; x += CoarseColumnStep)
            {
                if (x < 272 || x > 320 || z < 432 || z > 480)
                    continue;
                int taper = (z - 432) / CoarseColumnStep;
                if (x > 304 - taper * 8)
                    continue;
                authoring.Set(originX + x, BaseY + (x == 272 ? 0 : 1), originZ + z, GameMaterialIds.Stone);
            }
        }

        private static void AuthorTerrainApron(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            const float centreX = 222f;
            const float centreZ = 258f;
            const float radiusX = 218f;
            const float radiusZ = 238f;

            for (int z = CoarseColumnCentre; z < 512; z += CoarseColumnStep)
            for (int x = CoarseColumnCentre; x < 512; x += CoarseColumnStep)
            {
                float nx = (x - centreX) / radiusX;
                float nz = (z - centreZ) / radiusZ;
                float d = nx * nx + nz * nz;
                if (d > 1f)
                    continue;

                float relief = Mathf.Sin(x * 0.031f) * 2.2f
                    + Mathf.Cos(z * 0.027f) * 1.7f
                    + Mathf.Sin((x + z) * 0.019f) * 1.2f;
                int y = BaseY - 10 + Mathf.RoundToInt(relief);
                byte material = d > 0.78f || relief < -2.2f
                    ? GameMaterialIds.Stone
                    : GameMaterialIds.Grass;
                authoring.Set(originX + x, y, originZ + z, material);
            }
        }

        private static void AuthorRiverBanks(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            for (int z = 48; z <= 416; z += CoarseColumnStep)
            {
                float centre = RiverCentreX(z);
                float halfWater = RiverHalfWidth(z);
                float halfBank = halfWater + 34f;
                int waterY = RiverHeight(z);
                for (int x = CoarseColumnCentre; x < 512; x += CoarseColumnStep)
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
                    authoring.Set(originX + x, waterY - 3, originZ + z, GameMaterialIds.Stone);
                }
            }
        }

        private static void AuthorRiver(IStructureAuthoringSession authoring, int originX, int originZ)
        {
            for (int z = 48; z <= 368; z += CoarseColumnStep)
            {
                float centre = RiverCentreX(z);
                float halfWidth = RiverHalfWidth(z);
                int y = RiverHeight(z);
                for (int x = CoarseColumnCentre; x < 512; x += CoarseColumnStep)
                {
                    if (Mathf.Abs(x - centre) <= halfWidth)
                        authoring.Set(originX + x, y, originZ + z, GameMaterialIds.Water);
                }
            }
        }

        private static void FillRiverBand(
            IStructureAuthoringSession authoring,
            int originX,
            int originZ,
            int minZ,
            int maxZ,
            int y,
            byte material,
            float halfWidth)
        {
            for (int z = FirstCoarseCentreAtOrAfter(minZ); z <= maxZ; z += CoarseColumnStep)
            {
                float centre = RiverCentreX(z);
                for (int x = CoarseColumnCentre; x < 512; x += CoarseColumnStep)
                {
                    if (Mathf.Abs(x - centre) <= halfWidth)
                        authoring.Set(originX + x, y, originZ + z, material);
                }
            }
        }

        private static float RiverCentreX(int z)
        {
            float t = Mathf.InverseLerp(48f, 416f, z);
            return 250f + Mathf.Sin(t * Mathf.PI * 1.65f) * 24f + Mathf.Sin(t * Mathf.PI * 3.1f) * 8f;
        }

        private static float RiverHalfWidth(int z)
        {
            float t = Mathf.InverseLerp(48f, 416f, z);
            return Mathf.Lerp(50f, 34f, t) + Mathf.Sin(t * Mathf.PI * 2.2f) * 5f;
        }

        private static int RiverHeight(int z)
        {
            if (z <= 176)
                return BaseY + 5;
            if (z <= 272)
                return BaseY + 3;
            if (z <= 368)
                return BaseY + 1;
            return BaseY + 7;
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
            for (int z = CoarseColumnCentre; z < 512; z += CoarseColumnStep)
            for (int x = CoarseColumnCentre; x < 512; x += CoarseColumnStep)
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

        private static int FirstCoarseCentreAtOrAfter(int minimum)
        {
            if (minimum <= CoarseColumnCentre)
                return CoarseColumnCentre;
            int delta = minimum - CoarseColumnCentre;
            return CoarseColumnCentre + ((delta + CoarseColumnStep - 1) / CoarseColumnStep) * CoarseColumnStep;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Water Validation Review Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.29f, 0.43f, 0.61f, 1f);
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;

            // Use a lower three-quarter presentation so bank relief, river descent, and the cascade
            // read spatially. This is scene policy and intentionally remains outside the harness.
            Vector3 position = new Vector3(27f, 55f, 264f);
            Vector3 target = new Vector3(76f, BaseY * 0.1f + 1f, 340f);
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(target - position, Vector3.up));
            return camera;
        }

        private static void CreateSun()
        {
            GameObject lightObject = new GameObject("Water Validation Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.94f, 0.84f, 1f);
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
        }

        private bool FreezeProductionMeshes(VoxelFarTerrain far)
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo meshesField = typeof(VoxelFarTerrain).GetField("_ringMeshes", privateInstance);
            FieldInfo materialField = typeof(VoxelFarTerrain).GetField("m_Material", privateInstance);
            var meshes = meshesField?.GetValue(far) as IList;
            var productionMaterial = materialField?.GetValue(far) as Material;
            if (meshes == null || productionMaterial == null)
                return false;

            _frozenMaterial = new Material(productionMaterial)
            {
                name = "Water Validation Production Material"
            };
            GameObject rootObject = new GameObject("Frozen Production Water Tableau");
            rootObject.transform.SetParent(transform, false);
            _frozenRoot = rootObject.transform;

            int copied = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                if (!(meshes[i] is Mesh source) || source.vertexCount == 0 || source.triangles.Length == 0)
                    continue;

                Mesh frozen = Instantiate(source);
                frozen.name = $"Frozen Validation Ring {i}";
                GameObject ringObject = new GameObject(frozen.name);
                ringObject.transform.SetParent(_frozenRoot, false);
                MeshFilter filter = ringObject.AddComponent<MeshFilter>();
                filter.sharedMesh = frozen;
                MeshRenderer renderer = ringObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _frozenMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                copied++;
            }

            return copied >= 2;
        }

        private void OnDestroy()
        {
            if (_frozenRoot != null)
            {
                MeshFilter[] filters = _frozenRoot.GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter filter in filters)
                {
                    if (filter.sharedMesh != null)
                        Destroy(filter.sharedMesh);
                }
            }

            if (_frozenMaterial != null)
                Destroy(_frozenMaterial);

            _storage?.Dispose();
            _storage = null;
        }
    }
}
