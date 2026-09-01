using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Captures ordinary semantic structure-authoring operations into a small conservative set of
    /// coarse primitives. The session never allocates voxel storage and never expands operations
    /// per voxel; cost is bounded by the number of semantic authoring calls.
    /// </summary>
    public sealed class StructurePresentationCaptureSession : IStructurePresentationCaptureSession
    {
        private const int MaxRepresentativePrimitives = 63;

        private readonly Func<int, int, int, byte> _baselineMaterial;
        private readonly List<Representative> _representatives = new(MaxRepresentativePrimitives);
        private bool _hasBounds;
        private int3 _boundsMin;
        private int3 _boundsMax;
        private byte _unionMaterial;
        private ushort _unionSurfaceStyle;
        private byte _unionCoating;
        private VoxelSurfaceFlags _unionFlags;
        private long _estimatedWrites;

        public StructurePresentationCaptureSession(Func<int, int, int, byte> baselineMaterial = null)
        {
            _baselineMaterial = baselineMaterial;
        }

        public bool BudgetExceeded => false;
        public int WriteBudget => int.MaxValue;
        public long TotalVoxelsWritten => _estimatedWrites;

        public byte Get(int x, int y, int z) =>
            _baselineMaterial != null ? _baselineMaterial(x, y, z) : VoxelGrid.MaterialEmpty;

        public byte GetCoating(int x, int y, int z) => Coatings.None;

        public bool IsSolid(int x, int y, int z) => Get(x, y, z) != VoxelGrid.MaterialEmpty;

        public void Set(int x, int y, int z, byte material)
        {
            if (material == VoxelGrid.MaterialEmpty) return;
            AddBox(new int3(x, y, z), new int3(1), material, 0, Coatings.None, VoxelSurfaceFlags.None);
        }

        public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
            byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None)
        {
            if (material == VoxelGrid.MaterialEmpty) return;
            AddBox(new int3(x, y, z), new int3(1), material, surfaceStyle, coating, flags);
        }

        public void Coat(int x, int y, int z, byte coating) { }

        public void FillBulk(int3 min, int3 size, byte material) =>
            AddBox(min, size, material, 0, Coatings.None, VoxelSurfaceFlags.None);

        public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
        {
            AddBox(new int3(x, minY, z), new int3(1, maxYExclusive - minY, 1), material,
                0, Coatings.None, VoxelSurfaceFlags.None);
        }

        public void Box(int3 min, int3 size, byte material) =>
            AddBox(min, size, material, 0, Coatings.None, VoxelSurfaceFlags.None);

        public void HollowBox(int3 min, int3 size, int thickness, byte material,
            bool floor, bool ceiling) =>
            AddBox(min, size, material, 0, Coatings.None, VoxelSurfaceFlags.None);

        public void Cylinder(int cx, int baseY, int cz, int radius, int height,
            byte material, int innerRadius = 0) =>
            AddBox(new int3(cx - radius, baseY, cz - radius),
                new int3(radius * 2 + 1, height, radius * 2 + 1), material,
                0, Coatings.None, VoxelSurfaceFlags.None);

        public void Disc(int cx, int y, int cz, int radius, byte material) =>
            AddBox(new int3(cx - radius, y, cz - radius),
                new int3(radius * 2 + 1, 1, radius * 2 + 1), material,
                0, Coatings.None, VoxelSurfaceFlags.None);

        public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) =>
            AddBox(new int3(cx - radius, baseY, cz - radius),
                new int3(radius * 2 + 1, height, radius * 2 + 1), material,
                0, Coatings.None, VoxelSurfaceFlags.None);

        public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) =>
            AddBox(new int3(cx - radius, ceilingY - math.max(0, height - 1), cz - radius),
                new int3(radius * 2 + 1, height, radius * 2 + 1), material,
                0, Coatings.None, VoxelSurfaceFlags.None);

        public void Gable(int3 min, int3 size, bool alongX, byte material) =>
            AddBox(min, size, material, 0, Coatings.None, VoxelSurfaceFlags.None);

        public void Crenellate(int3 start, int3 step, int count, int width, int height,
            int merlon, int gap, byte material)
        {
            if (count <= 0 || width <= 0 || height <= 0) return;
            int3 end = start + step * (count - 1);
            int3 min = math.min(start, end);
            int3 max = math.max(start, end);
            int horizontalPad = math.max(0, width - 1);
            max += new int3(horizontalPad, height - 1, horizontalPad);
            AddInclusiveBounds(min, max, material, 0, Coatings.None, VoxelSurfaceFlags.None);
        }

        public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) =>
            AddBox(new int3(cx - radius, y, cz - radius),
                new int3(radius * 2 + 1, height, radius * 2 + 1), material,
                0, Coatings.None, VoxelSurfaceFlags.None);

        public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material)
        {
            int3 size = depthAxis == 0
                ? new int3(depth, height, width)
                : new int3(width, height, depth);
            AddBox(min, size, material, 0, Coatings.None, VoxelSurfaceFlags.None);
        }

        public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material)
        {
            if (steps <= 0) return;
            int length = steps * math.max(1, run);
            int height = steps * math.max(1, rise);
            int3 size = axis == 0
                ? new int3(length, height, width)
                : new int3(width, height, length);
            AddBox(min, size, material, 0, Coatings.None, VoxelSurfaceFlags.None);
        }

        public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) =>
            AddBox(new int3(cx - radius - 1, baseY, cz - radius - 1),
                new int3((radius + 1) * 2 + 1, height, (radius + 1) * 2 + 1), material,
                0, Coatings.None, VoxelSurfaceFlags.None);

        public void Carve(int3 min, int3 size)
        {
            // A far bake is conservative outer massing. Carves may remove interior occupancy but
            // never expand the silhouette, so replaying them is unnecessary for this derived tier.
        }

        public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100)
        {
            // Weathering is surface-only and does not affect conservative occupancy or bounds.
        }

        public FeaturePresentationBake Bake(
            ulong sourceId,
            ulong revisionSeed,
            FeatureKind kind,
            int3 position,
            byte orientation = 0)
        {
            if (!_hasBounds)
                throw new InvalidOperationException("Presentation capture contains no solid authoring operations.");

            var primitives = new Primitive[_representatives.Count + 1];
            ulong revision = Mix(revisionSeed ^ sourceId ^ (ulong)kind ^ orientation);
            for (int i = 0; i < _representatives.Count; i++)
            {
                primitives[i] = _representatives[i].Primitive;
                revision = HashPrimitive(revision, in primitives[i]);
            }

            primitives[^1] = CreateBoxPrimitive(
                _boundsMin, _boundsMax, _unionMaterial, _unionSurfaceStyle, _unionCoating, _unionFlags,
                _representatives.Count);
            revision = HashPrimitive(revision, in primitives[^1]);

            return new FeaturePresentationBake(
                sourceId,
                revision,
                kind,
                position,
                orientation,
                _boundsMin,
                _boundsMax,
                primitives);
        }

        private void AddBox(int3 min, int3 size, byte material, ushort surfaceStyle,
            byte coating, VoxelSurfaceFlags flags)
        {
            if (material == VoxelGrid.MaterialEmpty || math.any(size <= 0)) return;
            AddInclusiveBounds(min, min + size - 1, material, surfaceStyle, coating, flags);
        }

        private void AddInclusiveBounds(int3 min, int3 max, byte material, ushort surfaceStyle,
            byte coating, VoxelSurfaceFlags flags)
        {
            if (material == VoxelGrid.MaterialEmpty || math.any(max < min)) return;

            if (!_hasBounds)
            {
                _hasBounds = true;
                _boundsMin = min;
                _boundsMax = max;
                _unionMaterial = material;
                _unionSurfaceStyle = surfaceStyle;
                _unionCoating = coating;
                _unionFlags = flags;
            }
            else
            {
                _boundsMin = math.min(_boundsMin, min);
                _boundsMax = math.max(_boundsMax, max);
            }

            long volume = SaturatingVolume(min, max);
            _estimatedWrites = SaturatingAdd(_estimatedWrites, volume);
            var candidate = new Representative(
                CreateBoxPrimitive(min, max, material, surfaceStyle, coating, flags, 0), volume);

            if (_representatives.Count < MaxRepresentativePrimitives)
            {
                _representatives.Add(candidate);
                return;
            }

            int smallest = 0;
            long smallestVolume = _representatives[0].Volume;
            for (int i = 1; i < _representatives.Count; i++)
            {
                if (_representatives[i].Volume >= smallestVolume) continue;
                smallest = i;
                smallestVolume = _representatives[i].Volume;
            }
            if (volume > smallestVolume) _representatives[smallest] = candidate;
        }

        private static Primitive CreateBoxPrimitive(int3 min, int3 max, byte material,
            ushort surfaceStyle, byte coating, VoxelSurfaceFlags flags, int order) => new()
        {
            Shape = PrimitiveShape.Box,
            Mode = PrimitiveMode.Fill,
            Material = material,
            SurfaceStyle = surfaceStyle,
            Coating = coating,
            SurfaceFlags = flags,
            Order = order,
            A = min,
            B = max,
            Direction = 1,
        };

        private static long SaturatingVolume(int3 min, int3 max)
        {
            long x = (long)max.x - min.x + 1;
            long y = (long)max.y - min.y + 1;
            long z = (long)max.z - min.z + 1;
            if (x <= 0 || y <= 0 || z <= 0) return 0;
            if (x > long.MaxValue / y) return long.MaxValue;
            long xy = x * y;
            return z > long.MaxValue / xy ? long.MaxValue : xy * z;
        }

        private static long SaturatingAdd(long left, long right) =>
            right > long.MaxValue - left ? long.MaxValue : left + right;

        private static ulong HashPrimitive(ulong hash, in Primitive primitive)
        {
            hash = Hash(hash, (ulong)primitive.Shape);
            hash = Hash(hash, primitive.Material);
            hash = Hash(hash, primitive.SurfaceStyle);
            hash = Hash(hash, primitive.Coating);
            hash = Hash(hash, (ulong)primitive.SurfaceFlags);
            hash = Hash(hash, unchecked((uint)primitive.A.x));
            hash = Hash(hash, unchecked((uint)primitive.A.y));
            hash = Hash(hash, unchecked((uint)primitive.A.z));
            hash = Hash(hash, unchecked((uint)primitive.B.x));
            hash = Hash(hash, unchecked((uint)primitive.B.y));
            hash = Hash(hash, unchecked((uint)primitive.B.z));
            return hash;
        }

        private static ulong Hash(ulong hash, ulong value) => Mix(hash ^ Mix(value + 0x9E3779B97F4A7C15ul));

        private static ulong Mix(ulong value)
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9ul;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBul;
            return value ^ (value >> 31);
        }

        private readonly struct Representative
        {
            public Representative(Primitive primitive, long volume)
            {
                Primitive = primitive;
                Volume = volume;
            }

            public Primitive Primitive { get; }
            public long Volume { get; }
        }
    }
}
