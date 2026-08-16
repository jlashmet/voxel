using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Rendering.Runtime.AmbientLife;

namespace VoxelEngine.CI
{
    [NUnit.Framework.Explicit("Fixed-position shader articulation validation; run by animation CI.")]
    public sealed class AmbientLifeArticulationVisualTests
    {
        [UnityTest]
        public IEnumerator AllVisualShapeFamilies_HaveExpectedArticulationWithoutLocomotion()
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera(
                "CI Ambient Articulation Camera",
                new Vector3(0f, 0f, -4.2f),
                Vector3.zero,
                30f,
                out Camera camera,
                out RenderTexture target);
            Texture2D background = null;
            Mesh quad = null;
            GameObject subjectObject = null;
            MeshRenderer subjectRenderer = null;
            var captures = new List<Texture2D>();
            var report = new StringBuilder(
                "kind,shape,time_a,time_b,width_a,width_b,height_a,height_b,area_a,area_b,mean_luma_a,mean_luma_b,width_change,height_change,area_change,luma_change\n");

            try
            {
                camera.Render();
                background = VegetationLifeRenderingVisualTests.ReadTarget(target);
                quad = BuildQuad();
                Assert.That(ProceduralAmbientLifeMaterials.Shared, Is.Not.Null);
                ProceduralAmbientLifeMaterials.ApplyLighting();

                // A persistent CI-only renderer avoids one-frame Graphics.DrawMeshInstanced submission
                // carry-over between captures. Production ambient life remains GPU-instanced; this object
                // exists only to isolate shader articulation from locomotion and previous draw calls.
                subjectObject = new GameObject("CI Ambient Fixed Articulation Subject");
                MeshFilter subjectFilter = subjectObject.AddComponent<MeshFilter>();
                subjectFilter.sharedMesh = quad;
                subjectRenderer = subjectObject.AddComponent<MeshRenderer>();
                subjectRenderer.sharedMaterial = ProceduralAmbientLifeMaterials.Shared;

                ArticulationCase[] cases =
                {
                    WingCase(AmbientLifeKind.Butterfly, 8f, 0.25f),
                    WingCase(AmbientLifeKind.Bee, 10f, 0.11f),
                    WingCase(AmbientLifeKind.Dragonfly, 12f, 0.08f),
                    new ArticulationCase(AmbientLifeKind.Beetle, 0f, 0.90f, ArticulationExpectation.Stable, 0.07f),
                    new ArticulationCase(AmbientLifeKind.Frog, 0f, 1.25f, ArticulationExpectation.AnyAxis, 0.025f),
                    WingCase(AmbientLifeKind.Songbird, 6f, 0.20f),
                    new ArticulationCase(AmbientLifeKind.SporeMote, 0f, 3.38f, ArticulationExpectation.BothAxes, 0.035f),
                    new ArticulationCase(AmbientLifeKind.Wisp, 0f, 1.68f, ArticulationExpectation.AnyAxis, 0.04f),
                    WingCase(AmbientLifeKind.Emberfly, 11f, 0.12f),
                    LuminanceCase(AmbientLifeKind.Firefly, 3f, 0.025f),
                };

                string reportPath = VegetationLifeRenderingVisualTests.ArtifactPath("ambient_articulation_quality.csv");
                for (int i = 0; i < cases.Length; i++)
                {
                    ArticulationCase c = cases[i];
                    Texture2D first;
                    Texture2D second;
                    FrameMetrics a;
                    FrameMetrics b;
                    float timeA;
                    float timeB;

                    if (c.Expectation == ArticulationExpectation.Width)
                    {
                        CaptureWingEnvelope(
                            camera,
                            target,
                            background,
                            subjectObject.transform,
                            subjectRenderer,
                            c,
                            out first,
                            out second,
                            out a,
                            out b,
                            out timeA,
                            out timeB);
                    }
                    else if (c.Expectation == ArticulationExpectation.Luminance)
                    {
                        CaptureLuminanceEnvelope(
                            camera,
                            target,
                            background,
                            subjectObject.transform,
                            subjectRenderer,
                            c,
                            out first,
                            out second,
                            out a,
                            out b,
                            out timeA,
                            out timeB);
                    }
                    else
                    {
                        timeA = c.TimeA;
                        timeB = c.TimeB;
                        first = CaptureFixed(camera, target, subjectObject.transform, subjectRenderer, c.Kind, timeA);
                        second = CaptureFixed(camera, target, subjectObject.transform, subjectRenderer, c.Kind, timeB);
                        a = Measure(first, background);
                        b = Measure(second, background);
                    }

                    captures.Add(first);
                    captures.Add(second);

                    File.WriteAllBytes(
                        VegetationLifeRenderingVisualTests.ArtifactPath($"ambient_articulation_{c.Kind}_a.png"),
                        first.EncodeToPNG());
                    File.WriteAllBytes(
                        VegetationLifeRenderingVisualTests.ArtifactPath($"ambient_articulation_{c.Kind}_b.png"),
                        second.EncodeToPNG());

                    float widthChange = RelativeChange(a.Width, b.Width);
                    float heightChange = RelativeChange(a.Height, b.Height);
                    float areaChange = RelativeChange(a.ForegroundPixels, b.ForegroundPixels);
                    float lumaChange = Mathf.Abs(a.MeanLuminance - b.MeanLuminance);
                    AmbientVisualShape shape = ProceduralAmbientLifeMaterials.StyleFor(c.Kind).Shape;

                    report.AppendLine(
                        $"{c.Kind},{shape},{timeA:0.000},{timeB:0.000},{a.Width},{b.Width},{a.Height},{b.Height}," +
                        $"{a.ForegroundPixels},{b.ForegroundPixels},{a.MeanLuminance:0.0000},{b.MeanLuminance:0.0000}," +
                        $"{widthChange:0.0000},{heightChange:0.0000},{areaChange:0.0000},{lumaChange:0.0000}");
                    // Persist progress before assertions so a later species failure still leaves useful metrics.
                    File.WriteAllText(reportPath, report.ToString());

                    Assert.That(a.ForegroundPixels, Is.GreaterThan(300), $"{c.Kind} did not render a readable fixed-position silhouette.");
                    Assert.That(b.ForegroundPixels, Is.GreaterThan(300), $"{c.Kind} disappeared at its second articulation timestamp.");
                    AssertExpectation(c, widthChange, heightChange, areaChange, lumaChange);
                }

                Texture2D sheet = BuildContactSheet(captures, 4, 4);
                try
                {
                    File.WriteAllBytes(
                        VegetationLifeRenderingVisualTests.ArtifactPath("ambient_articulation_contact_sheet.png"),
                        sheet.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sheet);
                }
            }
            finally
            {
                for (int i = 0; i < captures.Count; i++)
                    if (captures[i] != null) UnityEngine.Object.DestroyImmediate(captures[i]);
                if (background != null) UnityEngine.Object.DestroyImmediate(background);
                if (subjectObject != null) UnityEngine.Object.DestroyImmediate(subjectObject);
                if (quad != null) UnityEngine.Object.DestroyImmediate(quad);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            yield return null;
        }

