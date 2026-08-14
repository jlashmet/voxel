using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Deterministic 2D water-layer lookdev that matches the screen-space placement, palette,
    /// stepped falls, pools, foam, and foreground river of the Sunlit Cleric reference.
    /// </summary>
    public static class WaterStudyRasterCapture
    {
        private const int Width = 1024;
        private const int Height = 1536;
        private static Color32[] pixels;

        private static readonly Color32 Deep = new Color32(35, 116, 172, 255);
        private static readonly Color32 Water = new Color32(45, 166, 219, 255);
        private static readonly Color32 Shallow = new Color32(88, 199, 235, 255);
        private static readonly Color32 Fall = new Color32(105, 205, 240, 255);
        private static readonly Color32 Glint = new Color32(153, 230, 249, 255);
        private static readonly Color32 Foam = new Color32(232, 249, 255, 255);

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "Water");
            Directory.CreateDirectory(outputDirectory);

            Texture2D texture = null;
            try
            {
                pixels = new Color32[Width * Height];
                DrawWater();

                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(Path.Combine(outputDirectory, "water-study.png"), texture.EncodeToPNG());
                File.WriteAllText(Path.Combine(outputDirectory, "water-study.txt"),
                    "target=Sunlit Cleric waterfall water\n" +
                    "composition=reference-aligned screen-space water layer\n" +
                    "palette=deep turquoise, clear cyan, pale cyan, white foam\n" +
                    "background=transparent\n" +
                    $"size={Width}x{Height}\n");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                pixels = null;
            }

            EditorApplication.Exit(0);
        }

        private static void DrawWater()
        {
            // Distant right-side cascade.
            DrawPool(835, 476, 105, 24, 0.4f);
            DrawFall(838, 492, 830, 590, 72, 90, 11);
            DrawPool(826, 605, 122, 30, 1.2f);

            DrawFall(820, 625, 808, 716, 102, 118, 23);
            DrawPool(803, 731, 152, 38, 2.1f);
            DrawFall(795, 748, 805, 824, 122, 138, 37);
            DrawPool(805, 842, 125, 30, 2.8f);

            // Small center-left terrace visible between grassy ledges.
            DrawPool(500, 750, 76, 21, 1.5f);
            DrawFall(505, 774, 516, 870, 98, 115, 47);

            // Main middle pool and the wide waterfall that feeds it.
            DrawPool(610, 916, 288, 78, 3.4f);
            DrawArc(610, 905, 215, 43, 188f, 350f, 4f, Foam);
            DrawArc(625, 925, 175, 31, 198f, 338f, 3f, Glint);
            DrawFoamSpray(520, 886, 95, 26, 19, 61);

            // Right-side connecting steps.
            DrawPool(850, 900, 90, 24, 0.9f);
            DrawFall(855, 918, 880, 982, 78, 96, 73);
            DrawPool(884, 995, 102, 28, 1.7f);
            DrawFall(900, 1012, 918, 1078, 86, 105, 89);

            // Broad visible stream between the middle basin and foreground river.
            DrawRiver(
                new[]
                {
                    new Vector2(600, 930), new Vector2(690, 955), new Vector2(770, 994),
                    new Vector2(830, 1040), new Vector2(875, 1095), new Vector2(900, 1145)
                },
                new[] { 250f, 240f, 220f, 190f, 165f, 150f });

            // Foreground river hugs the right edge just like the reference.
            DrawRiver(
                new[]
                {
                    new Vector2(900, 1090), new Vector2(930, 1185), new Vector2(955, 1300),
                    new Vector2(945, 1410), new Vector2(910, 1510), new Vector2(880, 1575)
                },
                new[] { 190f, 280f, 380f, 470f, 560f, 640f });

            DrawFoamSpray(830, 594, 78, 18, 13, 101);
            DrawFoamSpray(808, 720, 98, 20, 16, 121);
            DrawFoamSpray(805, 828, 105, 21, 16, 141);
            DrawFoamSpray(515, 875, 105, 24, 20, 161);
            DrawFoamSpray(880, 987, 62, 16, 10, 181);
            DrawFoamSpray(918, 1082, 68, 17, 11, 201);

            // Painterly surface dashes in the large basin and river.
            DrawHighlightDashes(450, 845, 820, 1015, 32, 223);
            DrawHighlightDashes(735, 1080, 1020, 1530, 40, 271);
        }

        private static void DrawPool(float cx, float cy, float rx, float ry, float phase)
        {
            FillPolygon(OrganicEllipse(cx, cy + 3, rx * 1.04f, ry * 1.15f, phase, 64), Deep);
            FillPolygon(OrganicEllipse(cx, cy, rx, ry, phase + 0.6f, 64), Water);
            FillPolygon(OrganicEllipse(cx - rx * 0.025f, cy - ry * 0.06f, rx * 0.78f, ry * 0.68f, phase + 1.1f, 64), Shallow);
            DrawArc(cx, cy - ry * 0.05f, rx * 0.68f, ry * 0.48f, 195f, 344f, 3f, Glint);
            DrawArc(cx + rx * 0.05f, cy + ry * 0.02f, rx * 0.40f, ry * 0.29f, 15f, 142f, 2f, Foam);
        }

        private static void DrawFall(float topX, float topY, float bottomX, float bottomY,
                                     float topWidth, float bottomWidth, int seed)
        {
            var outer = new[]
            {
                new Vector2(topX - topWidth * 0.54f, topY), new Vector2(topX + topWidth * 0.54f, topY),
                new Vector2(bottomX + bottomWidth * 0.54f, bottomY), new Vector2(bottomX - bottomWidth * 0.54f, bottomY)
            };
            FillPolygon(outer, Deep);

            var body = new[]
            {
                new Vector2(topX - topWidth * 0.48f, topY), new Vector2(topX + topWidth * 0.48f, topY),
                new Vector2(bottomX + bottomWidth * 0.48f, bottomY), new Vector2(bottomX - bottomWidth * 0.48f, bottomY)
            };
            FillPolygon(body, Fall);

            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                float sx = Mathf.Lerp(topX - topWidth * 0.38f, topX + topWidth * 0.38f, t);
                float ex = Mathf.Lerp(bottomX - bottomWidth * 0.34f, bottomX + bottomWidth * 0.34f, t);
                ex += (Hash01(seed + i * 17) - 0.5f) * 8f;
                DrawThickLine(sx, topY + 2, ex, bottomY - 2, i % 3 == 0 ? 5f : 3f, i % 2 == 0 ? Foam : Glint);
            }

            DrawThickLine(topX - topWidth * 0.48f, topY, topX + topWidth * 0.48f, topY, 5f, Foam);
            DrawArc(bottomX, bottomY + 2, bottomWidth * 0.66f, 16f, 190f, 350f, 5f, Foam);
        }

        private static void DrawRiver(Vector2[] path, float[] widths)
        {
            FillPolygon(Ribbon(path, Scale(widths, 1.08f)), Deep);
            FillPolygon(Ribbon(path, widths), Water);
            FillPolygon(Ribbon(Offset(path, -9f, -2f), Scale(widths, 0.72f)), Shallow);

            for (int band = 0; band < 4; band++)
            {
                float side = Mathf.Lerp(-0.28f, 0.28f, band / 3f);
                Vector2[] stripe = OffsetRibbonPath(path, widths, side);
                for (int i = 0; i < stripe.Length - 1; i++)
                    DrawThickLine(stripe[i].x, stripe[i].y, stripe[i + 1].x, stripe[i + 1].y, band % 2 == 0 ? 3f : 2f, band % 2 == 0 ? Foam : Glint);
            }
        }

        private static Vector2[] OrganicEllipse(float cx, float cy, float rx, float ry, float phase, int segments)
        {
            var points = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float wobble = 1f + Mathf.Sin(a * 3f + phase) * 0.055f + Mathf.Sin(a * 7f - phase * 1.4f) * 0.025f;
                points[i] = new Vector2(cx + Mathf.Cos(a) * rx * wobble, cy + Mathf.Sin(a) * ry * wobble);
            }
            return points;
        }

        private static Vector2[] Ribbon(Vector2[] path, float[] widths)
        {
            var points = new Vector2[path.Length * 2];
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 tangent = i == 0 ? path[1] - path[0] : i == path.Length - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                points[i] = path[i] + normal * widths[i] * 0.5f;
                points[points.Length - 1 - i] = path[i] - normal * widths[i] * 0.5f;
            }
            return points;
        }

        private static Vector2[] OffsetRibbonPath(Vector2[] path, float[] widths, float side)
        {
            var result = new Vector2[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 tangent = i == 0 ? path[1] - path[0] : i == path.Length - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                result[i] = path[i] + normal * widths[i] * side;
            }
            return result;
        }

        private static Vector2[] Offset(Vector2[] path, float dx, float dy)
        {
            var result = new Vector2[path.Length];
            for (int i = 0; i < path.Length; i++) result[i] = path[i] + new Vector2(dx, dy);
            return result;
        }

        private static float[] Scale(float[] values, float scale)
        {
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = values[i] * scale;
            return result;
        }

        private static void DrawArc(float cx, float cy, float rx, float ry, float startDeg, float endDeg, float width, Color32 color)
        {
            const int segments = 44;
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                Vector2 current = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
                if (i > 0) DrawThickLine(previous.x, previous.y, current.x, current.y, width, color);
                previous = current;
            }
        }

        private static void DrawFoamSpray(float cx, float cy, float rx, float ry, int count, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Hash01(seed + i * 19) * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Hash01(seed + i * 31 + 5));
                float x = cx + Mathf.Cos(a) * rx * r;
                float y = cy + Mathf.Sin(a) * ry * r;
                float sx = 2f + Hash01(seed + i * 43) * 4f;
                float sy = 1f + Hash01(seed + i * 59) * 2f;
                FillEllipse(x, y, sx, sy, Foam);
            }
        }

        private static void DrawHighlightDashes(int x0, int y0, int x1, int y1, int count, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, Hash01(seed + i * 17)));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, Hash01(seed + i * 29)));
                float len = 6f + Hash01(seed + i * 47) * 16f;
                float tilt = (Hash01(seed + i * 71) - 0.5f) * 4f;
                DrawThickLine(x - len * 0.5f, y, x + len * 0.5f, y + tilt, 1.5f, i % 3 == 0 ? Foam : Glint);
            }
        }

        private static void DrawThickLine(float x0, float y0, float x1, float y1, float width, Color32 color)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) * 0.55f));
            float radius = Mathf.Max(0.75f, width * 0.5f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                FillEllipse(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), radius, radius, color);
            }
        }

        private static void FillEllipse(float cx, float cy, float rx, float ry, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
            int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + rx));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry));
            int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(cy + ry));
            float invX = 1f / Mathf.Max(0.001f, rx * rx);
            float invY = 1f / Mathf.Max(0.001f, ry * ry);
            for (int y = minY; y <= maxY; y++)
            {
                float yy = (y - cy) * (y - cy) * invY;
                for (int x = minX; x <= maxX; x++)
                {
                    if ((x - cx) * (x - cx) * invX + yy <= 1f) SetPixelTop(x, y, color);
                }
            }
        }

        private static void FillPolygon(IReadOnlyList<Vector2> polygon, Color32 color)
        {
            float minYf = float.MaxValue;
            float maxYf = float.MinValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                minYf = Mathf.Min(minYf, polygon[i].y);
                maxYf = Mathf.Max(maxYf, polygon[i].y);
            }
            int minY = Mathf.Max(0, Mathf.FloorToInt(minYf));
            int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(maxYf));
            var intersections = new List<float>(polygon.Count);
            for (int y = minY; y <= maxY; y++)
            {
                intersections.Clear();
                float scanY = y + 0.5f;
                for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                {
                    Vector2 a = polygon[j];
                    Vector2 b = polygon[i];
                    if ((a.y > scanY) == (b.y > scanY)) continue;
                    float x = a.x + (scanY - a.y) * (b.x - a.x) / (b.y - a.y);
                    intersections.Add(x);
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    int x0 = Mathf.Max(0, Mathf.CeilToInt(intersections[i]));
                    int x1 = Mathf.Min(Width - 1, Mathf.FloorToInt(intersections[i + 1]));
                    for (int x = x0; x <= x1; x++) SetPixelTop(x, y, color);
                }
            }
        }

        private static void SetPixelTop(int x, int yTop, Color32 color)
        {
            if ((uint)x >= Width || (uint)yTop >= Height) return;
            int y = Height - 1 - yTop;
            pixels[y * Width + x] = color;
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }
    }
}
