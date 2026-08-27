using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ProceduralGrassBillboardTests
    {
        [Test, Timeout(30000)]
        public void GrassSilhouetteRemainsCompactAndReadableAcrossCameraAzimuths()
        {
            VegetationRenderStyle grassStyle = ProceduralVegetationMaterials.StyleFor(VegetationKind.Grass);
            VegetationRenderStyle cloverStyle = ProceduralVegetationMaterials.StyleFor(VegetationKind.Clover);
            VegetationRenderStyle waterGrassStyle = ProceduralVegetationMaterials.StyleFor(VegetationKind.WaterGrass);
            Assert.That(grassStyle.Shape, Is.EqualTo(5f),
                "Semantic Grass keeps its dedicated pixel-sprite presentation discriminator.");
            Assert.That(cloverStyle.Shape, Is.EqualTo(0f),
                "Mundane grass-like tufts must remain on the production foliage shape-0 bucket.");
            Assert.That(waterGrassStyle.Shape, Is.EqualTo(0f),
                "WaterGrass must exercise the same grass-like shape-0 presentation path.");

            Shader shader = Shader.Find(ProceduralVegetationMaterials.FoliageShaderName);
            Assert.That(shader, Is.Not.Null, "Production foliage shader must be available in PlayMode.");

            var cameraObject = new GameObject("Grass billboard regression camera");
            var bladeObject = new GameObject("Grass billboard regression blade");
            var camera = cameraObject.AddComponent<Camera>();
            var filter = bladeObject.AddComponent<MeshFilter>();
            var renderer = bladeObject.AddComponent<MeshRenderer>();
            var mesh = BuildQuad();
            var material = new Material(shader) { enableInstancing = true };
            var block = new MaterialPropertyBlock();
            var target = new RenderTexture(160, 160, 24, RenderTextureFormat.ARGB32);
            var front = new Texture2D(160, 160, TextureFormat.RGBA32, false);
            var side = new Texture2D(160, 160, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;

                // Render Clover rather than semantic Grass here: the saved gallery replay proved
                // that the marked patch can be any ecology-selected grass-like tuft. Shape 0 is the
                // production path shared by clover/weeds/nettles/reeds/cattails/dead/water grass.
                ProceduralVegetationMaterials.Configure(block, VegetationKind.Clover);
                block.SetFloat("_WindStrength", 0f);
                renderer.SetPropertyBlock(block);

                material.SetFloat("_UseValidationAnimationTime", 1f);
                material.SetFloat("_ValidationAnimationTime", 0f);

                target.Create();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 0.82f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;

                SetView(camera.transform, new Vector3(0f, 0.52f, -3f));
                Render(camera, target, front);

                SetView(camera.transform, new Vector3(3f, 0.52f, 0f));
                Render(camera, target, side);

                GrassPixelStats frontStats = AnalyzeGrass(front);
                GrassPixelStats sideStats = AnalyzeGrass(side);

                AssertReadablePixelGrass(frontStats, "front");
                AssertReadablePixelGrass(sideStats, "side");
                Assert.That(sideStats.PixelCount, Is.InRange(
                        Mathf.RoundToInt(frontStats.PixelCount * 0.80f),
                        Mathf.RoundToInt(frontStats.PixelCount * 1.25f)),
                    $"Camera-facing grass should keep nearly the same filled silhouette area across a 90-degree "
                    + $"camera change; front={frontStats.PixelCount}, side={sideStats.PixelCount}.");
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(front);
                Object.DestroyImmediate(side);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(bladeObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void AssertReadablePixelGrass(GrassPixelStats stats, string view)
        {
            Assert.That(stats.PixelCount, Is.GreaterThan(300),
                $"The {view} view produced only {stats.PixelCount} readable grass pixels.");
            Assert.That(stats.Width, Is.GreaterThanOrEqualTo(Mathf.RoundToInt(stats.Height * 0.72f)),
                $"The {view} silhouette is too narrow and reads as vertical bars instead of a compact leaf fan; "
                + $"bounds={stats.Width}x{stats.Height}.");
            Assert.That(stats.MaxHorizontalRuns, Is.GreaterThanOrEqualTo(3),
                $"The {view} silhouette must expose at least three separated blade runs through its middle; "
                + $"maxRuns={stats.MaxHorizontalRuns}.");
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Grass billboard regression quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.42f, 0f, 0f),
                new Vector3( 0.42f, 0f, 0f),
                new Vector3( 0.42f, 1f, 0f),
                new Vector3(-0.42f, 1f, 0f),
            };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
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

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!IsGrass(pixels[y * width + x])) continue;
                count++;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            if (count == 0)
                return new GrassPixelStats(0, 0, 0, 0);

            int boundsHeight = maxY - minY + 1;
            int firstRow = minY + Mathf.RoundToInt(boundsHeight * 0.32f);
            int lastRow = minY + Mathf.RoundToInt(boundsHeight * 0.72f);
            int maxRuns = 0;
            for (int y = firstRow; y <= lastRow; y++)
            {
                int runs = 0;
                bool inside = false;
                for (int x = minX; x <= maxX; x++)
                {
                    bool grass = IsGrass(pixels[y * width + x]);
                    if (grass && !inside) runs++;
                    inside = grass;
                }
                maxRuns = Mathf.Max(maxRuns, runs);
            }

            return new GrassPixelStats(count, maxX - minX + 1, boundsHeight, maxRuns);
        }

        private static bool IsGrass(Color32 pixel) =>
            pixel.g > 24 && pixel.g > pixel.r + 4 && pixel.g > pixel.b + 4;

        private readonly struct GrassPixelStats
        {
            public readonly int PixelCount;
            public readonly int Width;
            public readonly int Height;
            public readonly int MaxHorizontalRuns;

            public GrassPixelStats(int pixelCount, int width, int height, int maxHorizontalRuns)
            {
                PixelCount = pixelCount;
                Width = width;
                Height = height;
                MaxHorizontalRuns = maxHorizontalRuns;
            }
        }
    }
}