        private static ArticulationCase WingCase(AmbientLifeKind kind, float flutterSpeed, float minimumWidthChange)
        {
            // AmbientMask uses abs(sin(phase)), whose articulation period is PI. Sample one complete
            // envelope rather than assuming a particular world-space phase origin.
            float flapPeriod = Mathf.PI / flutterSpeed;
            return new ArticulationCase(kind, 0f, flapPeriod, ArticulationExpectation.Width, minimumWidthChange);
        }

        private static ArticulationCase LuminanceCase(AmbientLifeKind kind, float flutterSpeed, float minimumLuminanceChange)
        {
            // Emissive pulse uses a full sine cycle at max(0.5, flutterSpeed * 0.42). Sampling the
            // entire cycle makes the quality gate independent of the world-space phase offset.
            float pulseSpeed = Mathf.Max(0.5f, flutterSpeed * 0.42f);
            float pulsePeriod = Mathf.PI * 2f / pulseSpeed;
            return new ArticulationCase(kind, 0f, pulsePeriod, ArticulationExpectation.Luminance, minimumLuminanceChange);
        }

        private static void CaptureWingEnvelope(
            Camera camera,
            RenderTexture target,
            Texture2D background,
            Transform subject,
            MeshRenderer renderer,
            ArticulationCase c,
            out Texture2D narrowest,
            out Texture2D widest,
            out FrameMetrics narrowestMetrics,
            out FrameMetrics widestMetrics,
            out float narrowestTime,
            out float widestTime)
        {
            const int sampleCount = 5;
            Texture2D[] samples = new Texture2D[sampleCount];
            FrameMetrics[] metrics = new FrameMetrics[sampleCount];
            int minIndex = 0;
            int maxIndex = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                float alpha = i / (float)(sampleCount - 1);
                float time = Mathf.Lerp(c.TimeA, c.TimeB, alpha);
                samples[i] = CaptureFixed(camera, target, subject, renderer, c.Kind, time);
                metrics[i] = Measure(samples[i], background);
                if (metrics[i].Width < metrics[minIndex].Width) minIndex = i;
                if (metrics[i].Width > metrics[maxIndex].Width) maxIndex = i;
            }

