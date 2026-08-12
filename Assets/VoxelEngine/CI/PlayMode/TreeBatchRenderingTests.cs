using System.Collections;
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
    /// Rendering contract for spatial tree batching. Healthy trees must contribute real pixels
    /// without retaining per-tree GameObjects or meshes; first damage lazily materializes only the
    /// affected tree while the rest of the forest stays batched.
    /// </summary>
    public sealed class TreeBatchRenderingTests
    {
        private const int Width = 768;
        private const int Height = 768;

        [UnityTest]
        public IEnumerator HealthyForest_BatchesVisibly_AndDamageReleasesOneTree()
        {
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D withBatch = null;
            Texture2D withoutBatch = null;

            try
            {
                ProceduralTreeRenderer renderer = null;
                for (int frame = 0; frame < 60; frame++)
                {
                    renderer = FindRuntimeRenderer();
                    if (renderer != null) break;
                    yield return null;
                }
                Assert.That(renderer, Is.Not.Null);

                var instances = new TreeInstance[8];
                for (int i = 0; i < instances.Length; i++)
                {
                    instances[i] = new TreeInstance
                    {
                        PositionMetres = new float3(3f + (i % 4) * 5f, 0f, 3f + (i / 4) * 5f),
                        Species = (TreeSpecies)(i % 7),
                        Seed = 0xA341316Cu + (uint)i * 2654435761u,
                        Scale = 0.92f + (i % 3) * 0.06f,
                    };
                }
                TreeWorldState.Replace(instances);

                for (int frame = 0;
                     frame < 90 && (renderer.PresentationCount != instances.Length
                                    || renderer.BatchCount != 1
                                    || renderer.BatchedTreeCount != instances.Length);
                     frame++)
                    yield return null;

                Assert.That(renderer.PresentationCount, Is.EqualTo(instances.Length));
                Assert.That(renderer.BatchCount, Is.EqualTo(1),
                            "Eight healthy trees in one 32 m cell should collapse to one render batch.");
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(instances.Length));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0),
                            "Healthy batched trees must not retain per-tree GameObjects.");
                Assert.That(renderer.DynamicMeshCount, Is.EqualTo(0),
                            "Healthy batched trees must not retain dormant per-tree meshes.");
                Assert.That(renderer.GeneratedMeshCount, Is.EqualTo(3),
                            "The only resident tree meshes should be the batch's three LOD meshes.");
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(4),
                            "One healthy batch should own one root plus three LOD objects.");
                Assert.That(renderer.EstimatedVisibleDrawCount, Is.EqualTo(2),
                            "One healthy batch should render as bark + leaves for the selected LOD.");

                for (int i = 0; i < instances.Length; i++)
                {
                    Assert.That(renderer.TryGetDynamicPresentationRoot(i, out _), Is.False,
                                $"Healthy batched tree {i} unexpectedly owns a dynamic presentation.");
                    Assert.That(FindTreeRoot(renderer, i), Is.Null,
                                $"Healthy batched tree {i} unexpectedly left a GameObject in the hierarchy.");
                }

                Transform batchRoot = FindBatchRoot(renderer);
                Assert.That(batchRoot, Is.Not.Null);
                MeshRenderer[] batchRenderers = batchRoot.GetComponentsInChildren<MeshRenderer>(true);
                MeshFilter[] batchFilters = batchRoot.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(batchRenderers.Length, Is.EqualTo(3));
                Assert.That(batchFilters.Length, Is.EqualTo(3));

                int batchVertices = 0;
                foreach (MeshFilter filter in batchFilters)
                {
                    Assert.That(filter.sharedMesh, Is.Not.Null);
                    Assert.That(filter.sharedMesh.subMeshCount, Is.EqualTo(2));
                    batchVertices += filter.sharedMesh.vertexCount;
                }
                Assert.That(batchVertices, Is.GreaterThan(0));

                Bounds bounds = CalculateBounds(batchRenderers);
                Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(0.01f));

                cameraObject = new GameObject("CI Tree Batch Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.52f, 0.60f, 0.70f, 1f);
                camera.fieldOfView = 36f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 250f;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                Vector3 focus = bounds.center;
                float radius = Mathf.Max(bounds.extents.magnitude, 3f);
                Vector3 direction = new Vector3(0.76f, 0.30f, -1f).normalized;
                cameraObject.transform.position = focus + direction * (radius * 2.65f);
                cameraObject.transform.LookAt(focus);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Tree Batch Capture",
                };
                target.Create();
                camera.targetTexture = target;
                yield return null;
                yield return null;

                withBatch = Capture(camera, target);

                bool[] enabled = new bool[batchRenderers.Length];
                try
                {
                    for (int i = 0; i < batchRenderers.Length; i++)
                    {
                        enabled[i] = batchRenderers[i].enabled;
                        batchRenderers[i].enabled = false;
                    }
                    withoutBatch = Capture(camera, target);
                }
                finally
                {
                    for (int i = 0; i < batchRenderers.Length; i++)
                        if (batchRenderers[i] != null) batchRenderers[i].enabled = enabled[i];
                }

                int changedPixels = CountChangedPixels(withBatch, withoutBatch, 8);
                Assert.That(changedPixels, Is.GreaterThan(1024),
                            "The combined batch exists structurally but does not contribute enough real pixels.");

                TreeWorldState.SetDamage(0, 0.70f, false);
                for (int frame = 0;
                     frame < 60 && (renderer.BatchedTreeCount != instances.Length - 1
                                    || renderer.DynamicPresentationCount != 1);
                     frame++)
                    yield return null;

                Assert.That(renderer.BatchCount, Is.EqualTo(1));
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(instances.Length - 1));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(1));
                Assert.That(renderer.DynamicMeshCount, Is.EqualTo(3));
                Assert.That(renderer.GeneratedMeshCount, Is.EqualTo(6),
                            "After first damage only the batch and one dynamic tree should own meshes.");
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(8),
                            "One batch plus one dynamic tree should own eight hierarchy objects total.");
                Assert.That(renderer.EstimatedVisibleDrawCount, Is.EqualTo(4),
                            "Seven healthy trees should remain one batch while the damaged tree uses two dynamic draws.");

                Assert.That(renderer.TryGetDynamicPresentationRoot(0, out Transform damagedRoot), Is.True,
                            "Damaged tree did not lazily materialize after leaving its batch.");
                Assert.That(damagedRoot, Is.Not.Null);
                Assert.That(Quaternion.Angle(damagedRoot.localRotation, Quaternion.identity), Is.LessThan(1f));
                MeshRenderer[] damagedRenderers = damagedRoot.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(damagedRenderers.Length, Is.EqualTo(3));
                Assert.That(damagedRenderers, Has.Some.Matches<MeshRenderer>(r => r.enabled));

                for (int i = 1; i < instances.Length; i++)
                    Assert.That(renderer.TryGetDynamicPresentationRoot(i, out _), Is.False,
                                $"Unchanged healthy tree {i} should remain data-only inside the batch.");
            }
            finally
            {
                TreeWorldState.Replace(System.Array.Empty<TreeInstance>());
                if (withBatch != null) Object.Destroy(withBatch);
                if (withoutBatch != null) Object.Destroy(withoutBatch);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                if (cameraObject != null) Object.Destroy(cameraObject);
            }
        }

        private static Texture2D Capture(Camera camera, RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                var capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                capture.Apply(false, false);
                return capture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static int CountChangedPixels(Texture2D a, Texture2D b, int threshold)
        {
            Color32[] first = a.GetPixels32();
            Color32[] second = b.GetPixels32();
            Assert.That(first.Length, Is.EqualTo(second.Length));
            int changed = 0;
            for (int i = 0; i < first.Length; i++)
            {
                int maxDelta = Mathf.Max(
                    Mathf.Abs(first[i].r - second[i].r),
                    Mathf.Max(Mathf.Abs(first[i].g - second[i].g),
                              Mathf.Abs(first[i].b - second[i].b)));
                if (maxDelta >= threshold) changed++;
            }
            return changed;
        }

        private static Bounds CalculateBounds(MeshRenderer[] renderers)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (MeshRenderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            Assert.That(hasBounds, Is.True);
            return bounds;
        }

        private static Transform FindBatchRoot(ProceduralTreeRenderer renderer)
        {
            for (int i = 0; i < renderer.transform.childCount; i++)
            {
                Transform child = renderer.transform.GetChild(i);
                if (child.name.StartsWith("Tree Batch ")) return child;
            }
            return null;
        }

        private static Transform FindTreeRoot(ProceduralTreeRenderer renderer, int index)
        {
            string prefix = $"Tree {index:000} ";
            for (int i = 0; i < renderer.transform.childCount; i++)
            {
                Transform child = renderer.transform.GetChild(i);
                if (child.name.StartsWith(prefix)) return child;
            }
            return null;
        }

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            foreach (ProceduralTreeRenderer renderer in all)
            {
                if (renderer == null || renderer.gameObject == null) continue;
                if (!renderer.gameObject.scene.IsValid()) continue;
                return renderer;
            }
            return null;
        }
    }
}
