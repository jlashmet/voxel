using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ProceduralGrassBillboardTests
    {
        [Test, Timeout(30000)]
        public void MeadowTuftsUseSolidRibbonTopologyWithoutRestylingFlowersOrWetlandPlants()
        {
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Grass).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Grass));
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Clover).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Grass));
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Weed).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Grass));
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Nettle).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Grass));
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.DeadGrass).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Grass));

            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Flower).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Foliage), "Flowers must stay on their existing foliage renderer.");
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.WaterGrass).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Foliage), "Aquatic grass is outside the marked meadow fix.");
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Reed).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Foliage), "Wetland reeds are outside the marked meadow fix.");

            Shader shader = Shader.Find(ProceduralVegetationMaterials.GrassShaderName);
            Assert.That(shader, Is.Not.Null, "The dedicated production grass shader must import in PlayMode.");

            Mesh grassMesh = GetProductionGrassMesh();
            Assert.That(grassMesh, Is.Not.Null);
            Assert.That(grassMesh.name, Does.Contain("Solid Meadow Grass Ribbons"));
            Assert.That(grassMesh.vertexCount, Is.EqualTo(110),
                "Eleven independently packed blades with five vertex rows each should stay construction-time geometry.");
            Assert.That(grassMesh.triangles.Length / 3, Is.EqualTo(88),
                "Each blade must remain four solid ribbon segments; no transparent billboard sections are allowed.");
            Assert.That(grassMesh.uv2.Length, Is.EqualTo(grassMesh.vertexCount),
                "Every ribbon vertex carries stable per-blade phase/variation data for GPU deformation.");
        }

        [Test, Timeout(30000)]
        public void GrassRibbonsStayReadableAcrossOrbitAndRecoverAfterInteractorLeaves()
        {
            Shader shader = Shader.Find(ProceduralVegetationMaterials.GrassShaderName);
            Assert.That(shader, Is.Not.Null, "Production grass shader must be available in PlayMode.");
            Assert.That(shader.isSupported, Is.True, "Production grass shader must compile for the active test graphics API.");

            var cameraObject = new GameObject("Grass ribbon regression camera");
            var grassObject = new GameObject("Grass ribbon regression tuft");
            var camera = cameraObject.AddComponent<Camera>();
            var filter = grassObject.AddComponent<MeshFilter>();
            var renderer = grassObject.AddComponent<MeshRenderer>();
            Mesh mesh = GetProductionGrassMesh();
            Material material = ProceduralVegetationMaterials.MaterialFor(VegetationKind.Grass);
            var block = new MaterialPropertyBlock();
            var target = new RenderTexture(192, 192, 24, RenderTextureFormat.ARGB32);
            var front = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var side = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var pushed = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var recovered = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                Assert.That(material, Is.Not.Null);
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                ProceduralVegetationMaterials.Configure(block, VegetationKind.Grass);
                block.SetFloat("_WindStrength", 0f);
                renderer.SetPropertyBlock(block);

                material.SetFloat("_UseValidationAnimationTime", 1f);
                material.SetFloat("_ValidationAnimationTime", 0f);
                ProceduralVegetationMaterials.SetGrassInteractors(null);
                ProceduralVegetationMaterials.ApplyLighting();

                target.Create();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 0.72f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;

                SetView(camera.transform, new Vector3(0f, 0.48f, -3f));
                Render(camera, target, front);
                GrassPixelStats frontStats = AnalyzeGrass(front);
                AssertReadableGrass(frontStats, "front");

                SetView(camera.transform, new Vector3(3f, 0.48f, 0f));
                Render(camera, target, side);
                GrassPixelStats sideStats = AnalyzeGrass(side);
                AssertReadableGrass(sideStats, "side");
                Assert.That(sideStats.PixelCount, Is.InRange(
                        Mathf.RoundToInt(frontStats.PixelCount * 0.50f),
                        Mathf.RoundToInt(frontStats.PixelCount * 1.80f)),
                    $"World-fixed radial ribbons should remain readable through a 90-degree orbit; " +
                    $"front={frontStats.PixelCount}, side={sideStats.PixelCount}.");

                SetView(camera.transform, new Vector3(0f, 0.48f, -3f));
                ProceduralVegetationMaterials.SetGrassInteractors(new[]
                {
                    new Vector4(-0.22f, 0f, 0f, 0.78f),
                });
                ProceduralVegetationMaterials.ApplyLighting();
                Render(camera, target, pushed);
                GrassPixelStats pushedStats = AnalyzeGrass(pushed);

                ProceduralVegetationMaterials.SetGrassInteractors(null);
                ProceduralVegetationMaterials.ApplyLighting();
                Render(camera, target, recovered);
                GrassPixelStats recoveredStats = AnalyzeGrass(recovered);

                Assert.That(pushedStats.CentroidX - frontStats.CentroidX, Is.GreaterThan(2.0f),
                    $"A nearby character should push the upper ribbons away locally; baseline x={frontStats.CentroidX:F2}, " +
                    $"pushed x={pushedStats.CentroidX:F2}.");
                Assert.That(Mathf.Abs(recoveredStats.CentroidX - frontStats.CentroidX), Is.LessThan(0.75f),
                    $"Clearing the interactor must recover the original GPU-deformed silhouette; baseline x={frontStats.CentroidX:F2}, " +
                    $"recovered x={recoveredStats.CentroidX:F2}.");
                Assert.That(PixelDifference(front, recovered), Is.LessThan(64),
                    "With deterministic validation time and no interactor, recovery should return almost exactly to baseline.");
            }
            finally
            {
                ProceduralVegetationMaterials.SetGrassInteractors(null);
                ProceduralVegetationMaterials.ApplyLighting();
                if (material != null)
                {
                    material.SetFloat("_UseValidationAnimationTime", 0f);
                    material.SetFloat("_ValidationAnimationTime", 0f);
                }
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(front);
                UnityEngine.Object.DestroyImmediate(side);
                UnityEngine.Object.DestroyImmediate(pushed);
                UnityEngine.Object.DestroyImmediate(recovered);
                UnityEngine.Object.DestroyImmediate(grassObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static Mesh GetProductionGrassMesh()
        {
            Assembly assembly = typeof(ProceduralVegetationBatchRenderer).Assembly;
            Type meshLibrary = assembly.GetType(
                "VoxelEngine.Rendering.Runtime.Vegetation.ProceduralVegetationMeshLibrary",
                throwOnError: true);
            MethodInfo meshFor = meshLibrary.GetMethod(
                "MeshFor", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(meshFor, Is.Not.Null);
            return (Mesh)meshFor.Invoke(null, new object[]
            {
                VegetationShaderClass.Grass,
                VegetationGrowthForm.Tuft,
            });
        }

        private static void AssertReadableGrass(GrassPixelStats stats, string view)
        {
            Assert.That(stats.PixelCount, Is.GreaterThan(220),
                $"The {view} view produced only {stats.PixelCount} readable grass pixels.");
            Assert.That(stats.Width, Is.GreaterThan(10),
                $"The {view} grass collapsed to an edge-on strip; width={stats.Width}.");
            Assert.That(stats.Height, Is.GreaterThan(20),
                $"The {view} grass lost its layered blade height; height={stats.Height}.");
        }

        private static void SetView(Transform cameraTransform, Vector3 position)
        {
            cameraTransform.position = position;
            cameraTransform.LookAt(new Vector3(0f, 0.34f, 0f), Vector3.up);
        }

        private static void Render(Camera camera, RenderTexture target, Texture2D destination)
        {
            camera.Render();
            RenderTexture.active = target;
            destination.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            destination.Apply(false, false);
        }

        private static GrassPixelStats AnalyzeGrass(Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            int width = image.width;
            int height = image.height;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            int count = 0;
            double weightedX = 0;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!IsGrass(pixels[y * width + x])) continue;
                count++;
                weightedX += x;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            if (count == 0)
                return new GrassPixelStats(0, 0, 0, 0f);

            return new GrassPixelStats(
                count,
                maxX - minX + 1,
                maxY - minY + 1,
                (float)(weightedX / count));
        }

        private static int PixelDifference(Texture2D a, Texture2D b)
        {
            Color32[] left = a.GetPixels32();
            Color32[] right = b.GetPixels32();
            int changed = 0;
            for (int i = 0; i < left.Length; i++)
            {
                int delta = Mathf.Abs(left[i].r - right[i].r)
                            + Mathf.Abs(left[i].g - right[i].g)
                            + Mathf.Abs(left[i].b - right[i].b);
                if (delta > 6) changed++;
            }
            return changed;
        }

        private static bool IsGrass(Color32 pixel) =>
            pixel.g > 20 && pixel.g > pixel.r + 3 && pixel.g > pixel.b + 3;

        private readonly struct GrassPixelStats
        {
            public readonly int PixelCount;
            public readonly int Width;
            public readonly int Height;
            public readonly float CentroidX;

            public GrassPixelStats(int pixelCount, int width, int height, float centroidX)
            {
                PixelCount = pixelCount;
                Width = width;
                Height = height;
                CentroidX = centroidX;
            }
        }
    }
}