            narrowest = samples[minIndex];
            widest = samples[maxIndex];
            narrowestMetrics = metrics[minIndex];
            widestMetrics = metrics[maxIndex];
            narrowestTime = Mathf.Lerp(c.TimeA, c.TimeB, minIndex / (float)(sampleCount - 1));
            widestTime = Mathf.Lerp(c.TimeA, c.TimeB, maxIndex / (float)(sampleCount - 1));

            for (int i = 0; i < sampleCount; i++)
            {
                if (i == minIndex || i == maxIndex) continue;
                UnityEngine.Object.DestroyImmediate(samples[i]);
            }
        }

        private static void CaptureLuminanceEnvelope(
            Camera camera,
            RenderTexture target,
            Texture2D background,
            Transform subject,
            MeshRenderer renderer,
            ArticulationCase c,
            out Texture2D darkest,
            out Texture2D brightest,
            out FrameMetrics darkestMetrics,
            out FrameMetrics brightestMetrics,
            out float darkestTime,
            out float brightestTime)
        {
            const int sampleCount = 9;
            Texture2D[] samples = new Texture2D[sampleCount];
            FrameMetrics[] metrics = new FrameMetrics[sampleCount];
            int minIndex = 0;
            int maxIndex = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                float alpha = i / (float)(sampleCount - 1);
                float time = Mathf.Lerp(c.TimeA, c.TimeB, alpha);
                samples[i] = CaptureFixed(camera, target, subject, renderer, c.Kind, time);
                metrics[i] = Measure(samples[i], background);
                if (metrics[i].MeanLuminance < metrics[minIndex].MeanLuminance) minIndex = i;
                if (metrics[i].MeanLuminance > metrics[maxIndex].MeanLuminance) maxIndex = i;
            }

            darkest = samples[minIndex];
            brightest = samples[maxIndex];
            darkestMetrics = metrics[minIndex];
            brightestMetrics = metrics[maxIndex];
            darkestTime = Mathf.Lerp(c.TimeA, c.TimeB, minIndex / (float)(sampleCount - 1));
            brightestTime = Mathf.Lerp(c.TimeA, c.TimeB, maxIndex / (float)(sampleCount - 1));

