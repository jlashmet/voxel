using System;
using System.Collections.Generic;
using System.IO;
using Game.Materials.Api;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    /// <summary>
    /// Dense bounded voxel capture session for human-inspectable visual regression output.
    /// Structure authorers execute their real IStructureAuthoringSession writes into this volume;
    /// exposed voxel faces are then rasterized with a deterministic software isometric camera.
    /// Optional empty-write recording makes excavation/cave voids directly renderable as cutaways.
    /// </summary>
    internal sealed class VisualStructureCapture : IStructureAuthoringSession
    {
        private readonly int3 _min;
        private readonly int3 _size;
        private readonly byte[] _voxels;
        private readonly byte[] _voidWrites;
        private bool _hasSolid;
        private int3 _occupiedMin;
        private int3 _occupiedMaxExclusive;
        private bool _hasVoid;
        private int3 _voidMin;
        private int3 _voidMaxExclusive;

        public VisualStructureCapture(int3 min, int3 size, bool recordEmptyWrites = false)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));
            long volume = (long)size.x * size.y * size.z;
            if (volume > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(size), "Visual capture volume is too large.");

            _min = min;
            _size = size;
            _voxels = new byte[(int)volume];
            _voidWrites = recordEmptyWrites ? new byte[(int)volume] : null;
            _occupiedMin = min + size;
            _occupiedMaxExclusive = min;
            _voidMin = min + size;
            _voidMaxExclusive = min;
        }

        public bool BudgetExceeded => false;
        public int WriteBudget => int.MaxValue;
        public long TotalVoxelsWritten { get; private set; }

        public byte Get(int x, int y, int z) =>
            TryIndex(x, y, z, out int index) ? _voxels[index] : GameMaterialIds.Empty;

        public byte GetCoating(int x, int y, int z) => Coatings.None;
        public bool IsSolid(int x, int y, int z) => Get(x, y, z) != GameMaterialIds.Empty;

        public void Set(int x, int y, int z, byte material)
        {
            if (!TryIndex(x, y, z, out int index)) return;

            if (_voidWrites != null)
            {
                if (material == GameMaterialIds.Empty)
                {
                    if (_voidWrites[index] == 0)
                    {
                        _voidWrites[index] = 1;
                        IncludeVoid(x, y, z);
                    }
                }
                else
                {
                    _voidWrites[index] = 0;
                }
            }

            byte previous = _voxels[index];
            if (previous == material) return;

            _voxels[index] = material;
            TotalVoxelsWritten++;
            if (material != GameMaterialIds.Empty)
                IncludeSolid(x, y, z);
        }

        public void SetStyled(
            int x,
            int y,
            int z,
            byte material,
            ushort surfaceStyle,
            byte coating = Coatings.None,
            VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => Set(x, y, z, material);

        public void Coat(int x, int y, int z, byte coating) { }
        public void FillBulk(int3 min, int3 size, byte material) => Box(min, size, material);

        public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
        {
            for (int y = minY; y < maxYExclusive; y++) Set(x, y, z, material);
        }

        public void Box(int3 min, int3 size, byte material)
        {
            int3 max = min + size;
            int sx = math.max(min.x, _min.x);
            int sy = math.max(min.y, _min.y);
            int sz = math.max(min.z, _min.z);
            int ex = math.min(max.x, _min.x + _size.x);
            int ey = math.min(max.y, _min.y + _size.y);
            int ez = math.min(max.z, _min.z + _size.z);

            for (int y = sy; y < ey; y++)
            for (int z = sz; z < ez; z++)
            for (int x = sx; x < ex; x++)
                Set(x, y, z, material);
        }

        public void HollowBox(
            int3 min,
            int3 size,
            int thickness,
            byte material,
            bool floor,
            bool ceiling)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
            {
                bool wall = x < min.x + thickness || x >= max.x - thickness ||
                            z < min.z + thickness || z >= max.z - thickness;
                bool floorVoxel = floor && y < min.y + thickness;
                bool ceilingVoxel = ceiling && y >= max.y - thickness;
                if (wall || floorVoxel || ceilingVoxel) Set(x, y, z, material);
            }
        }

        public void Cylinder(
            int cx,
            int baseY,
            int cz,
            int radius,
            int height,
            byte material,
            int innerRadius = 0)
        {
            int outer2 = radius * radius;
            int inner2 = innerRadius * innerRadius;
            for (int y = baseY; y < baseY + height; y++)
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx;
                int dz = z - cz;
                int d2 = dx * dx + dz * dz;
                if (d2 <= outer2 && (innerRadius <= 0 || d2 >= inner2))
                    Set(x, y, z, material);
            }
        }

        public void Disc(int cx, int y, int cz, int radius, byte material)
        {
            int r2 = radius * radius;
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx;
                int dz = z - cz;
                if (dx * dx + dz * dz <= r2) Set(x, y, z, material);
            }
        }

        public void Cone(int cx, int baseY, int cz, int radius, int height, byte material)
        {
            for (int layer = 0; layer < height; layer++)
            {
                int r = math.max(0, radius * (height - 1 - layer) / math.max(1, height - 1));
                Disc(cx, baseY + layer, cz, r, material);
            }
        }

        public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material)
        {
            for (int layer = 0; layer < height; layer++)
            {
                int r = math.max(0, radius * (height - 1 - layer) / math.max(1, height - 1));
                Disc(cx, ceilingY - layer, cz, r, material);
            }
        }

        public void Gable(int3 min, int3 size, bool alongX, byte material)
        {
            int span = alongX ? size.z : size.x;
            int half = math.max(1, (span - 1) / 2);
            for (int s = 0; s < span; s++)
            {
                int distance = math.abs(s - (span - 1) / 2);
                int columnHeight = math.max(1, size.y - distance * size.y / math.max(1, half + 1));
                if (alongX)
                    Box(new int3(min.x, min.y, min.z + s), new int3(size.x, columnHeight, 1), material);
                else
                    Box(new int3(min.x + s, min.y, min.z), new int3(1, columnHeight, size.z), material);
            }
        }

        public void Crenellate(
            int3 start,
            int3 step,
            int count,
            int width,
            int height,
            int merlon,
            int gap,
            byte material)
        {
            for (int i = 0; i < count; i++)
            {
                int3 p = start + step * i;
                int3 size = new int3(
                    step.x != 0 ? math.max(1, merlon) : width,
                    height,
                    step.z != 0 ? math.max(1, merlon) : width);
                Box(p, size, material);
            }
        }

        public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material)
        {
            int circumference = math.max(8, radius * 6);
            for (int i = 0; i < circumference; i += 3)
            {
                double angle = i * Math.PI * 2.0 / circumference;
                int x = cx + (int)Math.Round(Math.Cos(angle) * radius);
                int z = cz + (int)Math.Round(Math.Sin(angle) * radius);
                Box(new int3(x, y, z), new int3(2, height, 2), material);
            }
        }

        public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material)
        {
            // Legacy direct Arch calls are represented by their bounded envelope. Shared modern
            // openings carve through Box, so their final occupancy is captured exactly.
            int3 size = depthAxis == 0
                ? new int3(depth, height, width)
                : new int3(width, height, depth);
            Box(min, size, material);
        }

        public void Stairs(
            int3 min,
            int width,
            int steps,
            int rise,
            int run,
            int axis,
            byte material)
        {
            for (int i = 0; i < steps; i++)
            {
                int3 p = min;
                int3 size;
                if (axis == 0)
                {
                    p.x += i * run;
                    p.y += i * rise;
                    size = new int3(run, rise, width);
                }
                else
                {
                    p.z += i * run;
                    p.y += i * rise;
                    size = new int3(width, rise, run);
                }
                Box(p, size, material);
            }
        }

        public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material)
        {
            for (int i = 0; i < math.max(1, height); i++)
            {
                double angle = i * Math.PI / 4.0;
                int x = cx + (int)Math.Round(Math.Cos(angle) * radius);
                int z = cz + (int)Math.Round(Math.Sin(angle) * radius);
                Box(new int3(x, baseY + i, z), new int3(2, 1, 2), material);
            }
        }

        public void Carve(int3 min, int3 size) => Box(min, size, GameMaterialIds.Empty);
        public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }

        public string RenderPng(string fileStem, int width = 1280, int height = 900)
        {
            if (!_hasSolid)
                throw new InvalidOperationException("Cannot render an empty visual capture.");
            return RenderFaces(
                fileStem,
                BuildSolidFaces(),
                _occupiedMin,
                _occupiedMaxExclusive,
                width,
                height);
        }

        public string RenderCarvedVoidPng(string fileStem, int width = 1280, int height = 900)
        {
            if (_voidWrites == null)
                throw new InvalidOperationException("Construct the capture with recordEmptyWrites=true.");
            if (!_hasVoid)
                throw new InvalidOperationException("No carve/empty writes were recorded.");
            return RenderFaces(
                fileStem,
                BuildVoidFaces(),
                _voidMin,
                _voidMaxExclusive,
                width,
                height);
        }

        private string RenderFaces(
            string fileStem,
            List<Face> faces,
            int3 boundsMin,
            int3 boundsMaxExclusive,
            int width,
            int height)
        {
            if (faces.Count == 0)
                throw new InvalidOperationException("Visual capture contained no exposed faces.");

            Vector4 projectedBounds = ComputeProjectedBounds(boundsMin, boundsMaxExclusive);
            const float margin = 28f;
            float scaleX = (width - margin * 2f) / math.max(1f, projectedBounds.z - projectedBounds.x);
            float scaleY = (height - margin * 2f) / math.max(1f, projectedBounds.w - projectedBounds.y);
            float scale = math.max(0.25f, math.min(scaleX, scaleY));
            float offsetX = margin - projectedBounds.x * scale;
            float offsetY = margin - projectedBounds.y * scale;

            var pixels = new Color32[width * height];
            var background = new Color32(236, 239, 242, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

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
            TestContext.WriteLine($"Worldbuilding visual: {path}");
            return path;
        }

        private List<Face> BuildSolidFaces()
        {
            var faces = new List<Face>(65536);
            for (int y = _occupiedMin.y; y < _occupiedMaxExclusive.y; y++)
            for (int z = _occupiedMin.z; z < _occupiedMaxExclusive.z; z++)
            for (int x = _occupiedMin.x; x < _occupiedMaxExclusive.x; x++)
            {
                byte material = Get(x, y, z);
                if (material == GameMaterialIds.Empty) continue;

                Color32 baseColor = MaterialColor(material);
                if (!IsSolid(x, y + 1, z))
                    faces.Add(Face.Top(x, y, z, Shade(baseColor, 1.10f)));
                if (!IsSolid(x + 1, y, z))
                    faces.Add(Face.Right(x, y, z, Shade(baseColor, 0.88f)));
                if (!IsSolid(x, y, z - 1))
                    faces.Add(Face.Left(x, y, z, Shade(baseColor, 0.70f)));
            }
            return faces;
        }

        private List<Face> BuildVoidFaces()
        {
            var faces = new List<Face>(32768);
            var baseColor = new Color32(210, 132, 76, 255);
            for (int y = _voidMin.y; y < _voidMaxExclusive.y; y++)
            for (int z = _voidMin.z; z < _voidMaxExclusive.z; z++)
            for (int x = _voidMin.x; x < _voidMaxExclusive.x; x++)
            {
                if (!IsVoid(x, y, z)) continue;
                if (!IsVoid(x, y + 1, z))
                    faces.Add(Face.Top(x, y, z, Shade(baseColor, 1.10f)));
                if (!IsVoid(x + 1, y, z))
                    faces.Add(Face.Right(x, y, z, Shade(baseColor, 0.88f)));
                if (!IsVoid(x, y, z - 1))
                    faces.Add(Face.Left(x, y, z, Shade(baseColor, 0.70f)));
            }
            return faces;
        }

        private Vector4 ComputeProjectedBounds(int3 min, int3 max)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            IncludeProjection(min.x, min.y, min.z, ref minX, ref minY, ref maxX, ref maxY);
            IncludeProjection(max.x, min.y, min.z, ref minX, ref minY, ref maxX, ref maxY);
            IncludeProjection(min.x, max.y, min.z, ref minX, ref minY, ref maxX, ref maxY);
            IncludeProjection(max.x, max.y, min.z, ref minX, ref minY, ref maxX, ref maxY);
            IncludeProjection(min.x, min.y, max.z, ref minX, ref minY, ref maxX, ref maxY);
            IncludeProjection(max.x, min.y, max.z, ref minX, ref minY, ref maxX, ref maxY);
            IncludeProjection(min.x, max.y, max.z, ref minX, ref minY, ref maxX, ref maxY);
            IncludeProjection(max.x, max.y, max.z, ref minX, ref minY, ref maxX, ref maxY);
            return new Vector4(minX, minY, maxX, maxY);
        }

        private static void IncludeProjection(
            int x, int y, int z,
            ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            Vector2 p = Project(new Vector3(x, y, z));
            minX = math.min(minX, p.x);
            minY = math.min(minY, p.y);
            maxX = math.max(maxX, p.x);
            maxY = math.max(maxY, p.y);
        }

        private static Vector2 Screen(
            Vector3 world, float scale, float offsetX, float offsetY, int imageHeight)
        {
            Vector2 p = Project(world);
            return new Vector2(
                p.x * scale + offsetX,
                imageHeight - (p.y * scale + offsetY));
        }

        private static Vector2 Project(Vector3 p)
        {
            const float cos30 = 0.8660254f;
            return new Vector2((p.x - p.z) * cos30, (p.x + p.z) * 0.5f - p.y);
        }

        private static Color32 MaterialColor(byte material)
        {
            uint h = material * 2654435761u;
            byte r = (byte)(96 + ((h >> 16) & 95));
            byte g = (byte)(96 + ((h >> 8) & 95));
            byte b = (byte)(96 + (h & 95));
            return new Color32(r, g, b, 255);
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
            float area = Edge(a, b, c);
            if (math.abs(area) < 0.0001f) return;

            int minX = math.clamp((int)Math.Floor(math.min(a.x, math.min(b.x, c.x))), 0, width - 1);
            int maxX = math.clamp((int)Math.Ceiling(math.max(a.x, math.max(b.x, c.x))), 0, width - 1);
            int minY = math.clamp((int)Math.Floor(math.min(a.y, math.min(b.y, c.y))), 0, height - 1);
            int maxY = math.clamp((int)Math.Ceiling(math.max(a.y, math.max(b.y, c.y))), 0, height - 1);
            bool positive = area > 0f;

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * width;
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float e0 = Edge(a, b, p);
                    float e1 = Edge(b, c, p);
                    float e2 = Edge(c, a, p);
                    if (positive
                        ? e0 >= 0f && e1 >= 0f && e2 >= 0f
                        : e0 <= 0f && e1 <= 0f && e2 <= 0f)
                        pixels[row + x] = color;
                }
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p) =>
            (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);

        private void IncludeSolid(int x, int y, int z)
        {
            var p = new int3(x, y, z);
            if (!_hasSolid)
            {
                _hasSolid = true;
                _occupiedMin = p;
                _occupiedMaxExclusive = p + 1;
                return;
            }
            _occupiedMin = math.min(_occupiedMin, p);
            _occupiedMaxExclusive = math.max(_occupiedMaxExclusive, p + 1);
        }

        private void IncludeVoid(int x, int y, int z)
        {
            var p = new int3(x, y, z);
            if (!_hasVoid)
            {
                _hasVoid = true;
                _voidMin = p;
                _voidMaxExclusive = p + 1;
                return;
            }
            _voidMin = math.min(_voidMin, p);
            _voidMaxExclusive = math.max(_voidMaxExclusive, p + 1);
        }

        private bool IsVoid(int x, int y, int z)
        {
            return _voidWrites != null &&
                   TryIndex(x, y, z, out int index) &&
                   _voidWrites[index] != 0;
        }

        private bool TryIndex(int x, int y, int z, out int index)
        {
            int lx = x - _min.x;
            int ly = y - _min.y;
            int lz = z - _min.z;
            if ((uint)lx >= (uint)_size.x ||
                (uint)ly >= (uint)_size.y ||
                (uint)lz >= (uint)_size.z)
            {
                index = -1;
                return false;
            }
            index = (ly * _size.z + lz) * _size.x + lx;
            return true;
        }

        private readonly struct Face
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Vector3 C;
            public readonly Vector3 D;
            public readonly float Depth;
            public readonly Color32 Color;

            private Face(
                Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                float depth, Color32 color)
            {
                A = a;
                B = b;
                C = c;
                D = d;
                Depth = depth;
                Color = color;
            }

            public static Face Top(int x, int y, int z, Color32 color) => new Face(
                new Vector3(x, y + 1, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x, y + 1, z + 1),
                x - z + y + 0.75f,
                color);

            public static Face Right(int x, int y, int z, Color32 color) => new Face(
                new Vector3(x + 1, y, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y + 1, z + 1),
                new Vector3(x + 1, y, z + 1),
                x - z + y + 0.50f,
                color);

            public static Face Left(int x, int y, int z, Color32 color) => new Face(
                new Vector3(x, y, z),
                new Vector3(x, y + 1, z),
                new Vector3(x + 1, y + 1, z),
                new Vector3(x + 1, y, z),
                x - z + y + 0.25f,
                color);
        }
    }
}
