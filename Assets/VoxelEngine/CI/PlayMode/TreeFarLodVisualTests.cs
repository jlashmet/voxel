using System;
using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Vegetation.Runtime;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.CI
{
    public sealed class TreeFarLodVisualTests
    {
        private const int Width = 768;
        private const int Height = 768;

        [UnityTest]
        public IEnumerator UltraFarImpostor_IsFourthLodAndMatchesTreeSilhouette()
        {
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D baseline = null;
            Texture2D lod2Capture = null;
            Texture2D lod3Capture = null;
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
                Assert.That(ProceduralTreeMaterials.Ensure(), Is.True);
                Assert.That(ProceduralTreeMaterials.Impostor, Is.Not.Null);
                Assert.That(ProceduralTreeMaterials.Impostor.shader.name,
                            Is.EqualTo("VoxelEngine/ProceduralTreeImpostor"));

                var instance = new TreeInstance
                {
                    PositionMetres = new float3(16f, 0f, 16f),
                    Species = TreeSpecies.Oak,
                    Seed = 0x00C0FFEEu,
                    Scale = 1f,
                };
                TreeWorldRuntime.Replace(new[] { instance });
                for (int frame = 0;
                     frame < 90 && (renderer.PresentationCount != 1 || renderer.BatchCount != 1);
                     frame++)
                    yield return null;

                Assert.That(renderer.BatchMeshCount, Is.EqualTo(4));
                Assert.That(renderer.GeneratedMeshCount, Is.EqualTo(4));
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(5));
                Assert.That(renderer.TryGetTreeBounds(0, out Bounds treeBounds), Is.True);

                Transform batchRoot = renderer.transform.GetChild(0);
                LODGroup group = batchRoot.GetComponent<LODGroup>();
                Assert.That(group, Is.Not.Null);
                LOD[] lods = group.GetLODs();
                Assert.That(lods.Length, Is.EqualTo(4));
                Assert.That(lods[0].screenRelativeTransitionHeight, Is.EqualTo(0.34f).Within(0.001f));
                Assert.That(lods[1].screenRelativeTransitionHeight, Is.EqualTo(0.13f).Within(0.001f));
                Assert.That(lods[2].screenRelativeTransitionHeight, Is.EqualTo(0.025f).Within(0.001f));
                Assert.That(lods[3].screenRelativeTransitionHeight, Is.EqualTo(0.005f).Within(0.001f));

                MeshFilter[] filters = batchRoot.GetComponentsInChildren<MeshFilter>(true);
                MeshRenderer[] renderers = batchRoot.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(filters.Length, Is.EqualTo(4));
                Assert.That(renderers.Length, Is.EqualTo(4));
                Mesh impostor = filters[3].sharedMesh;
                Assert.That(impostor, Is.Not.Null);
                Assert.That(impostor.subMeshCount, Is.EqualTo(1));
                Assert.That((int)impostor.GetIndexCount(0) / 3, Is.EqualTo(8));
                Assert.That(renderers[3].sharedMaterial, Is.SameAs(ProceduralTreeMaterials.Impostor));

                cameraObject = new GameObject("CI Far Tree LOD Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.50f, 0.59f, 0.70f, 1f);
                camera.fieldOfView = 34f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 2000f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.transform.position = treeBounds.center + Vector3.back * 120f;
                camera.transform.LookAt(treeBounds.center);

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;

                batchRoot.gameObject.SetActive(false);
                baseline = Capture(camera, target);
                batchRoot.gameObject.SetActive(true);

                group.ForceLOD(2);
                yield return null;
                lod2Capture = Capture(camera, target);
                MeasureDifference(lod2Capture, baseline, out int lod2Pixels,
                                  out RectInt lod2Bounds, out float lod2Fill);

                group.ForceLOD(3);
                yield return null;
                lod3Capture = Capture(camera, target);
                MeasureDifference(lod3Capture, baseline, out int lod3Pixels,
                                  out RectInt lod3Bounds, out float lod3Fill);
                group.ForceLOD(-1);

                Assert.That(lod2Pixels, Is.GreaterThan(100));
                Assert.That(lod3Pixels, Is.GreaterThan(100));
                Assert.That(lod3Bounds.height, Is.GreaterThan(20));
                Assert.That(lod3Bounds.height, Is.GreaterThan(lod3Bounds.width * 0.55f));
                Assert.That(lod3Fill, Is.LessThan(0.90f));
                float coverageRatio = lod3Pixels / (float)lod2Pixels;
                Assert.That(coverageRatio, Is.InRange(0.15f, 4.0f),
                            $"LOD2->LOD3 silhouette coverage popped: {lod2Pixels}->{lod3Pixels}, " +
                            $"bounds {lod2Bounds}->{lod3Bounds}, fill {lod2Fill:F3}->{lod3Fill:F3}");
            }
            finally
            {
                TreeWorldRuntime.Replace(Array.Empty<TreeInstance>());
                if (baseline != null) UnityEngine.Object.Destroy(baseline);
                if (lod2Capture != null) UnityEngine.Object.Destroy(lod2Capture);
                if (lod3Capture != null) UnityEngine.Object.Destroy(lod3Capture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.Destroy(target);
                }
                if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
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

        private static void MeasureDifference(Texture2D withTree, Texture2D withoutTree,
                                              out int changedPixels, out RectInt bounds,
                                              out float fillRatio)
        {
            Color32[] a = withTree.GetPixels32();
            Color32[] b = withoutTree.GetPixels32();
            changedPixels = 0;
            int minX = Width, minY = Height, maxX = -1, maxY = -1;
            for (int i = 0; i < a.Length; i++)
            {
                int delta = Mathf.Max(Mathf.Abs(a[i].r - b[i].r),
                    Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                if (delta < 8) continue;
                changedPixels++;
                int x = i % Width;
                int y = i / Width;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            if (changedPixels == 0)
            {
                bounds = new RectInt(0, 0, 0, 0);
                fillRatio = 0f;
                return;
            }
            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            fillRatio = changedPixels / (float)Mathf.Max(1, bounds.width * bounds.height);
        }

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            foreach (ProceduralTreeRenderer candidate in
                     Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>())
            {
                if (candidate == null || candidate.gameObject == null) continue;
                if (!candidate.gameObject.scene.IsValid()) continue;
                return candidate;
            }
            return null;
        }
    }
}
