using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ProceduralGrassBillboardTests
    {
        [Test, Timeout(30000)]
        public void GalleryMeadowPacksDeterministicRegionalRibbonsAndPreservesFallbackKinds()
        {
            var host = new GameObject("Worldbuilding gallery meadow regression");
            var player = new GameObject("Worldbuilding gallery meadow regression player");
            WorldbuildingGalleryMeadowRenderer renderer = host.AddComponent<WorldbuildingGalleryMeadowRenderer>();
            var fallback = new CapturingVegetationRenderer();
            List<VegetationInstance> semantic = BuildRegionalSemanticBatch();

            try
            {
                renderer.Publish(semantic, fallback, player.transform);
                Mesh first = GetPackedMesh(renderer);

                Assert.That(first, Is.Not.Null);
                Assert.That(renderer.BladeCount, Is.GreaterThan(0),
                    "The production gallery batch must construct visible meadow blades.");
                Assert.That(first.vertexCount, Is.EqualTo(renderer.BladeCount * 10),
                    "Four ribbon segments require five two-vertex rows per packed blade.");
                Assert.That(first.GetIndexCount(0) / 3, Is.EqualTo((uint)(renderer.BladeCount * 8)),
                    "Every packed blade must remain eight opaque ribbon triangles.");

                Assert.That(fallback.Instances.Count, Is.EqualTo(3));
                Assert.That(fallback.Instances[0].Kind, Is.EqualTo(VegetationKind.Flower));
                Assert.That(fallback.Instances[1].Kind, Is.EqualTo(VegetationKind.Reed));
                Assert.That(fallback.Instances[2].Kind, Is.EqualTo(VegetationKind.DeadGrass));

                var roots = new List<Vector2>();
                var shape = new List<Vector2>();
                var phases = new List<Vector2>();
                first.GetUVs(0, roots);
                first.GetUVs(2, shape);
                first.GetUVs(3, phases);
                Color[] firstColors = first.colors;
                Vector3[] firstVertices = first.vertices;

                Assert.That(roots.Count, Is.EqualTo(first.vertexCount));
                Assert.That(shape.Count, Is.EqualTo(first.vertexCount));
                Assert.That(phases.Count, Is.EqualTo(first.vertexCount));
                Assert.That(RangeX(roots), Is.GreaterThan(2f),
                    "Packed roots must retain world/regional placement rather than repeat one local tuft.");
                Assert.That(RangeY(roots), Is.GreaterThan(2f),
                    "Packed roots must span coherent world-space regions in both horizontal axes.");
                Assert.That(RangeTip(shape), Is.EqualTo(1f).Within(0.0001f),
                    "The packed shape channel must preserve rigid roots and fully weighted tips.");
                Assert.That(GreenRange(firstColors), Is.GreaterThan(0.05f),
                    "Regional colour and ground-shade fields must produce multiple green tonal regions.");
                Assert.That(PhaseRange(phases), Is.GreaterThan(0.5f),
                    "Per-blade phase must vary so local wind does not animate every blade uniformly.");

                renderer.Publish(semantic, fallback, player.transform);
                Mesh second = GetPackedMesh(renderer);
                Assert.That(second.vertexCount, Is.EqualTo(firstVertices.Length));
                Assert.That(second.colors.Length, Is.EqualTo(firstColors.Length));
                Vector3[] secondVertices = second.vertices;
                Color[] secondColors = second.colors;
                for (int i = 0; i < firstVertices.Length; i++)
                {
                    Assert.That(secondVertices[i], Is.EqualTo(firstVertices[i]),
                        $"Construction must be deterministic at vertex {i}.");
                    Assert.That(secondColors[i], Is.EqualTo(firstColors[i]),
                        $"Regional colour must be deterministic at vertex {i}.");
                }

                Shader shader = Shader.Find(WorldbuildingGalleryMeadowRenderer.ShaderName);
                Assert.That(shader, Is.Not.Null, "The gallery meadow shader must import in PlayMode.");
                Assert.That(shader.isSupported, Is.True, "The gallery meadow shader must compile for the active graphics API.");
                using var material = new Material(shader);
                Assert.That(material.HasProperty("_GrassPlayerPositionWS"), Is.True);
                Assert.That(material.HasProperty("_GrassCameraRightWS"), Is.True);
                Assert.That(material.HasProperty("_GrassPushRadius"), Is.True);
                Assert.That(material.HasProperty("_GrassTime"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(host);
            }
        }

        [Test, Timeout(30000)]
        public void GalleryMeadowShaderPushesLocallyAndRecoversAtFixedTime()
        {
            var host = new GameObject("Worldbuilding gallery meadow push regression");
            WorldbuildingGalleryMeadowRenderer renderer = host.AddComponent<WorldbuildingGalleryMeadowRenderer>();
            var filter = host.AddComponent<MeshFilter>();
            var meshRenderer = host.AddComponent<MeshRenderer>();
            var cameraObject = new GameObject("Worldbuilding gallery meadow push camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(192, 192, 24, RenderTextureFormat.ARGB32);
            var baseline = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var pushed = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var recovered = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            Material material = null;

            try
            {
                VegetationInstance tuft = FindVisibleTuft(renderer);
                Mesh mesh = GetPackedMesh(renderer);
                renderer.enabled = false;
                filter.sharedMesh = mesh;

                Shader shader = Shader.Find(WorldbuildingGalleryMeadowRenderer.ShaderName);
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.isSupported, Is.True);
                material = new Material(shader);
                meshRenderer.sharedMaterial = material;

                var roots = new List<Vector2>();
                var packedRoot = new List<Vector2>();
                mesh.GetUVs(0, roots);
                mesh.GetUVs(1, packedRoot);
                Assert.That(roots.Count, Is.GreaterThan(0));
                Vector3 root = new(roots[0].x, packedRoot[0].x, roots[0].y);

                target.Create();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 0.75f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                camera.transform.position = root + new Vector3(0f, 0.30f, -3f);
                camera.transform.LookAt(root + Vector3.up * 0.30f, Vector3.up);

                material.SetFloat("_GrassTime", 0f);
                material.SetFloat("_GrassPushRadius", 1.05f);
                material.SetVector("_GrassCameraRightWS", new Vector4(1f, 0f, 0f, 0f));
                material.SetVector("_GrassPlayerPositionWS", new Vector4(100000f, 0f, 100000f, 1f));
                Render(camera, target, baseline);
                PixelStats baselineStats = AnalyzeGrass(baseline);
                Assert.That(baselineStats.PixelCount, Is.GreaterThan(80),
                    "The real packed meadow shader must produce a readable opaque tuft.");

                material.SetVector("_GrassPlayerPositionWS",
                    new Vector4(root.x - 0.60f, root.y, root.z, 1f));
                Render(camera, target, pushed);
                PixelStats pushedStats = AnalyzeGrass(pushed);
                Assert.That(pushedStats.PixelCount, Is.GreaterThan(50));
                Assert.That(pushedStats.CentroidX - baselineStats.CentroidX, Is.GreaterThan(0.5f),
                    $"A nearby player should displace the tuft away locally; baseline={baselineStats.CentroidX:F2}, pushed={pushedStats.CentroidX:F2}.");

                material.SetVector("_GrassPlayerPositionWS", new Vector4(100000f, 0f, 100000f, 1f));
                Render(camera, target, recovered);
                PixelStats recoveredStats = AnalyzeGrass(recovered);
                Assert.That(Mathf.Abs(recoveredStats.CentroidX - baselineStats.CentroidX), Is.LessThan(0.1f),
                    "At fixed shader time, moving the player away must recover the baseline silhouette.");
                Assert.That(PixelDifference(baseline, recovered), Is.LessThanOrEqualTo(4),
                    "Stateless recovery should return to the same pixels when time and camera are fixed.");

                Assert.That(tuft.Kind, Is.EqualTo(VegetationKind.Grass));
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(baseline);
                Object.DestroyImmediate(pushed);
                Object.DestroyImmediate(recovered);
                if (material != null) Object.DestroyImmediate(material);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(host);
            }
        }

        private static List<VegetationInstance> BuildRegionalSemanticBatch()
        {
            var values = new List<VegetationInstance>();
            uint seed = 1;
            for (int z = -4; z <= 4; z++)
            for (int x = -4; x <= 4; x++)
            {
                values.Add(Instance(VegetationKind.Grass, x * 2f, z * 2f, seed++));
            }

            values.Add(Instance(VegetationKind.Clover, -3f, 1f, seed++));
            values.Add(Instance(VegetationKind.Weed, 3f, 1f, seed++));
            values.Add(Instance(VegetationKind.Nettle, 0f, -3f, seed++));
            values.Add(Instance(VegetationKind.Flower, 20f, 0f, seed++));
            values.Add(Instance(VegetationKind.Reed, 22f, 0f, seed++));
            values.Add(Instance(VegetationKind.DeadGrass, 24f, 0f, seed));
            return values;
        }

        private static VegetationInstance FindVisibleTuft(WorldbuildingGalleryMeadowRenderer renderer)
        {
            uint seed = 101;
            for (int z = -3; z <= 3; z++)
            for (int x = -3; x <= 3; x++)
            {
                VegetationInstance candidate = Instance(VegetationKind.Grass, x * 2f, z * 2f, seed++);
                renderer.Rebuild(new[] { candidate });
                if (renderer.BladeCount > 0) return candidate;
            }

            Assert.Fail("No visible deterministic tuft was found in the regression search grid.");
            return default;
        }

        private static VegetationInstance Instance(VegetationKind kind, float x, float z, uint seed) =>
            new()
            {
                PositionMetres = new float3(x, 0f, z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                Kind = kind,
                Seed = seed,
                Scale = 1f,
            };

        private static Mesh GetPackedMesh(WorldbuildingGalleryMeadowRenderer renderer)
        {
            FieldInfo meshField = typeof(WorldbuildingGalleryMeadowRenderer).GetField(
                "_mesh", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(meshField, Is.Not.Null);
            return (Mesh)meshField.GetValue(renderer);
        }

        private static float RangeX(List<Vector2> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++) { min = Mathf.Min(min, values[i].x); max = Mathf.Max(max, values[i].x); }
            return max - min;
        }

        private static float RangeY(List<Vector2> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++) { min = Mathf.Min(min, values[i].y); max = Mathf.Max(max, values[i].y); }
            return max - min;
        }

        private static float RangeTip(List<Vector2> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++) { min = Mathf.Min(min, values[i].y); max = Mathf.Max(max, values[i].y); }
            return max - min;
        }

        private static float PhaseRange(List<Vector2> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++) { min = Mathf.Min(min, values[i].x); max = Mathf.Max(max, values[i].x); }
            return max - min;
        }

        private static float GreenRange(Color[] values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Length; i++) { min = Mathf.Min(min, values[i].g); max = Mathf.Max(max, values[i].g); }
            return max - min;
        }

        private static void Render(Camera camera, RenderTexture target, Texture2D destination)
        {
            camera.Render();
            RenderTexture.active = target;
            destination.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            destination.Apply(false, false);
        }

        private static PixelStats AnalyzeGrass(Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            int count = 0;
            double weightedX = 0;
            for (int y = 0; y < image.height; y++)
            for (int x = 0; x < image.width; x++)
            {
                Color32 pixel = pixels[y * image.width + x];
                if (pixel.g <= 20 || pixel.g <= pixel.r + 3 || pixel.g <= pixel.b + 3) continue;
                count++;
                weightedX += x;
            }
            return new PixelStats(count, count == 0 ? 0f : (float)(weightedX / count));
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

        private sealed class CapturingVegetationRenderer : IVegetationBatchRenderer
        {
            public readonly List<VegetationInstance> Instances = new();
            public int InstanceCount => Instances.Count;
            public bool enabled { get; set; } = true;

            public void SetInstances(IReadOnlyList<VegetationInstance> instances)
            {
                Instances.Clear();
                if (instances == null) return;
                for (int i = 0; i < instances.Count; i++) Instances.Add(instances[i]);
            }

            public void Clear() => Instances.Clear();
            public void DrawNow() { }
        }

        private readonly struct PixelStats
        {
            public readonly int PixelCount;
            public readonly float CentroidX;

            public PixelStats(int pixelCount, float centroidX)
            {
                PixelCount = pixelCount;
                CentroidX = centroidX;
            }
        }
    }
}
