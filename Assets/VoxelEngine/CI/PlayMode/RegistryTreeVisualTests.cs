using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Exercises the real runtime registry -> ProceduralTreeRenderer path in Play Mode. A healthy
    /// singleton must remain GameObject-free while still producing visible GPU-submitted geometry.
    /// </summary>
    public sealed class RegistryTreeVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;

        [UnityTest]
        public IEnumerator RegistryRenderer_RendersExactlyOneUprightTreeWithoutTreeGameObjects()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "RegistryTree");
            Directory.CreateDirectory(outputDirectory);

            GameObject cameraObject = null;
            GameObject groundObject = null;
            Material groundMaterial = null;
            RenderTexture target = null;
            Texture2D capture = null;
            Texture2D noTreeCapture = null;

            try
            {
                List<ProceduralTreeRenderer> bootstrapRenderers = null;
                for (int frame = 0; frame < 30; frame++)
                {
                    bootstrapRenderers = FindRuntimeRenderers();
                    if (bootstrapRenderers.Count > 0) break;
                    yield return null;
                }

                Assert.That(bootstrapRenderers, Is.Not.Null);
                Assert.That(bootstrapRenderers.Count, Is.EqualTo(1));
                ProceduralTreeRenderer renderer = bootstrapRenderers[0];

                var instance = new TreeInstance
                {
                    PositionMetres = float3.zero,
                    Species = TreeSpecies.Oak,
                    Seed = 0x00C0FFEEu,
                    Scale = 1f,
                };
                TreeWorldState.Replace(new[] { instance });

                for (int frame = 0;
                     frame < 60 && (renderer.PresentationCount != 1
                                    || renderer.BatchCount != 1
                                    || renderer.BatchedTreeCount != 1);
                     frame++)
                    yield return null;
                yield return null;

                Assert.That(renderer.PresentationCount, Is.EqualTo(1));
                Assert.That(renderer.BatchCount, Is.EqualTo(1));
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(1));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0));
                Assert.That(renderer.DynamicMeshCount, Is.EqualTo(0));
                Assert.That(renderer.BatchMeshCount, Is.EqualTo(4),
                            "Singleton batch must own LOD0/1/2 plus the ultra-far impostor.");
                Assert.That(renderer.GeneratedMeshCount, Is.EqualTo(4));
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));
                Assert.That(renderer.EstimatedVisibleDrawCount, Is.EqualTo(2));
                Assert.That(renderer.TryGetDynamicPresentationRoot(0, out _), Is.False);
                Assert.That(renderer.transform.childCount, Is.EqualTo(0),
                            "Standing tree renderer must not create batch/tree child GameObjects.");

                Assert.That(renderer.TryGetTreeBounds(0, out Bounds bounds), Is.True);
                Assert.That(bounds.size.y, Is.GreaterThan(bounds.size.x * 0.8f));
                Assert.That(bounds.min.y, Is.GreaterThan(-0.75f));

                groundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                groundObject.name = "CI Registry Ground Reference";
                groundObject.transform.position = new Vector3(0f, -0.025f, 0f);
                groundObject.transform.localScale = Vector3.one * 4f;
                Shader groundShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (groundShader != null)
                {
                    groundMaterial = new Material(groundShader) { name = "CI Registry Ground" };
                    groundMaterial.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.20f, 1f));
                    groundObject.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
                }

                cameraObject = new GameObject("CI Registry Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.52f, 0.60f, 0.70f, 1f);
                camera.fieldOfView = 34f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 200f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                Vector3 focus = bounds.center;
                float radius = Mathf.Max(bounds.extents.magnitude, 2f);
                Vector3 viewDirection = new Vector3(0.78f, 0.20f, -1f).normalized;
                cameraObject.transform.position = focus + viewDirection * (radius * 3.05f);
                cameraObject.transform.LookAt(focus + Vector3.up * (bounds.extents.y * 0.06f));

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Registry Tree Capture",
                    antiAliasing = 4,
                };
                target.Create();
                camera.targetTexture = target;

                // Give the direct-submit renderer a frame with this camera as Camera.main before
                // forcing the capture.
                yield return null;
                yield return null;

                RenderTexture previous = RenderTexture.active;
                int changedPixels;
                try
                {
                    camera.Render();
                    RenderTexture.active = target;
                    capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                    capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    capture.Apply(false, false);
                    byte[] png = capture.EncodeToPNG();
                    Assert.That(png, Is.Not.Null);
                    Assert.That(png.Length, Is.GreaterThan(0));
                    File.WriteAllBytes(Path.Combine(outputDirectory, "registry-tree.png"), png);

                    // Remove the semantic tree rather than disabling MeshRenderers: standing trees
                    // intentionally have none now.
                    TreeWorldState.Replace(System.Array.Empty<TreeInstance>());
                    for (int frame = 0; frame < 3; frame++) yield return null;

                    camera.Render();
                    RenderTexture.active = target;
                    noTreeCapture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                    noTreeCapture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    noTreeCapture.Apply(false, false);
                    changedPixels = CountChangedPixels(capture, noTreeCapture, 8);
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                Assert.That(changedPixels, Is.GreaterThan(512),
                            "Removing the semantic singleton did not remove enough pixels; " +
                            "the direct-submit tree is not demonstrably visible in the captured frame.");

                string metadata =
                    $"rendererInstances={FindRuntimeRenderers().Count}\n" +
                    $"standingRenderObjects=0\n" +
                    $"lodMeshesPerBatch=4\n" +
                    $"boundsCenter={bounds.center:F3}\n" +
                    $"boundsSize={bounds.size:F3}\n" +
                    $"boundsMinY={bounds.min.y:F3}\n" +
                    $"renderChangedPixels={changedPixels}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "registry-tree.txt"), metadata);
                Debug.Log($"CI registry-tree capture written to {outputDirectory}\n{metadata}");
            }
            finally
            {
                TreeWorldState.Replace(System.Array.Empty<TreeInstance>());
                if (capture != null) Object.Destroy(capture);
                if (noTreeCapture != null) Object.Destroy(noTreeCapture);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                if (cameraObject != null) Object.Destroy(cameraObject);
                if (groundObject != null) Object.Destroy(groundObject);
                if (groundMaterial != null) Object.Destroy(groundMaterial);
            }
        }

        private static int CountChangedPixels(Texture2D withTree, Texture2D withoutTree,
                                              int channelThreshold)
        {
            Color32[] withPixels = withTree.GetPixels32();
            Color32[] withoutPixels = withoutTree.GetPixels32();
            Assert.That(withPixels.Length, Is.EqualTo(withoutPixels.Length));

            int changed = 0;
            for (int i = 0; i < withPixels.Length; i++)
            {
                Color32 a = withPixels[i];
                Color32 b = withoutPixels[i];
                int maxDelta = Mathf.Max(
                    Mathf.Abs(a.r - b.r),
                    Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
                if (maxDelta >= channelThreshold) changed++;
            }
            return changed;
        }

        private static List<ProceduralTreeRenderer> FindRuntimeRenderers()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            var result = new List<ProceduralTreeRenderer>(all.Length);
            foreach (ProceduralTreeRenderer renderer in all)
            {
                if (renderer == null || renderer.gameObject == null) continue;
                if (!renderer.gameObject.scene.IsValid()) continue;
                result.Add(renderer);
            }
            return result;
        }
    }
}
