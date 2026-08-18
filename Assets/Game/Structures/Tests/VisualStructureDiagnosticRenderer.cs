using System;
using System.Collections.Generic;
using System.IO;
using Game.Materials.Api;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Structures.Tests
{
    /// <summary>
    /// Independent diagnostic renderer for authored voxel captures. Unlike the original quick
    /// isometric preview, this renderer considers all six exposed face directions and uses one
    /// projection/depth convention throughout. It intentionally depends only on Get(), so it can
    /// validate the visualizer as well as the structure authoring result.
    /// </summary>
    internal static class VisualStructureDiagnosticRenderer
    {
        public static string Render(
            VisualStructureCapture capture,
            int3 min,
            int3 size,
            string fileStem,
            int width = 1280,
            int height = 900)
        {
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            if (math.any(size <= 0)) throw new ArgumentOutOfRangeException(nameof(size));

            int3 max = min + size;
            var faces = new List<Face>(65536);
            bool hasVoxel = false;
            int3 occupiedMin = max;
            int3 occupiedMax = min;

            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
            {
                byte material = capture.Get(x, y, z);
                if (material == GameMaterialIds.Empty) continue;

                hasVoxel = true;
                occupiedMin = math.min(occupiedMin, new int3(x, y, z));
                occupiedMax = math.max(occupiedMax, new int3(x + 1, y + 1, z + 1));
                Color32 color = MaterialColor(material);

                if (capture.Get(x, y + 1, z) == GameMaterialIds.Empty)
                    faces.Add(Face.Create(x, y, z, FaceDirection.PosY, Shade(color, 1.12f)));
                if (capture.Get(x, y - 1, z) == GameMaterialIds.Empty)
                    faces.Add(Face.Create(x, y, z, FaceDirection.NegY, Shade(color, 0.54f)));
                if (capture.Get(x + 1, y, z) == GameMaterialIds.Empty)
                    faces.Add(Face.Create(x, y, z, FaceDirection.PosX, Shade(color, 0.90f)));
                if (capture.Get(x - 1, y, z) == GameMaterialIds.Empty)
                    faces.Add(Face.Create(x, y, z, FaceDirection.NegX, Shade(color, 0.66f)));
                if (capture.Get(x, y, z + 1) == GameMaterialIds.Empty)
                    faces.Add(Face.Create(x, y, z, FaceDirection.PosZ, Shade(color, 0.78f)));
                if (capture.Get(x, y, z - 1) == GameMaterialIds.Empty)
                    faces.Add(Face.Create(x, y, z, FaceDirection.NegZ, Shade(color, 0.62f)));
            }

            if (!hasVoxel || faces.Count == 0)
                throw new InvalidOperationException("Cannot render an empty diagnostic capture.");

            Vector4 projected = ProjectedBounds(occupiedMin, occupiedMax);
            const float margin = 28f;
            float scaleX = (width - margin * 2f) / math.max(1f, projected.z - projected.x);
            float scaleY = (height - margin * 2f) / math.max(1f, projected.w - projected.y);
            float scale = math.max(0.25f, math.min(scaleX, scaleY));
            float offsetX = margin - projected.x * scale;
            float offsetY = margin - projected.y * scale;

            var pixels = new Color32[width * height];
            var background = new Color32(236, 239, 242, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

            // Projection looks toward -X/-Y/-Z, so larger x+y+z is nearer. Paint far to near.
            faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            for (int i = 0; i < faces.Count; i++)
            {
                Face face = faces[i];
                Vector2 a = Screen(face.A, scale, offsetX, offsetY, height);
                Vector2 b = Screen(face.B, scale, offsetX, offsetY, height);
                Vector2 c = Screen(face.C, scale, offsetX, offsetY, height);
                Vector2 d = Screen(face.D, scale, offsetX, offsetY, height);
                FillTriangle(pixels, width, height, a, b, c, face.Color);
                FillTriangle(pixels, width, height, a, c, d, face.Color);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            string directory = Path.Combine(
                Directory.GetCurrentDirectory(), "TestResults", "WorldbuildingVisuals");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileStem + ".png");
            File.WriteAllBytes(path, png);
            TestContext.WriteLine($"Worldbuilding diagnostic visual: {path}");
            return path;
        }

        private static Vector4 ProjectedBounds(int3 min, int3 max)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int ix = 0; ix < 2; ix++)
            for (int iy = 0; iy < 2; iy++)
            for (int iz = 0; iz < 2; iz++)
            {
                Vector2 p = Project(new Vector3(
                    ix == 0 ? min.x : max.x,
                    iy == 0 ? min.y : max.y,
                    iz == 0 ? min.z : max.z));
                minX = math.min(minX, p.x);
                minY = math.min(minY, p.y);
                maxX = math.max(maxX, p.x);
                maxY = math.max(maxY, p.y);
            }
            return new Vector4(minX, minY, maxX, maxY);
        }

        private static Vector2 Project(Vector3 p)
        {
            const float cos30 = 0.8660254f;
            return new Vector2((p.x - p.z) * cos30, (p.x + p.z) * 0.5f - p.y);
        }

        private static Vector2 Screen(Vector3 p, float scale, float ox, float oy, int height)
        {
            Vector2 projected = Project(p);
            return new Vector2(projected.x * scale + ox, height - (projected.y * scale + oy));
        }

        private static Color32 MaterialColor(byte material)
        {
            uint hash = (uint)(material * 2654435761u);
            return new Color32(
                (byte)(96 + ((hash >> 16) & 95)),
                (byte)(96 + ((hash >> 8) & 95)),
                (byte)(96 + (hash & 95)),
                255);
        }

        private static Color32 Shade(Color32 c, float factor) => new Color32(
            (byte)math.clamp((int)(c.r * factor), 0, 255),
            (byte)math.clamp((int)(c.g * factor), 0, 255),
            (byte)math.clamp((int)(c.b * factor), 0, 255),
            255);

        private static void FillTriangle(
            Color32[] pixels,
            int width,
            int height,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Color32 color)
        {
            int minX = math.max(0, (int)math.floor(math.min(a.x, math.min(b.x, c.x))));
            int maxX = math.min(width - 1, (int)math.ceil(math.max(a.x, math.max(b.x, c.x))));
            int minY = math.max(0, (int)math.floor(math.min(a.y, math.min(b.y, c.y))));
            int maxY = math.min(height - 1, (int)math.ceil(math.max(a.y, math.max(b.y, c.y))));

            float area = Edge(a, b, c);
            if (math.abs(area) < 0.0001f) return;
            bool positive = area > 0f;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float e0 = Edge(a, b, p);
                float e1 = Edge(b, c, p);
                float e2 = Edge(c, a, p);
                if (positive ? e0 >= 0f && e1 >= 0f && e2 >= 0f
                             : e0 <= 0f && e1 <= 0f && e2 <= 0f)
                    pixels[y * width + x] = color;
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p) =>
            (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);

        private enum FaceDirection : byte { PosX, NegX, PosY, NegY, PosZ, NegZ }

        private readonly struct Face
        {
            public readonly Vector3 A, B, C, D;
            public readonly float Depth;
            public readonly Color32 Color;

            private Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
            {
                A = a; B = b; C = c; D = d; Color = color;
                Vector3 centre = (a + b + c + d) * 0.25f;
                Depth = centre.x + centre.y + centre.z;
            }

            public static Face Create(int x, int y, int z, FaceDirection direction, Color32 color)
            {
                float x0 = x, x1 = x + 1, y0 = y, y1 = y + 1, z0 = z, z1 = z + 1;
                switch (direction)
                {
                    case FaceDirection.PosX:
                        return new Face(new Vector3(x1,y0,z0), new Vector3(x1,y1,z0), new Vector3(x1,y1,z1), new Vector3(x1,y0,z1), color);
                    case FaceDirection.NegX:
                        return new Face(new Vector3(x0,y0,z1), new Vector3(x0,y1,z1), new Vector3(x0,y1,z0), new Vector3(x0,y0,z0), color);
                    case FaceDirection.PosY:
                        return new Face(new Vector3(x0,y1,z0), new Vector3(x0,y1,z1), new Vector3(x1,y1,z1), new Vector3(x1,y1,z0), color);
                    case FaceDirection.NegY:
                        return new Face(new Vector3(x0,y0,z1), new Vector3(x0,y0,z0), new Vector3(x1,y0,z0), new Vector3(x1,y0,z1), color);
                    case FaceDirection.PosZ:
                        return new Face(new Vector3(x1,y0,z1), new Vector3(x1,y1,z1), new Vector3(x0,y1,z1), new Vector3(x0,y0,z1), color);
                    default:
                        return new Face(new Vector3(x0,y0,z0), new Vector3(x0,y1,z0), new Vector3(x1,y1,z0), new Vector3(x1,y0,z0), color);
                }
            }
        }
    }
}
