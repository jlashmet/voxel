using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Module-owned built-player tableau for the far-world Rendering module.
    /// It supplies deterministic bounded geometry and render-ready feature instances, then exercises
    /// the production far-terrain shader and production far-feature renderer without depending on
    /// Kentridge generation, voxel residency, or a parallel validation renderer.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Validation/Far World Rendering Tableau")]
    [DisallowMultipleComponent]
    public sealed class FarWorldRenderingValidationShowcase : MonoBehaviour
    {
        private const float StageSeconds = 3.5f;
        private const float NearTerrainEnd = 650f;
        private const float HorizonDistance = 13000f;
        private const float TerrainHalfWidth = 2500f;

        private static readonly ViewStage[] s_Views =
        {
            new("near", new Vector3(0f, 145f, -120f), 350f),
            new("handoff", new Vector3(0f, 115f, 260f), 820f),
            new("1km", new Vector3(0f, 155f, -20f), 1000f),
            new("3km", new Vector3(0f, 180f, -20f), 3000f),
            new("6km", new Vector3(0f, 205f, -20f), 6000f),
            new("8km", new Vector3(0f, 220f, -20f), 8000f),
            new("10km", new Vector3(0f, 235f, -20f), 10000f),
            new("12km", new Vector3(0f, 250f, -20f), 12000f),
        };

        private readonly List<FarFeatureInstance> _instances = new();
        private Camera _camera;
        private ProceduralFarFeatureRenderer _featureRenderer;
        private Material _terrainMaterial;
        private Mesh _nearTerrainMesh;
        private Mesh _farTerrainMesh;
        private int _stageIndex = -1;
        private string _currentView = "initializing";

        private readonly struct ViewStage
        {
            public ViewStage(string name, Vector3 cameraPosition, float targetDistance)
            {
                Name = name;
                CameraPosition = cameraPosition;
                TargetDistance = targetDistance;
            }

            public string Name { get; }
            public Vector3 CameraPosition { get; }
            public float TargetDistance { get; }
        }

        private void Start()
        {
            if (!BuildTableau())
            {
                Debug.LogError("FARWORLD_VALIDATION failure: tableau initialization failed.");
                enabled = false;
                return;
            }

            ApplyView(0);
            Debug.Log(
                $"FARWORLD_VALIDATION ready: features={_featureRenderer.InstanceCount}, " +
                $"nearTerrainVertices={_nearTerrainMesh.vertexCount}, " +
                $"farTerrainVertices={_farTerrainMesh.vertexCount}, views={s_Views.Length}, " +
                "shader=VoxelEngine/FarTerrain.");
        }

        private bool BuildTableau()
        {
            Shader farTerrainShader = Shader.Find("VoxelEngine/FarTerrain");
            if (farTerrainShader == null)
            {
                Debug.LogError("FARWORLD_VALIDATION failure: missing VoxelEngine/FarTerrain shader.");
                return false;
            }

            ConfigurePresentationRows();

            _terrainMaterial = new Material(farTerrainShader)
            {
                name = "FarWorldValidation-Terrain",
                hideFlags = HideFlags.DontSave,
            };
            _terrainMaterial.SetFloat("_AerialDistance", 13000f);
            _terrainMaterial.SetFloat("_VoxelSize", 0.1f);

            _camera = EnsureCamera();
            EnsureLighting();

            _nearTerrainMesh = CreateTerrainMesh(
                "FarWorldValidation-NearTerrain",
                0f,
                NearTerrainEnd,
                xSegments: 48,
                zSegments: 64);
            _farTerrainMesh = CreateTerrainMesh(
                "FarWorldValidation-FarTerrain",
                NearTerrainEnd,
                HorizonDistance,
                xSegments: 48,
                zSegments: 128);

            CreateTerrainObject("Near Terrain - Dense", _nearTerrainMesh);
            CreateTerrainObject("Far Terrain - Coarse", _farTerrainMesh);

            var rendererRoot = new GameObject("Production Far Feature Renderer")
            {
                hideFlags = HideFlags.DontSave,
            };
            rendererRoot.transform.SetParent(transform, false);
            _featureRenderer = rendererRoot.AddComponent<ProceduralFarFeatureRenderer>();

            PopulateRenderReadyFeatures();
            _featureRenderer.SetInstances(_instances);
            return _featureRenderer.InstanceCount == _instances.Count;
        }

        private Camera EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Far World Validation Camera")
                {
                    hideFlags = HideFlags.DontSave,
                };
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 16000f;
            camera.fieldOfView = 46f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.82f, 1f);
            return camera;
        }

        private static void EnsureLighting()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Light>() != null) return;

            var lightObject = new GameObject("Far World Validation Sun")
            {
                hideFlags = HideFlags.DontSave,
            };
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.85f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.64f, 0.78f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.40f, 0.44f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.16f, 0.14f, 1f);
        }

        private static void ConfigurePresentationRows()
        {
            var albedo = new Vector4[32];
            var sampling = new Vector4[32];
            var surface = new Vector4[32];
            var variation = new Vector4[32];

            Vector4 defaultAlbedo = new(0.43f, 0.42f, 0.40f, 1f);
            for (int i = 0; i < 32; i++)
            {
                albedo[i] = defaultAlbedo;
                sampling[i] = new Vector4(0f, 0f, 1f, 0f);
                surface[i] = new Vector4(0.035f, 0.45f, 0.76f, 0f);
                variation[i] = new Vector4(0.45f, 0.28f, 0.16f, 0.30f);
            }

            albedo[0] = new Vector4(0.28f, 0.43f, 0.19f, 1f);
            albedo[1] = new Vector4(0.39f, 0.29f, 0.18f, 1f);
            albedo[2] = new Vector4(0.43f, 0.42f, 0.40f, 1f);
            albedo[3] = new Vector4(0.82f, 0.84f, 0.82f, 1f);
            surface[0] = new Vector4(0.050f, 0.35f, 0.82f, 0f);
            surface[1] = new Vector4(0.045f, 0.30f, 0.74f, 0f);
            surface[2] = new Vector4(0.032f, 0.52f, 0.88f, 0f);
            surface[3] = new Vector4(0.026f, 0.18f, 0.68f, 0f);

            Shader.SetGlobalVectorArray("_MaterialAlbedo", albedo);
            Shader.SetGlobalVectorArray("_MaterialSampling", sampling);
            Shader.SetGlobalVectorArray("_MaterialSurface", surface);
            Shader.SetGlobalVectorArray("_MaterialVariation", variation);
        }

        private Mesh CreateTerrainMesh(
            string meshName,
            float zStart,
            float zEnd,
            int xSegments,
            int zSegments)
        {
            int xCount = xSegments + 1;
            int zCount = zSegments + 1;
            var vertices = new List<Vector3>(xCount * zCount);
            var colours = new List<Color>(xCount * zCount);
            var triangles = new List<int>(xSegments * zSegments * 6);

            for (int zIndex = 0; zIndex < zCount; zIndex++)
            {
                float z = Mathf.Lerp(zStart, zEnd, zIndex / (float)zSegments);
                for (int xIndex = 0; xIndex < xCount; xIndex++)
                {
                    float x = Mathf.Lerp(-TerrainHalfWidth, TerrainHalfWidth, xIndex / (float)xSegments);
                    float y = TerrainHeight(x, z);
                    vertices.Add(new Vector3(x, y, z));
                    colours.Add(MaterialColourAt(x, z, y));
                }
            }

            for (int zIndex = 0; zIndex < zSegments; zIndex++)
            {
                for (int xIndex = 0; xIndex < xSegments; xIndex++)
                {
                    int a = zIndex * xCount + xIndex;
                    int b = a + 1;
                    int c = a + xCount;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.DontSave,
            };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateTerrainObject(string objectName, Mesh mesh)
        {
            var terrainObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave,
            };
            terrainObject.transform.SetParent(transform, false);
            var filter = terrainObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = terrainObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _terrainMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static float TerrainHeight(float x, float z)
        {
            float ridge = 72f * Mathf.Sin(z * 0.00105f);
            float diagonal = 46f * Mathf.Sin((x + z) * 0.0018f);
            float counter = 26f * Mathf.Sin((x - z) * 0.0031f);
            float broad = 34f * Mathf.Sin(x * 0.0041f);
            float mountain = 170f * Mathf.Exp(-Mathf.Pow((z - 7600f) / 2300f, 2f))
                           * Mathf.Exp(-Mathf.Pow(x / 1850f, 2f));
            return ridge + diagonal + counter + broad + mountain;
        }

        private static Color MaterialColourAt(float x, float z, float height)
        {
            float dx = Mathf.Abs(TerrainHeight(x + 8f, z) - TerrainHeight(x - 8f, z));
            float dz = Mathf.Abs(TerrainHeight(x, z + 8f) - TerrainHeight(x, z - 8f));
            float slope = Mathf.Max(dx, dz);
            if (height > 210f) return new Color(0.82f, 0.84f, 0.82f, 1f);
            if (slope > 12f || height > 125f) return new Color(0.43f, 0.42f, 0.40f, 1f);
            if (height < -35f) return new Color(0.39f, 0.29f, 0.18f, 1f);
            return new Color(0.28f, 0.43f, 0.19f, 1f);
        }

        private void PopulateRenderReadyFeatures()
        {
            FarFeatureGeometry house = HouseGeometry();
            FarFeatureGeometry castle = CastleGeometry();
            FarFeatureGeometry tree = TreeGeometry();
            FarFeatureGeometry forest = ForestClusterGeometry();
            FarFeatureGeometry rock = RockGeometry();

            AddFeature(1, new Vector3(-120f, TerrainHeight(-120f, 280f), 280f),
                new Vector3(22f, 18f, 18f), "house", "validation-house",
                FarFeatureTier.Mid, FarFeatureVisualFlags.None, house);
            AddFeature(2, new Vector3(120f, TerrainHeight(120f, 420f), 420f),
                new Vector3(25f, 20f, 20f), "house", "validation-house",
                FarFeatureTier.Mid, FarFeatureVisualFlags.None, house);

            ulong id = 10;
            for (int row = 0; row < 4; row++)
            {
                float z = 180f + row * 145f;
                for (int column = -3; column <= 3; column++)
                {
                    float x = column * 75f + ((row & 1) == 0 ? 25f : -20f);
                    AddFeature(id++, new Vector3(x, TerrainHeight(x, z), z),
                        new Vector3(8f, 26f + row * 2f, 8f), "tree", "validation-tree",
                        FarFeatureTier.Mid, FarFeatureVisualFlags.None, tree);
                }
            }

            for (int row = 0; row < 3; row++)
            {
                float z = 900f + row * 230f;
                for (int column = -4; column <= 4; column++)
                {
                    float x = column * 68f;
                    AddFeature(id++, new Vector3(x, TerrainHeight(x, z), z),
                        new Vector3(20f, 18f, 18f), "house", "validation-house",
                        FarFeatureTier.Far, FarFeatureVisualFlags.None, house);
                }
            }

            AddFeature(200, new Vector3(-340f, TerrainHeight(-340f, 1000f), 1000f),
                new Vector3(230f, 70f, 150f), "forest-cluster", "validation-forest",
                FarFeatureTier.Far, FarFeatureVisualFlags.None, forest);
            AddFeature(201, new Vector3(360f, TerrainHeight(360f, 3000f), 3000f),
                new Vector3(360f, 110f, 220f), "forest-cluster", "validation-forest",
                FarFeatureTier.Far, FarFeatureVisualFlags.None, forest);
            AddFeature(202, new Vector3(-420f, TerrainHeight(-420f, 6000f), 6000f),
                new Vector3(520f, 150f, 320f), "forest-cluster", "validation-forest",
                FarFeatureTier.Horizon, FarFeatureVisualFlags.Landmark, forest);

            AddFeature(300, new Vector3(520f, TerrainHeight(520f, 3250f), 3250f),
                new Vector3(90f, 80f, 75f), "rock", "validation-rock",
                FarFeatureTier.Far, FarFeatureVisualFlags.Landmark, rock);
            AddFeature(301, new Vector3(-520f, TerrainHeight(-520f, 9800f), 9800f),
                new Vector3(230f, 250f, 180f), "rock", "validation-rock",
                FarFeatureTier.Horizon, FarFeatureVisualFlags.HorizonLandmark, rock);

            AddFeature(400, new Vector3(0f, TerrainHeight(0f, 8000f), 8000f),
                new Vector3(230f, 150f, 210f), "castle", "validation-castle",
                FarFeatureTier.Horizon,
                FarFeatureVisualFlags.SettlementAnchor | FarFeatureVisualFlags.Landmark,
                castle);
            AddFeature(401, new Vector3(0f, TerrainHeight(0f, 10000f), 10000f),
                new Vector3(285f, 180f, 245f), "castle", "validation-castle",
                FarFeatureTier.Horizon,
                FarFeatureVisualFlags.Landmark | FarFeatureVisualFlags.HorizonLandmark,
                castle);
            AddFeature(402, new Vector3(0f, TerrainHeight(0f, 12000f), 12000f),
                new Vector3(345f, 215f, 290f), "castle", "validation-castle",
                FarFeatureTier.Horizon,
                FarFeatureVisualFlags.Landmark | FarFeatureVisualFlags.HorizonLandmark,
                castle);

            AddFeature(500, new Vector3(420f, TerrainHeight(420f, 8600f), 8600f),
                new Vector3(40f, 145f, 40f), "tree", "validation-tree-landmark",
                FarFeatureTier.Horizon,
                FarFeatureVisualFlags.HorizonLandmark,
                tree);
        }

        private void AddFeature(
            ulong id,
            Vector3 position,
            Vector3 scale,
            string geometryKey,
            string styleKey,
            FarFeatureTier tier,
            FarFeatureVisualFlags flags,
            FarFeatureGeometry geometry)
        {
            float3 p = new(position.x, position.y, position.z);
            float3 s = new(scale.x, scale.y, scale.z);
            _instances.Add(new FarFeatureInstance(
                id,
                p,
                quaternion.identity,
                s,
                p + new float3(0f, scale.y * 0.5f, 0f),
                new float3(scale.x * 0.5f, scale.y * 0.5f, scale.z * 0.5f),
                geometryKey,
                styleKey,
                tier,
                flags,
                geometry));
        }

        private static FarFeatureGeometry HouseGeometry() => new(new[]
        {
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Box,
                new float3(-0.50f, 0f, -0.42f),
                new float3(0.50f, 0.60f, 0.42f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Prism,
                new float3(-0.56f, 0.56f, -0.48f),
                new float3(0.56f, 0.92f, 0.48f)),
        });

        private static FarFeatureGeometry CastleGeometry() => new(new[]
        {
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Box,
                new float3(-0.50f, 0f, -0.48f),
                new float3(0.50f, 0.24f, 0.48f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Box,
                new float3(-0.22f, 0f, -0.20f),
                new float3(0.22f, 0.70f, 0.20f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Cylinder,
                new float3(-0.54f, 0f, -0.54f),
                new float3(-0.30f, 0.58f, -0.30f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Cylinder,
                new float3(0.30f, 0f, -0.54f),
                new float3(0.54f, 0.58f, -0.30f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Cylinder,
                new float3(-0.54f, 0f, 0.30f),
                new float3(-0.30f, 0.58f, 0.54f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Cylinder,
                new float3(0.30f, 0f, 0.30f),
                new float3(0.54f, 0.58f, 0.54f)),
        });

        private static FarFeatureGeometry TreeGeometry() => new(new[]
        {
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Cylinder,
                new float3(-0.09f, 0f, -0.09f),
                new float3(0.09f, 0.58f, 0.09f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.Cylinder,
                new float3(-0.34f, 0.42f, -0.34f),
                new float3(0.34f, 1.00f, 0.34f)),
        });

        private static FarFeatureGeometry ForestClusterGeometry() => new(new[]
        {
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.RoundedBox,
                new float3(-0.50f, 0.02f, -0.38f),
                new float3(0.50f, 0.72f, 0.38f)),
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.RoundedBox,
                new float3(-0.38f, 0.18f, -0.50f),
                new float3(0.40f, 0.94f, 0.50f)),
        });

        private static FarFeatureGeometry RockGeometry() => new(new[]
        {
            new FarFeatureGeometryPrimitive(
                FarFeatureGeometryShape.RoundedBox,
                new float3(-0.50f, 0f, -0.42f),
                new float3(0.50f, 0.78f, 0.42f)),
        });

        private void Update()
        {
            if (_camera == null) return;

            int nextStage = Mathf.Min(
                s_Views.Length - 1,
                Mathf.FloorToInt(Time.unscaledTime / StageSeconds));
            if (nextStage != _stageIndex) ApplyView(nextStage);
        }

        private void ApplyView(int index)
        {
            _stageIndex = Mathf.Clamp(index, 0, s_Views.Length - 1);
            ViewStage stage = s_Views[_stageIndex];
            _currentView = stage.Name;

            _camera.transform.position = stage.CameraPosition;
            Vector3 target = new(
                0f,
                TerrainHeight(0f, stage.TargetDistance) + TargetHeightOffset(stage.Name),
                stage.TargetDistance);
            _camera.transform.rotation = Quaternion.LookRotation(
                (target - _camera.transform.position).normalized,
                Vector3.up);

            Debug.Log(
                $"FARWORLD_VALIDATION view={stage.Name}: camera={_camera.transform.position}, " +
                $"targetDistance={stage.TargetDistance:0}m.");
        }

        private static float TargetHeightOffset(string viewName)
        {
            return viewName switch
            {
                "near" => 35f,
                "handoff" => 45f,
                "1km" => 55f,
                "3km" => 75f,
                "6km" => 90f,
                _ => 105f,
            };
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 390f, 78f), GUIContent.none);
            GUI.Label(new Rect(32f, 28f, 350f, 24f), "FAR WORLD RENDERING VALIDATION");
            GUI.Label(new Rect(32f, 52f, 350f, 22f),
                $"view: {_currentView}   features: {_featureRenderer?.InstanceCount ?? 0}");
            GUI.Label(new Rect(32f, 72f, 350f, 20f),
                "dense near terrain | coarse far terrain | semantic proxy HLOD");
        }

        private void OnDestroy()
        {
            if (_nearTerrainMesh != null) DestroyImmediate(_nearTerrainMesh);
            if (_farTerrainMesh != null) DestroyImmediate(_farTerrainMesh);
            if (_terrainMaterial != null) DestroyImmediate(_terrainMaterial);
            _nearTerrainMesh = null;
            _farTerrainMesh = null;
            _terrainMaterial = null;
        }
    }
}
