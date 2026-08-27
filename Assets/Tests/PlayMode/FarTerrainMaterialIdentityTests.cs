using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class FarTerrainMaterialIdentityTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        private static IEnumerator LoadShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static List<Mesh> RingMeshes(VoxelFarTerrain far) =>
            (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

        private static object MaterialRoles(VoxelFarTerrain far)
        {
            PropertyInfo property = typeof(VoxelFarTerrain).GetProperty(
                "MaterialRoles", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "VoxelFarTerrain.MaterialRoles must be available.");
            object roles = property.GetValue(far);
            Assert.That(roles, Is.Not.Null, "VoxelFarTerrain.MaterialRoles must be initialized.");
            return roles;
        }

        private static int ReadRole(object roles, string name)
        {
            Type type = roles.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return Convert.ToInt32(property.GetValue(roles));

            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"{type.FullName}.{name} must be available.");
            return Convert.ToInt32(field.GetValue(roles));
        }

        private static int SurfaceAt(object roles, int height)
        {
            MethodInfo method = roles.GetType().GetMethod(
                "SurfaceAt", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int), typeof(int) }, null);
            Assert.That(method, Is.Not.Null, "Material role set must expose SurfaceAt(int, int).");
            return Convert.ToInt32(method.Invoke(
                roles, new object[] { height, ShowcaseWorld.BaseHeightVoxels }));
        }

        [UnityTest]
        public IEnumerator FarTerrainCarriesAuthoritativeMaterialIds()
        {
            yield return LoadShowcase();

            var far = UnityEngine.Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.That(far, Is.Not.Null, "VoxelShowcase did not create VoxelFarTerrain.");

            for (int frame = 0; frame < 600 && !far.HasSampledHeightsForEveryRing; frame++)
                yield return null;

            Assert.That(far.HasSampledHeightsForEveryRing, Is.True,
                "Far-terrain rings never finished publishing sampled heights.");

            List<Mesh> meshes = RingMeshes(far);
            Assert.That(meshes, Is.Not.Null.And.Not.Empty,
                "No generated far-terrain ring meshes were available to inspect.");

            object roles = MaterialRoles(far);
            int farStructure = ReadRole(roles, "FarStructure");
            int checkedTerrainVertices = 0;

            foreach (Mesh mesh in meshes)
            {
                if (mesh == null || mesh.vertexCount == 0) continue;

                Vector3[] vertices = mesh.vertices;
                Vector2[] materialIds = mesh.uv2;
                Assert.That(materialIds.Length, Is.EqualTo(vertices.Length),
                    $"{mesh.name} must carry one authoritative material id in UV2 per vertex.");

                for (int i = 0; i < vertices.Length; i++)
                {
                    int actual = Mathf.RoundToInt(materialIds[i].x);
                    if (actual == farStructure) continue;

                    int height = Mathf.RoundToInt(vertices[i].y / ShowcaseWorld.VoxelSize);
                    int expected = SurfaceAt(roles, height);

                    Assert.That(actual, Is.EqualTo(expected),
                        $"{mesh.name} vertex {i} at {vertices[i]} carries material id {actual}, "
                      + $"but the shared surface-role contract requires {expected}.");
                    checkedTerrainVertices++;
                }
            }

            Assert.That(checkedTerrainVertices, Is.GreaterThan(0),
                "No non-structure far-terrain vertices were checked.");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator FarTerrainHonorsLuminanceOnlyMaterialPresentation()
        {
            Shader shader = Shader.Find("VoxelEngine/FarTerrain");
            Assert.That(shader, Is.Not.Null, "Production far-terrain shader must be available in PlayMode.");

            var cameraObject = new GameObject("Far terrain presentation regression camera");
            var surfaceObject = new GameObject("Far terrain presentation regression surface");
            var camera = cameraObject.AddComponent<Camera>();
            var filter = surfaceObject.AddComponent<MeshFilter>();
            var renderer = surfaceObject.AddComponent<MeshRenderer>();
            var mesh = BuildPresentationQuad();
            var material = new Material(shader);
            var textureArray = new Texture2DArray(4, 4, 1, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                var white = new Color[16];
                for (int i = 0; i < white.Length; i++) white[i] = Color.white;
                textureArray.SetPixels(white, 0, 0);
                textureArray.Apply(false, false);

                var sampling = new Vector4[32];
                var surface = new Vector4[32];
                var variation = new Vector4[32];
                sampling[0] = new Vector4(0f, 0f, 1f, 1f);
                surface[0] = new Vector4(1f, 0f, 0.88f, 1f);
                variation[0] = new Vector4(0.66f, 0.58f, 0.10f, 0f);

                material.SetTexture("_AlbedoTextures", textureArray);
                material.SetVectorArray("_MaterialSampling", sampling);
                material.SetVectorArray("_MaterialSurface", surface);
                material.SetVectorArray("_MaterialVariation", variation);
                material.SetFloat("_VoxelSize", 0.1f);
                material.SetFloat("_AerialDistance", 100000f);

                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;

                target.Create();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 0.8f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                camera.transform.position = new Vector3(0f, 2f, 0f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                yield return null;
                yield return new WaitForEndOfFrame();

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                image.Apply(false, false);
                Color pixel = image.GetPixel(target.width / 2, target.height / 2);

                Assert.That(pixel.maxColorComponent, Is.GreaterThan(0.05f),
                    "Far-terrain regression quad did not render a visible center pixel.");
                Assert.That(pixel.g / Mathf.Max(pixel.r, 0.001f), Is.GreaterThan(1.35f),
                    $"A luminance-only material must retain its authored green hue when the source texture is white; got {pixel}. "
                  + "Direct raw-texture blending turns this pixel nearly neutral and recreates the near/far grass presentation split.");
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(image);
                UnityEngine.Object.Destroy(textureArray);
                UnityEngine.Object.Destroy(material);
                UnityEngine.Object.Destroy(mesh);
                UnityEngine.Object.Destroy(surfaceObject);
                UnityEngine.Object.Destroy(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator FarTerrainOwnsCanonicalBaseVoxelScaleOnItsMaterial()
        {
            yield return LoadShowcase();

            var far = UnityEngine.Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.That(far, Is.Not.Null, "VoxelShowcase did not create VoxelFarTerrain.");

            FieldInfo materialField = typeof(VoxelFarTerrain).GetField(
                "m_Material", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(materialField, Is.Not.Null, "VoxelFarTerrain material field must be available.");
            var material = materialField.GetValue(far) as Material;
            Assert.That(material, Is.Not.Null, "VoxelFarTerrain did not create its draw material.");
            Assert.That(material.HasProperty("_VoxelSize"), Is.True,
                "Far terrain is drawn outside VoxelRenderPass, so its material must own the base-voxel scale instead of inheriting a render-pass global.");

            float previousGlobal = Shader.GetGlobalFloat("_VoxelSize");
            Shader.SetGlobalFloat("_VoxelSize", 1f);
            try
            {
                Assert.That(material.GetFloat("_VoxelSize"),
                    Is.EqualTo(ShowcaseWorld.VoxelSize).Within(0.0001f),
                    "Far-terrain world-to-voxel UV conversion must remain 0.1 m per voxel even if unrelated global shader state changes.");
            }
            finally
            {
                Shader.SetGlobalFloat("_VoxelSize", previousGlobal);
            }
        }

        private static Mesh BuildPresentationQuad()
        {
            var mesh = new Mesh { name = "Far terrain presentation regression quad" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, 0f, -1f),
                new Vector3( 1f, 0f, -1f),
                new Vector3( 1f, 0f,  1f),
                new Vector3(-1f, 0f,  1f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            Color grass = new Color(0.28f, 0.46f, 0.20f, 1f);
            mesh.colors = new[] { grass, grass, grass, grass };
            mesh.uv2 = new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
