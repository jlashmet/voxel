using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Bounded in-memory structure authoring surface used only by showcase composition. It executes
    /// the same integer brush contract as production authoring, then exposes the final voxel volume
    /// to a runtime mesh builder. Empty writes may also be recorded separately for cave cutaways.
    /// </summary>
    internal sealed class ShowcaseVoxelAuthoringSession : IStructureAuthoringSession
    {
        private readonly byte[] _materials;
        private readonly byte[] _coatings;
        private readonly bool[] _carved;
        private long _revision;

        public int3 Min { get; }
        public int3 Size { get; }
        public long Revision => _revision;
        public bool BudgetExceeded => false;
        public int WriteBudget => int.MaxValue;
        public long TotalVoxelsWritten { get; private set; }
        public bool RecordsCarves => _carved != null;

        public ShowcaseVoxelAuthoringSession(int3 min, int3 size, bool recordCarves = false)
        {
            if (math.any(size <= 0)) throw new ArgumentOutOfRangeException(nameof(size));
            long count = (long)size.x * size.y * size.z;
            if (count <= 0 || count > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(size), "Showcase voxel capture is too large.");
            Min = min;
            Size = size;
            _materials = new byte[(int)count];
            _coatings = new byte[(int)count];
            _carved = recordCarves ? new bool[(int)count] : null;
        }

        public byte Get(int x, int y, int z)
        {
            return TryIndex(x, y, z, out int index) ? _materials[index] : (byte)0;
        }

        public byte GetCoating(int x, int y, int z)
        {
            return TryIndex(x, y, z, out int index) ? _coatings[index] : Coatings.None;
        }

        public bool IsSolid(int x, int y, int z) => Get(x, y, z) != 0;

        public bool WasCarved(int x, int y, int z)
        {
            return _carved != null && TryIndex(x, y, z, out int index) && _carved[index];
        }

        public void Set(int x, int y, int z, byte material)
        {
            if (!TryIndex(x, y, z, out int index)) return;
            if (_materials[index] == material)
            {
                if (material == 0 && _carved != null) _carved[index] = true;
                return;
            }
            _materials[index] = material;
            if (material == 0 && _carved != null) _carved[index] = true;
            TotalVoxelsWritten++;
            _revision++;
        }

        public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
            byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None)
        {
            Set(x, y, z, material);
            if (TryIndex(x, y, z, out int index)) _coatings[index] = coating;
        }

        public void Coat(int x, int y, int z, byte coating)
        {
            if (!TryIndex(x, y, z, out int index)) return;
            _coatings[index] = coating;
            _revision++;
        }

        public void FillBulk(int3 min, int3 size, byte material) => Box(min, size, material);

        public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
        {
            for (int y = minY; y < maxYExclusive; y++) Set(x, y, z, material);
        }

        public void Box(int3 min, int3 size, byte material)
        {
            int3 max = min + size;
            for (int z = min.z; z < max.z; z++)
            for (int y = min.y; y < max.y; y++)
            for (int x = min.x; x < max.x; x++)
                Set(x, y, z, material);
        }

        public void HollowBox(int3 min, int3 size, int thickness, byte material,
            bool floor, bool ceiling)
        {
            int3 max = min + size;
            for (int z = min.z; z < max.z; z++)
            for (int y = min.y; y < max.y; y++)
            for (int x = min.x; x < max.x; x++)
            {
                bool side = x < min.x + thickness || x >= max.x - thickness ||
                            z < min.z + thickness || z >= max.z - thickness;
                bool bottom = floor && y < min.y + thickness;
                bool top = ceiling && y >= max.y - thickness;
                if (side || bottom || top) Set(x, y, z, material);
            }
        }

        public void Cylinder(int cx, int baseY, int cz, int radius, int height,
            byte material, int innerRadius = 0)
        {
            int outer2 = radius * radius;
            int inner2 = math.max(0, innerRadius) * math.max(0, innerRadius);
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx, dz = z - cz;
                int d2 = dx * dx + dz * dz;
                if (d2 > outer2 || (innerRadius > 0 && d2 < inner2)) continue;
                FillColumnBulk(x, baseY, baseY + height, z, material);
            }
        }

        public void Disc(int cx, int y, int cz, int radius, byte material)
        {
            int r2 = radius * radius;
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx, dz = z - cz;
                if (dx * dx + dz * dz <= r2) Set(x, y, z, material);
            }
        }

        public void Cone(int cx, int baseY, int cz, int radius, int height, byte material)
        {
            if (height <= 0) return;
            for (int y = 0; y < height; y++)
            {
                int r = math.max(0, radius * (height - y) / height);
                Disc(cx, baseY + y, cz, r, material);
            }
        }

        public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material)
        {
            if (height <= 0) return;
            for (int y = 0; y < height; y++)
            {
                int r = math.max(0, radius * (height - y) / height);
                Disc(cx, ceilingY - y, cz, r, material);
            }
        }

        public void Gable(int3 min, int3 size, bool alongX, byte material)
        {
            if (math.any(size <= 0)) return;
            int span = alongX ? size.z : size.x;
            int half = math.max(1, span / 2);
            for (int i = 0; i < span; i++)
            {
                int distance = math.abs(i - (span - 1) / 2);
                int h = math.max(1, size.y - distance * size.y / half);
                if (alongX)
                    Box(new int3(min.x, min.y, min.z + i), new int3(size.x, h, 1), material);
                else
                    Box(new int3(min.x + i, min.y, min.z), new int3(1, h, size.z), material);
            }
        }

        public void Crenellate(int3 start, int3 step, int count, int width, int height,
            int merlon, int gap, byte material)
        {
            int cadence = math.max(1, merlon + gap);
            for (int i = 0; i < count; i++)
            {
                int3 origin = start + step * i;
                for (int offset = 0; offset < width; offset += cadence)
                {
                    int run = math.min(merlon, width - offset);
                    if (run <= 0) break;
                    int3 p = origin;
                    int3 s;
                    if (math.abs(step.x) >= math.abs(step.z))
                    {
                        p.z += offset;
                        s = new int3(1, height, run);
                    }
                    else
                    {
                        p.x += offset;
                        s = new int3(run, height, 1);
                    }
                    Box(p, s, material);
                }
            }
        }

        public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material)
        {
            int outer2 = radius * radius;
            int inner = math.max(0, radius - 2);
            int inner2 = inner * inner;
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx, dz = z - cz;
                int d2 = dx * dx + dz * dz;
                if (d2 > outer2 || d2 < inner2) continue;
                int phase = (math.abs(dx) + math.abs(dz)) / 3;
                if ((phase & 1) == 0) FillColumnBulk(x, y, y + height, z, material);
            }
        }

        public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material)
        {
            Box(min, depthAxis == 0 ? new int3(depth, height, width) : new int3(width, height, depth), material);
            int radius = math.max(1, width / 2);
            int cx = depthAxis == 0 ? min.z + width / 2 : min.x + width / 2;
            int springY = min.y + math.max(0, height - radius);
            for (int y = springY; y < min.y + height; y++)
            {
                int dy = y - springY;
                int half = math.max(0, (int)math.sqrt(math.max(0, radius * radius - dy * dy)));
                if (depthAxis == 0)
                    Box(new int3(min.x, y, cx - half), new int3(depth, 1, half * 2 + 1), material);
                else
                    Box(new int3(cx - half, y, min.z), new int3(half * 2 + 1, 1, depth), material);
            }
        }

        public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material)
        {
            for (int i = 0; i < steps; i++)
            {
                int3 p = min;
                int3 s;
                if (axis == 0)
                {
                    p.x += i * run;
                    p.y += i * rise;
                    s = new int3(run, rise, width);
                }
                else
                {
                    p.z += i * run;
                    p.y += i * rise;
                    s = new int3(width, rise, run);
                }
                Box(p, s, material);
            }
        }

        public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material)
        {
            int steps = math.max(1, height);
            for (int i = 0; i < steps; i++)
            {
                float angle = i * 0.5f;
                int x = cx + (int)math.round(math.cos(angle) * radius);
                int z = cz + (int)math.round(math.sin(angle) * radius);
                Box(new int3(x - 1, baseY + i, z - 1), new int3(3, 1, 3), material);
            }
        }

        public void Carve(int3 min, int3 size) => Box(min, size, 0);

        public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100)
        {
            if (chanceOutOf100 <= 0) return;
            int3 max = min + size;
            for (int z = min.z; z < max.z; z++)
            for (int y = min.y; y < max.y; y++)
            for (int x = min.x; x < max.x; x++)
            {
                if (!IsSolid(x, y, z)) continue;
                uint h = unchecked(seed ^ (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791));
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                if (h % 100u < chanceOutOf100) Coat(x, y, z, coating);
            }
        }

        private bool TryIndex(int x, int y, int z, out int index)
        {
            int lx = x - Min.x, ly = y - Min.y, lz = z - Min.z;
            if ((uint)lx >= (uint)Size.x || (uint)ly >= (uint)Size.y || (uint)lz >= (uint)Size.z)
            {
                index = -1;
                return false;
            }
            index = lx + Size.x * (ly + Size.y * lz);
            return true;
        }
    }
}
