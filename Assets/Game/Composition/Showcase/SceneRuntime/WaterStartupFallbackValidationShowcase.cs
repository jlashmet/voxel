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
    /// Small built-player review scene for the startup far-terrain water regression.
    /// It seeds the same authored-water coarse region as the behavioral test, lets the
    /// production VoxelFarTerrain build its first-frame synchronous/fallback meshes, then
    /// freezes copies of those exact meshes so CI's ten-second screenshots can inspect the
    /// transient startup presentation without booting the full VoxelShowcase world.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Water Startup Fallback Validation")]
    [DisallowMultipleComponent]
    public sealed class WaterStartupFallbackValidationShowcase : MonoBehaviour
    {
        private const uint ShowcaseSeed = 0x5EED1234u;
        private const int RegionX = 1;
        private const int RegionZ = 6;
        private const int WaterY = 100;
        private const float InnerRadiusMetres = 30f;
        private const float OuterRadiusMetres = 180f;

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
                mixedBrickCapacity: 4096,
                changeJournalCapacity: 32);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 512);
            var farField = new FarFieldStructureStore();

            int originX = RegionX * 512;
            int originZ = RegionZ * 512;
            for (int z = 16; z < 512; z += 32)
            for (int x = 16; x < 512; x += 32)
                authoring.Set(originX + x, WaterY, originZ + z, GameMaterialIds.Water);

            farField.CaptureRegion(new int3(RegionX, 0, RegionZ), _storage.Reads, ShowcaseSeed);
            int probeX = originX + 256;
            int probeZ = originZ + 256;
            if (farField.AuthoredTerrainHeightAt(probeX, probeZ) != WaterY
                || farField.AuthoredTerrainMaterialAt(probeX, probeZ) != GameMaterialIds.Water)
            {
                Debug.LogError("WATER_FALLBACK_VALIDATION authored water probe was not captured.");
                yield break;
            }

            VoxelFarTerrain far = VoxelFarTerrain.Create(
                parent: transform,
                seed: ShowcaseSeed,
                innerRadiusMetres: InnerRadiusMetres,
                outerRadiusMetres: OuterRadiusMetres);
            far.Structures = farField;

            // The first LateUpdate creates ring zero synchronously, builds the semantic startup
            // fallback, and only then schedules the first authoritative outer-ring job. Freeze at
            // end-of-frame so the review scene preserves exactly that production startup state.
            yield return new WaitForEndOfFrame();

            if (!FreezeProductionMeshes(far))
            {
                Debug.LogError("WATER_FALLBACK_VALIDATION production startup meshes could not be frozen.");
                yield break;
            }

            far.enabled = false;
            Destroy(far.gameObject);
            Debug.Log(
                "WATER_FALLBACK_VALIDATION ready: production startup meshes frozen with authored water semantics.");

            // Keep the deterministic camera alive and stationary for the external screenshot harness.
            camera.transform.hasChanged = false;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Water Fallback Review Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.51f, 0.66f, 1f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;

            Vector3 position = new Vector3(76.8f, 65f, 260f);
            Vector3 target = new Vector3(76.8f, WaterY * 0.1f, 332.8f);
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(target - position, Vector3.up));
            return camera;
        }

        private static void CreateSun()
        {
            GameObject lightObject = new GameObject("Water Fallback Review Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
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
                name = "Water Startup Fallback Validation Material"
            };
            GameObject rootObject = new GameObject("Frozen Production Far Terrain");
            rootObject.transform.SetParent(transform, false);
            _frozenRoot = rootObject.transform;

            int copied = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                if (!(meshes[i] is Mesh source) || source.vertexCount == 0 || source.triangles.Length == 0)
                    continue;

                Mesh frozen = Instantiate(source);
                frozen.name = $"Frozen Startup Ring {i}";
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
