using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// Visual regression coverage for the production standing-tree LOD path. The same semantic tree
    /// is rendered at representative distances inside every LOD band, immediately on both sides of
    /// each transition, and on both sides of the final vegetation horizon. Captures are written as
    /// CI artifacts so failures can be judged visually as well as by pixel/silhouette metrics.
    /// </summary>
    public sealed class TreeLodDistanceVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;
        private const float FieldOfView = 34f;
        private const int PixelDeltaThreshold = 8;

        private readonly struct Sample
        {
            public readonly string Name;
            public readonly float Distance;
            public readonly int ChangedPixels;
            public readonly RectInt Bounds;
            public readonly float FillRatio;

            public Sample(string name, float distance, int changedPixels,
                          RectInt bounds, float fillRatio)
            {
                Name = name;
                Distance = distance;
                ChangedPixels = changedPixels;
                Bounds = bounds;
                FillRatio = fillRatio;
            }

            public int Width => Bounds.width;
            public int Height => Bounds.height;
        }

        [UnityTest]
        public IEnumerator StandingTree_RendersCorrectlyAcrossEveryLodDistanceAndTransition()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "TreeLOD");
            Directory.CreateDirectory(outputDirectory);

            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D withTree = null;
            Texture2D withoutTree = null;

            try
            {
                ProceduralTreeRenderer renderer = null;
                for (int frame = 0; frame < 60; frame++)
                {
                    renderer = FindRuntimeRenderer();
                    if (renderer != null) break;
                    yield return null;
                }
                Assert.That(renderer, Is.Not.Null,
                            "Production ProceduralTreeRenderer was not available in PlayMode.");

                Assert.That(ProceduralTreeMaterials.Ensure(), Is.True);
                Assert.That(ProceduralTreeMaterials.Impostor, Is.Not.Null);
                Assert.That(ProceduralTreeMaterials.Impostor.shader.name,
                            Is.EqualTo("VoxelEngine/ProceduralTreeImpostor"),
                            "LOD3 accidentally regressed to the per-leaf shader; that produces a giant-leaf silhouette.");

                // Put the singleton in the centre of spatial cell (0,0). The production batch LOD
                // selector measures from that cell centre (16,8,16), so the camera uses y=8 and
                // moves along -Z to make the requested distances deterministic.
                var instance = new TreeInstance
                {
                    PositionMetres = new float3(16f, 0f, 16f),
                    Species = TreeSpecies.Oak,
                    Seed = 0x00C0FFEEu,
                    Scale = 1f,
                };

                TreeWorldState.Replace(new[] { instance });
                for (int frame = 0;
                     frame < 90 && (renderer.PresentationCount != 1
                                    || renderer.BatchCount != 1
                                    || renderer.BatchedTreeCount != 1);
                     frame++)
                    yield return null;

                Assert.That(renderer.PresentationCount, Is.EqualTo(1));
                Assert.That(renderer.BatchCount, Is.EqualTo(1));
                Assert.That(renderer.BatchMeshCount, Is.EqualTo(4));
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));
                Assert.That(renderer.transform.childCount, Is.EqualTo(0));
                Assert.That(renderer.TryGetTreeBounds(0, out Bounds treeBounds), Is.True);

                cameraObject = new GameObject("CI Tree LOD Distance Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.50f, 0.59f, 0.70f, 1f);
                camera.fieldOfView = FieldOfView;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 1700f;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Tree LOD Distance Capture",
                };
                target.Create();
                camera.targetTexture = target;

                var samples = new Dictionary<string, Sample>();
                string[] names =
                {
                    "lod0-20m",
                    "lod0-before-45m", "lod1-after-45m",
                    "lod1-80m",
                    "lod1-before-120m", "lod2-after-120m",
                    "lod2-200m",
                    "lod2-before-300m", "lod3-after-300m",
                    "lod3-500m",
                    "lod3-before-cull-1390m", "culled-1410m",
                };
                float[] distances =
                {
                    20f,
                    44f, 46f,
                    80f,
                    119f, 121f,
                    200f,
                    299f, 301f,
                    500f,
                    1390f, 1410f,
                };

                var metadata = new List<string>
                {
                    $"resolution={Width}x{Height}",
                    $"fov={FieldOfView:F1}",
                    "expectedLod0=0-45m",
                    "expectedLod1=45-120m",
                    "expectedLod2=120-300m",
                    "expectedLod3=300-1400m",
                    "expectedCull=>=1400m",
                    $"treeBounds={treeBounds}",
                    $"impostorShader={ProceduralTreeMaterials.Impostor.shader.name}",
                };

                for (int i = 0; i < distances.Length; i++)
                {
                    Sample sample = default;
                    yield return CaptureAtDistance(
                        renderer, instance, treeBounds, camera, target,
                        names[i], distances[i], outputDirectory,
                        value => sample = value,
                        texture =>
                        {
                            if (withTree != null) UnityEngine.Object.Destroy(withTree);
                            withTree = texture;
                        },
                        texture =>
                        {
                            if (withoutTree != null) UnityEngine.Object.Destroy(withoutTree);
                            withoutTree = texture;
                        });
                    samples.Add(sample.Name, sample);
                    metadata.Add(Format(sample));
                }

                // Representative samples inside each live band must be visibly present.
                AssertVisible(samples["lod0-20m"], 6000);
                AssertVisible(samples["lod1-80m"], 300);
                AssertVisible(samples["lod2-200m"], 60);
                AssertVisible(samples["lod3-500m"], 15);
                AssertVisible(samples["lod3-before-cull-1390m"], 2);

                // Silhouettes must remain tree-like rather than collapsing into a horizontal strip,
                // a full-screen quad, or the old giant per-leaf card artifact.
                AssertTreeLike(samples["lod0-20m"], minHeight: 180, maxFill: 0.82f);
                AssertTreeLike(samples["lod1-80m"], minHeight: 45, maxFill: 0.85f);
                AssertTreeLike(samples["lod2-200m"], minHeight: 15, maxFill: 0.88f);
                AssertTreeLike(samples["lod3-500m"], minHeight: 5, maxFill: 0.88f);

                // No transition may make the tree disappear or explode in screen coverage. Correct
                // for the small perspective-size change by allowing intentionally broad ratios.
                AssertTransitionContinuity(samples["lod0-before-45m"], samples["lod1-after-45m"],
                                           0.45f, 1.70f);
                AssertTransitionContinuity(samples["lod1-before-120m"], samples["lod2-after-120m"],
                                           0.35f, 1.90f);
                AssertTransitionContinuity(samples["lod2-before-300m"], samples["lod3-after-300m"],
                                           0.20f, 2.60f);

                // The horizon is deliberate: immediately inside it there must still be tree pixels,
                // while immediately beyond it direct submission should contribute nothing.
                Assert.That(samples["culled-1410m"].ChangedPixels, Is.LessThanOrEqualTo(1),
                            "Tree still renders beyond the 1400m vegetation horizon.");

                File.WriteAllLines(Path.Combine(outputDirectory, "tree-lod-distance.txt"), metadata);
                Debug.Log("Tree LOD distance visual proof:\n" + string.Join("\n", metadata));
            }
            finally
            {
                TreeWorldState.Replace(Array.Empty<TreeInstance>());
                if (withTree != null) UnityEngine.Object.Destroy(withTree);
                if (withoutTree != null) UnityEngine.Object.Destroy(withoutTree);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.Destroy(target);
                }
                if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
            }
        }

        private static IEnumerator CaptureAtDistance(
            ProceduralTreeRenderer renderer,
            TreeInstance instance,
            Bounds treeBounds,
            Camera camera,
            RenderTexture target,
            string sampleName,
            float distance,
            string outputDirectory,
            Action<Sample> result,
            Action<Texture2D> withTreeResult,
            Action<Texture2D> withoutTreeResult)
        {
            Vector3 batchCentre = new Vector3(16f, 8f, 16f);
            Vector3 cameraPosition = batchCentre + Vector3.back * distance;
            camera.transform.position = cameraPosition;
            camera.transform.LookAt(treeBounds.center);

            // Restore the tree and give the production Update path enough time to submit using this
            // Camera.main position.
            TreeWorldState.Replace(new[] { instance });
            for (int frame = 0;
                 frame < 30 && (renderer.PresentationCount != 1 || renderer.BatchCount != 1);
                 frame++)
                yield return null;
            yield return null;
            yield return null;

            Texture2D withTree = Capture(camera, target);
            File.WriteAllBytes(Path.Combine(outputDirectory, sampleName + ".png"), withTree.EncodeToPNG());

            // Same camera, same clear colour, same frame setup, but no semantic tree. Difference
            // pixels therefore measure the actual standing-tree contribution.
            TreeWorldState.Replace(Array.Empty<TreeInstance>());
            for (int frame = 0; frame < 3; frame++) yield return null;
            Texture2D withoutTree = Capture(camera, target);

            MeasureDifference(withTree, withoutTree, PixelDeltaThreshold,
                              out int changedPixels, out RectInt bounds, out float fillRatio);
            result(new Sample(sampleName, distance, changedPixels, bounds, fillRatio));
            withTreeResult(withTree);
            withoutTreeResult(withoutTree);
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
                                              int channelThreshold,
                                              out int changedPixels,
                                              out RectInt bounds,
                                              out float fillRatio)
        {
            Color32[] a = withTree.GetPixels32();
            Color32[] b = withoutTree.GetPixels32();
            Assert.That(a.Length, Is.EqualTo(b.Length));

            changedPixels = 0;
            int minX = Width;
            int minY = Height;
            int maxX = -1;
            int maxY = -1;

            for (int i = 0; i < a.Length; i++)
            {
                int delta = Mathf.Max(
                    Mathf.Abs(a[i].r - b[i].r),
                    Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                if (delta < channelThreshold) continue;

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

        private static void AssertVisible(Sample sample, int minimumPixels)
        {
            Assert.That(sample.ChangedPixels, Is.GreaterThanOrEqualTo(minimumPixels),
                        $"{sample.Name} at {sample.Distance:F0}m is effectively invisible: " +
                        $"pixels={sample.ChangedPixels}, bounds={sample.Bounds}.");
        }

        private static void AssertTreeLike(Sample sample, int minHeight, float maxFill)
        {
            Assert.That(sample.Height, Is.GreaterThanOrEqualTo(minHeight),
                        $"{sample.Name} collapsed vertically: bounds={sample.Bounds}.");
            Assert.That(sample.Height, Is.GreaterThan(sample.Width * 0.55f),
                        $"{sample.Name} is implausibly wide/flat for an upright tree: bounds={sample.Bounds}.");
            Assert.That(sample.FillRatio, Is.LessThan(maxFill),
                        $"{sample.Name} is too solid ({sample.FillRatio:P1}); likely card/quad silhouette regression.");
        }

        private static void AssertTransitionContinuity(Sample before, Sample after,
                                                       float minimumRatio, float maximumRatio)
        {
            Assert.That(before.ChangedPixels, Is.GreaterThan(0), before.Name + " disappeared.");
            Assert.That(after.ChangedPixels, Is.GreaterThan(0), after.Name + " disappeared.");
            float ratio = after.ChangedPixels / (float)before.ChangedPixels;
            Assert.That(ratio, Is.InRange(minimumRatio, maximumRatio),
                        $"LOD transition {before.Distance:F0}m->{after.Distance:F0}m has a coverage pop: " +
                        $"{before.ChangedPixels}->{after.ChangedPixels} pixels ({ratio:F2}x).");
        }

        private static string Format(Sample sample)
        {
            return string.Join(",",
                sample.Name,
                sample.Distance.ToString("F1", CultureInfo.InvariantCulture),
                "pixels=" + sample.ChangedPixels,
                "bounds=" + sample.Bounds,
                "fill=" + sample.FillRatio.ToString("F4", CultureInfo.InvariantCulture));
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
