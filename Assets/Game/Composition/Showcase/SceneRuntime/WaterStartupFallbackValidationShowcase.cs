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
            // A neutral terrain apron makes water/terrain contact legible from the overview camera.
            FillRect(authoring, originX, originZ, 32, 480, 32, 480, BaseY - 8, GameMaterialIds.Grass);

            // Still pool. A broad calm plate is surrounded by a shallow shelf so the shoreline
            // reads as a deliberate transition instead of a water rectangle on open ground.
            FillRect(authoring, originX, originZ, 64, 208, 96, 240, BaseY - 1, GameMaterialIds.Sand);
            FillRect(authoring, originX, originZ, 80, 192, 112, 224, BaseY, GameMaterialIds.Water);
            FillRect(authoring, originX, originZ, 96, 176, 128, 208, BaseY + 1, GameMaterialIds.Water);

            // River. The channel narrows and descends in three deterministic reaches, making its
            // flow direction readable even in a still screenshot without inventing harness motion.
            FillRect(authoring, originX, originZ, 224, 304, 64, 192, BaseY + 5, GameMaterialIds.Water);
            FillRect(authoring, originX, originZ, 232, 296, 192, 288, BaseY + 3, GameMaterialIds.Water);
            FillRect(authoring, originX, originZ, 240, 288, 288, 352, BaseY + 1, GameMaterialIds.Water);
            FillRect(authoring, originX, originZ, 208, 320, 48, 352, BaseY - 2, GameMaterialIds.Stone,
                skipExistingWater: true);

            // Waterfall/cascade. A raised lip feeds a semantic Cascade band into a lower receiving
            // pool. The lower stone shelf exposes contact on both sides of the drop.
            FillRect(authoring, originX, originZ, 232, 296, 352, 384, BaseY + 7, GameMaterialIds.Water);
            FillRect(authoring, originX, originZ, 232, 296, 384, 400, BaseY + 6, GameMaterialIds.Cascade);
            FillRect(authoring, originX, originZ, 232, 296, 400, 416, BaseY + 3, GameMaterialIds.Cascade);
            FillRect(authoring, originX, originZ, 216, 312, 416, 464, BaseY - 1, GameMaterialIds.Water);
            FillRect(authoring, originX, originZ, 200, 328, 368, 464, BaseY - 4, GameMaterialIds.Stone,
                skipExistingWater: true);

            // A dry rock tongue intrudes into the receiving pool to make water/solid intersection
            // and terrain contact visible rather than relying only on an outer shoreline.
            FillRect(authoring, originX, originZ, 264, 312, 432, 464, BaseY, GameMaterialIds.Stone);
        }

        private static void FillRect(
            IStructureAuthoringSession authoring,
            int originX,
            int originZ,
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            int y,
            byte material,
            bool skipExistingWater = false)
        {
            // FarFieldStructureStore samples each 32-voxel coarse column at local 16 + 32n.
            // Author exactly that lattice so semantic validation content cannot disappear merely
            // because a rectangle happened to begin on a different modulo-32 offset.
            int startX = FirstCoarseCentreAtOrAfter(minX);
            int startZ = FirstCoarseCentreAtOrAfter(minZ);
            for (int z = startZ; z <= maxZ; z += CoarseColumnStep)
            for (int x = startX; x <= maxX; x += CoarseColumnStep)
            {
                if (skipExistingWater && IsWaterZone(x, z))
                    continue;
                authoring.Set(originX + x, y, originZ + z, material);
            }
        }

        private static int FirstCoarseCentreAtOrAfter(int minimum)
        {
            if (minimum <= CoarseColumnCentre)
                return CoarseColumnCentre;
            int delta = minimum - CoarseColumnCentre;
            return CoarseColumnCentre + ((delta + CoarseColumnStep - 1) / CoarseColumnStep) * CoarseColumnStep;
        }

        private static bool IsWaterZone(int x, int z)
        {
            bool stillPool = x >= 80 && x <= 192 && z >= 112 && z <= 224;
            bool river = x >= 224 && x <= 304 && z >= 64 && z <= 416;
            bool receivingPool = x >= 216 && x <= 312 && z >= 416 && z <= 464;
            return stillPool || river || receivingPool;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Water Validation Review Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.34f, 0.50f, 0.69f, 1f);
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;

            // Frame the full authored region diagonally: still pool left, river centre, cascade and
            // receiving pool rear. This is scene policy, intentionally outside shared harness code.
            Vector3 position = new Vector3(55f, 86f, 300f);
            Vector3 target = new Vector3(78f, BaseY * 0.1f, 341f);
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
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.95f, 0.86f, 1f);
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
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