            for (int i = 0; i < sampleCount; i++)
            {
                if (i == minIndex || i == maxIndex) continue;
                UnityEngine.Object.DestroyImmediate(samples[i]);
            }
        }

        private static void AssertExpectation(
            ArticulationCase c,
            float widthChange,
            float heightChange,
            float areaChange,
            float luminanceChange)
        {
            switch (c.Expectation)
            {
                case ArticulationExpectation.Width:
                    Assert.That(widthChange, Is.GreaterThan(c.Threshold),
                        $"{c.Kind} is moving through space but its wing span is not visibly articulating.");
                    break;
                case ArticulationExpectation.AnyAxis:
                    Assert.That(Mathf.Max(widthChange, heightChange), Is.GreaterThan(c.Threshold),
                        $"{c.Kind} has no readable body articulation at a fixed world position.");
                    break;
                case ArticulationExpectation.BothAxes:
                    Assert.That(widthChange, Is.GreaterThan(c.Threshold));
                    Assert.That(heightChange, Is.GreaterThan(c.Threshold));
                    break;
                case ArticulationExpectation.Luminance:
                    Assert.That(luminanceChange, Is.GreaterThan(c.Threshold),
                        $"{c.Kind} has no readable emissive pulse at a fixed world position.");
                    break;
                case ArticulationExpectation.Stable:
                    Assert.That(widthChange, Is.LessThan(c.Threshold),
                        $"{c.Kind} deforms too much for a ground-scuttling body.");
                    Assert.That(heightChange, Is.LessThan(c.Threshold));
                    Assert.That(areaChange, Is.LessThan(c.Threshold * 1.5f));
                    break;
            }
        }

        private static Texture2D CaptureFixed(
            Camera camera,
            RenderTexture target,
            Transform subject,
            MeshRenderer renderer,
            AmbientLifeKind kind,
            float time)
        {
            var properties = new MaterialPropertyBlock();
            ProceduralAmbientLifeMaterials.Configure(properties, kind);
            properties.SetFloat("_AnimationTime", time);
            properties.SetFloat("_Opacity", 1f);
            renderer.SetPropertyBlock(properties);

            AmbientLifeRenderStyle style = ProceduralAmbientLifeMaterials.StyleFor(kind);
            GetValidationScale(style.Shape, out float width, out float height);
            subject.position = Vector3.zero;
            subject.rotation = Quaternion.identity;
            subject.localScale = new Vector3(width, height, 1f);

            camera.Render();
            return VegetationLifeRenderingVisualTests.ReadTarget(target);
        }

        private static void GetValidationScale(AmbientVisualShape shape, out float width, out float height)
        {
            const float baseScale = 1.35f;
            switch (shape)
            {
                case AmbientVisualShape.Butterfly: width = baseScale * 1.42f; height = baseScale * 0.90f; return;
                case AmbientVisualShape.CompactInsect: width = baseScale * 1.28f; height = baseScale * 0.76f; return;
                case AmbientVisualShape.Dragonfly: width = baseScale * 1.55f; height = baseScale * 1.02f; return;
                case AmbientVisualShape.GroundInsect: width = baseScale * 0.82f; height = baseScale * 1.08f; return;
                case AmbientVisualShape.Frog: width = baseScale * 1.30f; height = baseScale * 0.82f; return;
                case AmbientVisualShape.BirdOrBat: width = baseScale * 1.55f; height = baseScale * 0.78f; return;
                case AmbientVisualShape.Spore: width = baseScale * 0.72f; height = baseScale * 0.72f; return;
                case AmbientVisualShape.Wisp: width = baseScale * 0.72f; height = baseScale * 1.30f; return;
                case AmbientVisualShape.Emberfly: width = baseScale * 1.18f; height = baseScale * 0.88f; return;
                default: width = baseScale * 0.72f; height = baseScale * 0.72f; return;
            }
        }

        private static FrameMetrics Measure(Texture2D capture, Texture2D background)
        {
            bool[] mask = VegetationLifeRenderingVisualTests.ForegroundMask(capture, background, out int count);
            Color32[] pixels = capture.GetPixels32();
            int width = capture.width;
            int minX = width, minY = capture.height, maxX = -1, maxY = -1;
            float luminance = 0f;

            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i]) continue;
                int x = i % width;
                int y = i / width;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                Color p = pixels[i];
                luminance += p.r * 0.2126f + p.g * 0.7152f + p.b * 0.0722f;
            }

            return new FrameMetrics(
                count,
                maxX >= minX ? maxX - minX + 1 : 0,
                maxY >= minY ? maxY - minY + 1 : 0,
                count > 0 ? luminance / count : 0f);
        }

        private static float RelativeChange(int a, int b)
        {
            return Mathf.Abs(a - b) / (float)Mathf.Max(1, Mathf.Max(a, b));
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh
            {
                name = "CI Ambient Fixed Articulation Quad",
                hideFlags = HideFlags.DontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                },
                uv = new[]
                {
                    new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                },
                triangles = new[] { 0,1,2, 0,2,3 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D BuildContactSheet(IReadOnlyList<Texture2D> frames, int columns, int downsample)
        {
            int sourceWidth = frames[0].width;
            int sourceHeight = frames[0].height;
            int cellWidth = sourceWidth / downsample;
            int cellHeight = sourceHeight / downsample;
            int rows = (frames.Count + columns - 1) / columns;
            int sheetWidth = cellWidth * columns;
            int sheetHeight = cellHeight * rows;
            var destination = new Color32[sheetWidth * sheetHeight];

            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                Color32[] source = frames[frameIndex].GetPixels32();
                int originX = (frameIndex % columns) * cellWidth;
                int originY = (rows - 1 - frameIndex / columns) * cellHeight;
                for (int y = 0; y < cellHeight; y++)
                {
                    int sourceY = Mathf.Min(sourceHeight - 1, y * downsample);
                    for (int x = 0; x < cellWidth; x++)
                    {
                        int sourceX = Mathf.Min(sourceWidth - 1, x * downsample);
                        destination[(originY + y) * sheetWidth + originX + x] =
                            source[sourceY * sourceWidth + sourceX];
                    }
                }
            }

            var sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGB24, false, false);
            sheet.SetPixels32(destination);
            sheet.Apply(false, false);
            return sheet;
        }

        private readonly struct FrameMetrics
        {
            public readonly int ForegroundPixels;
            public readonly int Width;
            public readonly int Height;
            public readonly float MeanLuminance;

            public FrameMetrics(int foregroundPixels, int width, int height, float meanLuminance)
            {
                ForegroundPixels = foregroundPixels;
                Width = width;
                Height = height;
                MeanLuminance = meanLuminance;
            }
        }

        private readonly struct ArticulationCase
        {
            public readonly AmbientLifeKind Kind;
            public readonly float TimeA;
            public readonly float TimeB;
            public readonly ArticulationExpectation Expectation;
            public readonly float Threshold;

            public ArticulationCase(
                AmbientLifeKind kind,
                float timeA,
                float timeB,
                ArticulationExpectation expectation,
                float threshold)
            {
                Kind = kind;
                TimeA = timeA;
                TimeB = timeB;
                Expectation = expectation;
                Threshold = threshold;
            }
        }

        private enum ArticulationExpectation
        {
            Width,
            AnyAxis,
            BothAxes,
            Luminance,
            Stable,
        }
    }
}
