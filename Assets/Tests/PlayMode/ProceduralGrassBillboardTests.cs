using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ProceduralGrassBillboardTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags InternalStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test, Timeout(30000)]
        public void SemanticGrassUsesPackedSpatialChunksAndLeavesOtherVegetationOnExistingPath()
        {
            var host = new GameObject("engine grass routing regression");
            ProceduralVegetationBatchRenderer renderer = host.AddComponent<ProceduralVegetationBatchRenderer>();
            List<VegetationInstance> semantic = BuildSemanticBatch();

            try
            {
                renderer.SetInstances(semantic);
                Assert.That(renderer.InstanceCount, Is.EqualTo(semantic.Count));
                Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Grass).ShaderClass,
                    Is.EqualTo(VegetationShaderClass.Grass));
                Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Flower).ShaderClass,
                    Is.Not.EqualTo(VegetationShaderClass.Grass));
                Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Reed).ShaderClass,
                    Is.Not.EqualTo(VegetationShaderClass.Grass));
                Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.DeadGrass).ShaderClass,
                    Is.Not.EqualTo(VegetationShaderClass.Grass));

                IDictionary fallbackBatches = GetFallbackBatches(renderer);
                Assert.That(fallbackBatches.Contains(VegetationKind.Grass), Is.False,
                    "Semantic grass must not reach the generic instanced card renderer.");
                Assert.That(fallbackBatches.Contains(VegetationKind.Flower), Is.True);
                Assert.That(fallbackBatches.Contains(VegetationKind.Reed), Is.True);
                Assert.That(fallbackBatches.Contains(VegetationKind.DeadGrass), Is.True);

                object grassBatch = GetGrassBatch(renderer);
                IList meshes = GetGrassMeshes(grassBatch);
                int bladeCount = GetIntProperty(grassBatch, "BladeCount");
                int vertexCount = GetIntProperty(grassBatch, "VertexCount");
                int triangleCount = GetIntProperty(grassBatch, "TriangleCount");
                int chunkCount = GetIntProperty(grassBatch, "ChunkCount");

                Assert.That(chunkCount, Is.EqualTo(meshes.Count));
                Assert.That(chunkCount, Is.GreaterThan(1),
                    "World grass must remain spatially chunked rather than become one scene-wide mesh.");
                Assert.That(bladeCount, Is.GreaterThan(0));
                Assert.That(vertexCount, Is.EqualTo(bladeCount * 10),
                    "Four ribbon segments require five two-vertex rows per packed blade.");
                Assert.That(triangleCount, Is.EqualTo(bladeCount * 8),
                    "Each blade must remain eight opaque ribbon triangles, not transparent cards.");

                var roots = new List<Vector2>();
                var packedShape = new List<Vector2>();
                var phases = new List<Vector2>();
                var allColors = new List<Color>();
                for (int i = 0; i < meshes.Count; i++)
                {
                    Mesh mesh = (Mesh)meshes[i];
                    Assert.That(mesh.name, Does.Contain("Procedural Grass Packed Chunk"));
                    var localRoots = new List<Vector2>();
                    var localShape = new List<Vector2>();
                    var localPhases = new List<Vector2>();
                    mesh.GetUVs(0, localRoots);
                    mesh.GetUVs(2, localShape);
                    mesh.GetUVs(3, localPhases);
                    Assert.That(localRoots.Count, Is.EqualTo(mesh.vertexCount));
                    Assert.That(localShape.Count, Is.EqualTo(mesh.vertexCount));
                    Assert.That(localPhases.Count, Is.EqualTo(mesh.vertexCount));
                    roots.AddRange(localRoots);
                    packedShape.AddRange(localShape);
                    phases.AddRange(localPhases);
                    allColors.AddRange(mesh.colors);
                }

                Assert.That(RangeX(roots), Is.GreaterThan(32f),
                    "Packed roots must preserve world placement across more than one grass chunk.");
                Assert.That(RangeY(roots), Is.GreaterThan(8f));
                Assert.That(RangeY(packedShape), Is.EqualTo(1f).Within(0.0001f),
                    "Root vertices must stay rigid while tips receive full GPU deformation weight.");
                Assert.That(RangeX(phases), Is.GreaterThan(0.5f),
                    "Per-blade phase must vary so coherent wind is not a uniform field translation.");
                Assert.That(GreenRange(allColors), Is.GreaterThan(0.05f),
                    "Construction must bake multiple regional green tones from the independent fields.");

                AssertIndependentRegionalFields(grassBatch);

                List<MeshSnapshot> baseline = Snapshot(meshes);
                renderer.DrawNow();
                IList afterDraw = GetGrassMeshes(GetGrassBatch(renderer));
                Assert.That(afterDraw.Count, Is.EqualTo(meshes.Count));
                for (int i = 0; i < meshes.Count; i++)
                    Assert.That(afterDraw[i], Is.SameAs(meshes[i]),
                        "Per-frame drawing must reuse construction-time grass meshes.");
                AssertSnapshotsEqual(baseline, Snapshot(afterDraw));

                renderer.SetInstances(semantic);
                AssertSnapshotsEqual(baseline, Snapshot(GetGrassMeshes(GetGrassBatch(renderer))));

                Shader shader = Shader.Find(ProceduralVegetationMaterials.GrassShaderName);
                Assert.That(shader, Is.Not.Null, "The supplied grass shader must be installed as an engine shader.");
                Assert.That(shader.isSupported, Is.True, "The engine grass shader must compile for the active graphics API.");
                Material material = ProceduralVegetationMaterials.MaterialFor(VegetationKind.Grass);
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo(ProceduralVegetationMaterials.GrassShaderName));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test, Timeout(30000)]
        public void GrassShaderStaysReadableAcrossOrbitPushesLocallyAndRecoversAtFixedTime()
        {
            var host = new GameObject("engine grass shader regression");
            ProceduralVegetationBatchRenderer renderer = host.AddComponent<ProceduralVegetationBatchRenderer>();
            var filter = host.AddComponent<MeshFilter>();
            var meshRenderer = host.AddComponent<MeshRenderer>();
            var cameraObject = new GameObject("engine grass shader regression camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(192, 192, 24, RenderTextureFormat.ARGB32);
            var front = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var side = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var pushed = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            var recovered = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            Material material = null;

            try
            {
                VegetationInstance tuft = FindVisibleGrass(renderer);
                renderer.SetInstances(new[] { tuft });
                IList meshes = GetGrassMeshes(GetGrassBatch(renderer));
                Assert.That(meshes.Count, Is.EqualTo(1));
                Mesh mesh = (Mesh)meshes[0];
                renderer.enabled = false;
                filter.sharedMesh = mesh;

                Shader shader = Shader.Find(ProceduralVegetationMaterials.GrassShaderName);
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.isSupported, Is.True);
                material = new Material(shader);
                meshRenderer.sharedMaterial = material;

                var roots = new List<Vector2>();
                var rootHeightAndLateral = new List<Vector2>();
                mesh.GetUVs(0, roots);
                mesh.GetUVs(1, rootHeightAndLateral);
                Assert.That(roots.Count, Is.GreaterThan(0));
                Vector3 root = new Vector3(roots[0].x, rootHeightAndLateral[0].x, roots[0].y);

                target.Create();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 0.76f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;

                material.SetFloat("_GrassTime", 0f);
                material.SetFloat("_GrassPushRadius", 1.05f);
                material.SetInt("_GrassInteractorCount", 0);
                material.SetVector("_GrassPlayerPositionWS", new Vector4(100000f, 0f, 100000f, 1f));

                SetView(camera.transform, root, new Vector3(0f, 0.30f, -3f));
                SetCameraRight(material, camera.transform);
                Render(camera, target, front);
                PixelStats frontStats = AnalyzeGrass(front);
                AssertReadable(frontStats, "front");

                SetView(camera.transform, root, new Vector3(3f, 0.30f, 0f));
                SetCameraRight(material, camera.transform);
                Render(camera, target, side);
                PixelStats sideStats = AnalyzeGrass(side);
                AssertReadable(sideStats, "90-degree orbit");
                Assert.That(sideStats.PixelCount, Is.InRange(
                        Mathf.RoundToInt(frontStats.PixelCount * 0.45f),
                        Mathf.RoundToInt(frontStats.PixelCount * 1.90f)),
                    $"Camera-right reconstruction should preserve a readable silhouette; front={frontStats.PixelCount}, side={sideStats.PixelCount}.");

                SetView(camera.transform, root, new Vector3(0f, 0.30f, -3f));
                SetCameraRight(material, camera.transform);
                material.SetVector("_GrassPlayerPositionWS", new Vector4(root.x - 0.60f, root.y, root.z, 1f));
                Render(camera, target, pushed);
                PixelStats pushedStats = AnalyzeGrass(pushed);
                Assert.That(pushedStats.PixelCount, Is.GreaterThan(50));
                Assert.That(pushedStats.CentroidX - frontStats.CentroidX, Is.GreaterThan(0.5f),
                    $"A nearby player should displace blade tips away locally; baseline={frontStats.CentroidX:F2}, pushed={pushedStats.CentroidX:F2}.");

                material.SetVector("_GrassPlayerPositionWS", new Vector4(100000f, 0f, 100000f, 1f));
                Render(camera, target, recovered);
                PixelStats recoveredStats = AnalyzeGrass(recovered);
                Assert.That(Mathf.Abs(recoveredStats.CentroidX - frontStats.CentroidX), Is.LessThan(0.1f),
                    "At fixed shader time, moving the player outside the local radius must recover the baseline silhouette.");
                Assert.That(PixelDifference(front, recovered), Is.LessThanOrEqualTo(4),
                    "The supplied stateless push equation should recover the same pixels when time and camera are fixed.");
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(front);
                Object.DestroyImmediate(side);
                Object.DestroyImmediate(pushed);
                Object.DestroyImmediate(recovered);
                if (material != null) Object.DestroyImmediate(material);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(host);
            }
        }

        private static List<VegetationInstance> BuildSemanticBatch()
        {
            var values = new List<VegetationInstance>();
            uint seed = 1;
            for (int z = -2; z <= 2; z++)
            for (int x = -3; x <= 3; x++)
                values.Add(Instance(VegetationKind.Grass, x * 18f, z * 9f, seed++));

            values.Add(Instance(VegetationKind.Flower, 4f, 2f, seed++));
            values.Add(Instance(VegetationKind.Reed, 6f, 2f, seed++));
            values.Add(Instance(VegetationKind.DeadGrass, 8f, 2f, seed));
            return values;
        }

        private static VegetationInstance FindVisibleGrass(ProceduralVegetationBatchRenderer renderer)
        {
            object grassBatch = GetGrassBatch(renderer);
            MethodInfo coverage = grassBatch.GetType().GetMethod("CoverageField", InternalStatic);
            Assert.That(coverage, Is.Not.Null);

            uint seed = 101;
            for (int z = -6; z <= 6; z += 2)
            for (int x = -6; x <= 6; x += 2)
            {
                float value = (float)coverage.Invoke(null, new object[] { (float)x, (float)z });
                if (value >= 0.45f)
                    return Instance(VegetationKind.Grass, x, z, seed);
                seed++;
            }

            Assert.Fail("No visible deterministic grass root was found in the regression search grid.");
            return default;
        }

        private static VegetationInstance Instance(VegetationKind kind, float x, float z, uint seed) =>
            new VegetationInstance
            {
                PositionMetres = new float3(x, 0f, z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                Kind = kind,
                Seed = seed,
                Scale = 1f,
            };

        private static object GetGrassBatch(ProceduralVegetationBatchRenderer renderer)
        {
            FieldInfo field = typeof(ProceduralVegetationBatchRenderer).GetField("_grass", PrivateInstance);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(renderer);
        }

        private static IList GetGrassMeshes(object grassBatch)
        {
            FieldInfo field = grassBatch.GetType().GetField("_meshes", PrivateInstance);
            Assert.That(field, Is.Not.Null);
            return (IList)field.GetValue(grassBatch);
        }

        private static IDictionary GetFallbackBatches(ProceduralVegetationBatchRenderer renderer)
        {
            FieldInfo field = typeof(ProceduralVegetationBatchRenderer).GetField("_batches", PrivateInstance);
            Assert.That(field, Is.Not.Null);
            return (IDictionary)field.GetValue(renderer);
        }

        private static int GetIntProperty(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, PrivateInstance);
            Assert.That(property, Is.Not.Null, name);
            return (int)property.GetValue(instance);
        }

        private static void AssertIndependentRegionalFields(object grassBatch)
        {
            MethodInfo coverage = grassBatch.GetType().GetMethod("CoverageField", InternalStatic);
            MethodInfo colour = grassBatch.GetType().GetMethod("ColourField", InternalStatic);
            MethodInfo ground = grassBatch.GetType().GetMethod("GroundShadeField", InternalStatic);
            Assert.That(coverage, Is.Not.Null);
            Assert.That(colour, Is.Not.Null);
            Assert.That(ground, Is.Not.Null);

            var coverageValues = new List<float>();
            var colourValues = new List<float>();
            var groundValues = new List<float>();
            for (int i = 0; i < 12; i++)
            {
                float x = -72f + i * 13.7f;
                float z = 41f - i * 9.3f;
                coverageValues.Add((float)coverage.Invoke(null, new object[] { x, z }));
                colourValues.Add((float)colour.Invoke(null, new object[] { x, z }));
                groundValues.Add((float)ground.Invoke(null, new object[] { x, z }));
            }

            Assert.That(Range(coverageValues), Is.GreaterThan(0.08f));
            Assert.That(Range(colourValues), Is.GreaterThan(0.08f));
            Assert.That(Range(groundValues), Is.GreaterThan(0.08f));
            Assert.That(MaxPairDifference(coverageValues, colourValues), Is.GreaterThan(0.02f));
            Assert.That(MaxPairDifference(coverageValues, groundValues), Is.GreaterThan(0.02f));
            Assert.That(MaxPairDifference(colourValues, groundValues), Is.GreaterThan(0.02f));
        }

        private static List<MeshSnapshot> Snapshot(IList meshes)
        {
            var result = new List<MeshSnapshot>(meshes.Count);
            for (int i = 0; i < meshes.Count; i++)
            {
                Mesh mesh = (Mesh)meshes[i];
                var uv0 = new List<Vector2>();
                var uv1 = new List<Vector2>();
                var uv2 = new List<Vector2>();
                var uv3 = new List<Vector2>();
                mesh.GetUVs(0, uv0);
                mesh.GetUVs(1, uv1);
                mesh.GetUVs(2, uv2);
                mesh.GetUVs(3, uv3);
                result.Add(new MeshSnapshot(
                    mesh.vertices,
                    mesh.colors,
                    uv0.ToArray(),
                    uv1.ToArray(),
                    uv2.ToArray(),
                    uv3.ToArray(),
                    mesh.triangles));
            }
            return result;
        }

        private static void AssertSnapshotsEqual(List<MeshSnapshot> expected, List<MeshSnapshot> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int meshIndex = 0; meshIndex < expected.Count; meshIndex++)
            {
                MeshSnapshot left = expected[meshIndex];
                MeshSnapshot right = actual[meshIndex];
                Assert.That(right.Vertices, Is.EqualTo(left.Vertices), $"vertices mesh {meshIndex}");
                Assert.That(right.Colors, Is.EqualTo(left.Colors), $"colors mesh {meshIndex}");
                Assert.That(right.Uv0, Is.EqualTo(left.Uv0), $"uv0 mesh {meshIndex}");
                Assert.That(right.Uv1, Is.EqualTo(left.Uv1), $"uv1 mesh {meshIndex}");
                Assert.That(right.Uv2, Is.EqualTo(left.Uv2), $"uv2 mesh {meshIndex}");
                Assert.That(right.Uv3, Is.EqualTo(left.Uv3), $"uv3 mesh {meshIndex}");
                Assert.That(right.Triangles, Is.EqualTo(left.Triangles), $"triangles mesh {meshIndex}");
            }
        }

        private static float RangeX(List<Vector2> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++)
            {
                min = Mathf.Min(min, values[i].x);
                max = Mathf.Max(max, values[i].x);
            }
            return max - min;
        }

        private static float RangeY(List<Vector2> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++)
            {
                min = Mathf.Min(min, values[i].y);
                max = Mathf.Max(max, values[i].y);
            }
            return max - min;
        }

        private static float GreenRange(List<Color> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++)
            {
                min = Mathf.Min(min, values[i].g);
                max = Mathf.Max(max, values[i].g);
            }
            return max - min;
        }

        private static float Range(List<float> values)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < values.Count; i++)
            {
                min = Mathf.Min(min, values[i]);
                max = Mathf.Max(max, values[i]);
            }
            return max - min;
        }

        private static float MaxPairDifference(List<float> left, List<float> right)
        {
            float max = 0f;
            for (int i = 0; i < left.Count; i++)
                max = Mathf.Max(max, Mathf.Abs(left[i] - right[i]));
            return max;
        }

        private static void SetView(Transform cameraTransform, Vector3 root, Vector3 offset)
        {
            cameraTransform.position = root + offset;
            cameraTransform.LookAt(root + Vector3.up * 0.30f, Vector3.up);
        }

        private static void SetCameraRight(Material material, Transform cameraTransform)
        {
            Vector3 right = cameraTransform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
            material.SetVector("_GrassCameraRightWS", new Vector4(right.x, 0f, right.z, 0f));
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
            int minX = image.width;
            int minY = image.height;
            int maxX = -1;
            int maxY = -1;
            double weightedX = 0;
            for (int y = 0; y < image.height; y++)
            for (int x = 0; x < image.width; x++)
            {
                Color32 pixel = pixels[y * image.width + x];
                if (pixel.g <= 20 || pixel.g <= pixel.r + 3 || pixel.g <= pixel.b + 3) continue;
                count++;
                weightedX += x;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            return count == 0
                ? new PixelStats(0, 0, 0, 0f)
                : new PixelStats(count, maxX - minX + 1, maxY - minY + 1, (float)(weightedX / count));
        }

        private static void AssertReadable(PixelStats stats, string view)
        {
            Assert.That(stats.PixelCount, Is.GreaterThan(80), $"The {view} view produced too few grass pixels.");
            Assert.That(stats.Width, Is.GreaterThan(8), $"The {view} grass collapsed to an edge-on strip.");
            Assert.That(stats.Height, Is.GreaterThan(18), $"The {view} grass lost its layered blade height.");
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

        private sealed class MeshSnapshot
        {
            public readonly Vector3[] Vertices;
            public readonly Color[] Colors;
            public readonly Vector2[] Uv0;
            public readonly Vector2[] Uv1;
            public readonly Vector2[] Uv2;
            public readonly Vector2[] Uv3;
            public readonly int[] Triangles;

            public MeshSnapshot(
                Vector3[] vertices,
                Color[] colors,
                Vector2[] uv0,
                Vector2[] uv1,
                Vector2[] uv2,
                Vector2[] uv3,
                int[] triangles)
            {
                Vertices = vertices;
                Colors = colors;
                Uv0 = uv0;
                Uv1 = uv1;
                Uv2 = uv2;
                Uv3 = uv3;
                Triangles = triangles;
            }
        }

        private readonly struct PixelStats
        {
            public readonly int PixelCount;
            public readonly int Width;
            public readonly int Height;
            public readonly float CentroidX;

            public PixelStats(int pixelCount, int width, int height, float centroidX)
            {
                PixelCount = pixelCount;
                Width = width;
                Height = height;
                CentroidX = centroidX;
            }
        }
    }
}
