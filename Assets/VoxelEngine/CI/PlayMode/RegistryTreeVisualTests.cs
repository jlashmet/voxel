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
    /// Exercises the real runtime registry -> ProceduralTreeRenderer path in Play Mode.
    /// No Showcase world or legacy tree migration data is published, so any duplicate or
    /// orientation error here belongs to the semantic renderer lifecycle itself.
    /// </summary>
    public sealed class RegistryTreeVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;

        [UnityTest]
        public IEnumerator RegistryRenderer_RendersExactlyOneUprightTree()
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
                // The production bootstrap object is HideFlags.DontSave. Unity's normal
                // FindFirstObjectByType path may omit it even though the static singleton sees it,
                // so enumerate loaded runtime objects explicitly. This avoids accidentally creating
                // a second component that Awake() immediately destroys as a duplicate.
                List<ProceduralTreeRenderer> bootstrapRenderers = null;
                for (int frame = 0; frame < 30; frame++)
                {
                    bootstrapRenderers = FindRuntimeRenderers();
                    if (bootstrapRenderers.Count > 0) break;
                    yield return null;
                }

                Assert.That(bootstrapRenderers, Is.Not.Null);
                Assert.That(bootstrapRenderers.Count, Is.EqualTo(1),
                            "Production bootstrap must create exactly one ProceduralTreeRenderer.");
                ProceduralTreeRenderer renderer = bootstrapRenderers[0];

                var instance = new TreeInstance
                {
                    PositionMetres = float3.zero,
                    Species = TreeSpecies.Oak,
                    Seed = 0x00C0FFEEu,
                    Scale = 1f,
                };
                TreeWorldState.Replace(new[] { instance });

                // Give the production Update loop time to observe Version, build all three LODs,
                // and run its normal fallback-visibility pass.
                for (int frame = 0; frame < 30 && renderer != null && renderer.transform.childCount == 0; frame++)
                    yield return null;
                yield return null;
                yield return null;

                List<ProceduralTreeRenderer> renderers = FindRuntimeRenderers();
                Assert.That(renderers.Count, Is.EqualTo(1),
                            "Exactly one runtime ProceduralTreeRenderer must own vegetation.");
                renderer = renderers[0];
                Assert.That(renderer.transform.childCount, Is.EqualTo(1),
                            "One registry TreeInstance must produce exactly one presentation root.");

                Transform treeRoot = renderer.transform.GetChild(0);
                Assert.That(treeRoot.gameObject.activeSelf, Is.True,
                            "The isolated semantic tree should not be hidden by fallback ownership.");
                Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity), Is.LessThan(0.01f),
                            "Tree root must start with identity rotation.");
                Assert.That(Vector3.Distance(treeRoot.position, Vector3.zero), Is.LessThan(0.001f),
                            "Tree root must be placed at the published semantic position.");

                var lodGroup = treeRoot.GetComponent<LODGroup>();
                Assert.That(lodGroup, Is.Not.Null, "Production tree root must own one LODGroup.");

                MeshFilter[] filters = treeRoot.GetComponentsInChildren<MeshFilter>(true);
                MeshRenderer[] treeRenderers = treeRoot.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(filters.Length, Is.EqualTo(3), "Production tree should create exactly three LOD meshes.");
                Assert.That(treeRenderers.Length, Is.EqualTo(3), "Production tree should create exactly three LOD renderers.");

                int totalVertices = 0;
                int barkTriangles = 0;
                int leafTriangles = 0;
                Bounds bounds = default;
                bool hasBounds = false;
                for (int i = 0; i < filters.Length; i++)
                {
                    Mesh mesh = filters[i].sharedMesh;
                    Assert.That(mesh, Is.Not.Null, $"LOD{i} has no generated mesh.");
                    Assert.That(mesh.subMeshCount, Is.EqualTo(2), $"LOD{i} must have bark and leaf submeshes.");
                    totalVertices += mesh.vertexCount;
                    barkTriangles += (int)mesh.GetIndexCount(0) / 3;
                    leafTriangles += (int)mesh.GetIndexCount(1) / 3;
                }
                Assert.That(totalVertices, Is.GreaterThan(0));
                Assert.That(barkTriangles, Is.GreaterThan(0));
                Assert.That(leafTriangles, Is.GreaterThan(0));

                foreach (MeshRenderer meshRenderer in treeRenderers)
                {
                    if (!hasBounds)
                    {
                        bounds = meshRenderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(meshRenderer.bounds);
                    }
                }
                Assert.That(hasBounds, Is.True);
                Assert.That(bounds.size.y, Is.GreaterThan(bounds.size.x * 0.8f),
                            "A freshly generated tree should have a substantial vertical extent.");
                Assert.That(bounds.min.y, Is.GreaterThan(-0.75f),
                            "Fresh tree geometry should not extend sideways below its root like a fallen tree.");

                // Flat reference plane makes a 90-degree orientation failure visually obvious.
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

                // Let LODGroup evaluate against the real capture camera before rendering.
                yield return null;
                yield return null;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Registry Tree Capture",
                    antiAliasing = 4,
                };
                target.Create();
                camera.targetTexture = target;

                RenderTexture previous = RenderTexture.active;
                bool[] rendererEnabled = new bool[treeRenderers.Length];
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

                    // A non-empty PNG only proves the camera rendered something. Render the exact
                    // same frame again with the production tree renderers disabled and require a
                    // substantial pixel delta. This fails if the tree is transparent, black-holed by
                    // the shader, culled/off-camera, or otherwise absent despite valid mesh objects.
                    for (int i = 0; i < treeRenderers.Length; i++)
                    {
                        rendererEnabled[i] = treeRenderers[i].enabled;
                        treeRenderers[i].enabled = false;
                    }

                    camera.Render();
                    RenderTexture.active = target;
                    noTreeCapture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                    noTreeCapture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    noTreeCapture.Apply(false, false);
                    changedPixels = CountChangedPixels(capture, noTreeCapture, 8);
                }
                finally
                {
                    for (int i = 0; i < treeRenderers.Length; i++)
                        if (treeRenderers[i] != null) treeRenderers[i].enabled = rendererEnabled[i];
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                Assert.That(changedPixels, Is.GreaterThan(512),
                            "Disabling the production tree renderers did not remove enough pixels; " +
                            "the tree is not demonstrably visible in the captured frame.");

                string metadata =
                    $"registryInstances={TreeWorldState.Instances.Count}\n" +
                    $"rendererInstances={renderers.Count}\n" +
                    $"presentationRoots={renderer.transform.childCount}\n" +
                    $"rootName={treeRoot.name}\n" +
                    $"rootActive={treeRoot.gameObject.activeSelf}\n" +
                    $"rootPosition={treeRoot.position:F3}\n" +
                    $"rootLocalRotation={treeRoot.localEulerAngles:F3}\n" +
                    $"lodGroups=1\n" +
                    $"lodMeshes={filters.Length}\n" +
                    $"totalVertices={totalVertices}\n" +
                    $"barkTrianglesAllLods={barkTriangles}\n" +
                    $"leafTrianglesAllLods={leafTriangles}\n" +
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