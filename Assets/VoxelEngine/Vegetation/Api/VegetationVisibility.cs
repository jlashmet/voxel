using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Vegetation.Api
{
    public readonly struct VisibilitySectorBounds
    {
        public VisibilitySectorBounds(int minX, int minZ, int maxX, int maxZ)
        {
            if (maxX < minX) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (maxZ < minZ) throw new ArgumentOutOfRangeException(nameof(maxZ));
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }

        public bool Contains(int sectorX, int sectorZ) =>
            sectorX >= MinX && sectorX <= MaxX && sectorZ >= MinZ && sectorZ <= MaxZ;

        public static VisibilitySectorBounds Around(float2 centerMetres, float radiusMetres, float sectorSizeMetres)
        {
            if (!(radiusMetres >= 0f) || !math.isfinite(radiusMetres))
                throw new ArgumentOutOfRangeException(nameof(radiusMetres));
            if (!(sectorSizeMetres > 0f) || !math.isfinite(sectorSizeMetres))
                throw new ArgumentOutOfRangeException(nameof(sectorSizeMetres));

            int minX = (int)math.floor((centerMetres.x - radiusMetres) / sectorSizeMetres);
            int minZ = (int)math.floor((centerMetres.y - radiusMetres) / sectorSizeMetres);
            int maxX = (int)math.floor((centerMetres.x + radiusMetres) / sectorSizeMetres);
            int maxZ = (int)math.floor((centerMetres.y + radiusMetres) / sectorSizeMetres);
            return new VisibilitySectorBounds(minX, minZ, maxX, maxZ);
        }
    }

    public readonly struct VegetationVisibilityEntry
    {
        public VegetationVisibilityEntry(ulong stableId, int sourceIndex, int sectorX, int sectorZ, VegetationInstance instance)
        {
            StableId = stableId;
            SourceIndex = sourceIndex;
            SectorX = sectorX;
            SectorZ = sectorZ;
            Instance = instance;
        }

        public ulong StableId { get; }
        public int SourceIndex { get; }
        public int SectorX { get; }
        public int SectorZ { get; }
        public VegetationInstance Instance { get; }
    }

    public readonly struct TreeVisibilityEntry
    {
        public TreeVisibilityEntry(
            ulong stableId,
            int sourceIndex,
            int sectorX,
            int sectorZ,
            TreeInstance instance,
            TreeDamageState damage,
            ulong presentationRevision = 0UL)
        {
            StableId = stableId;
            SourceIndex = sourceIndex;
            SectorX = sectorX;
            SectorZ = sectorZ;
            Instance = instance;
            Damage = damage;
            PresentationRevision = presentationRevision;
        }

        public ulong StableId { get; }
        public int SourceIndex { get; }
        public int SectorX { get; }
        public int SectorZ { get; }
        public TreeInstance Instance { get; }
        public TreeDamageState Damage { get; }

        /// <summary>
        /// Deterministic invalidation token derived from the authoritative tree read source. Far
        /// presentation caches may compare this value, but it is not a second tree-state store.
        /// </summary>
        public ulong PresentationRevision { get; }
    }

    /// <summary>
    /// Stateless visibility projection over existing vegetation truth. It owns no placement or tree
    /// state: callers supply the current semantic instances/read source and receive stable sorted refs.
    /// </summary>
    public static class VegetationVisibility
    {
        public static void QueryVegetation(
            IReadOnlyList<VegetationInstance> instances,
            float sectorSizeMetres,
            in VisibilitySectorBounds sectors,
            List<VegetationVisibilityEntry> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            ValidateSectorSize(sectorSizeMetres);
            output.Clear();
            if (instances == null) return;

            for (int i = 0; i < instances.Count; i++)
            {
                VegetationInstance instance = instances[i];
                int sectorX = Sector(instance.PositionMetres.x, sectorSizeMetres);
                int sectorZ = Sector(instance.PositionMetres.z, sectorSizeMetres);
                if (!sectors.Contains(sectorX, sectorZ)) continue;
                output.Add(new VegetationVisibilityEntry(
                    StableVegetationId(instance), i, sectorX, sectorZ, instance));
            }
            output.Sort((a, b) => a.StableId.CompareTo(b.StableId));
        }

        public static void QueryTrees(
            ITreeWorldReadSource source,
            float sectorSizeMetres,
            in VisibilitySectorBounds sectors,
            List<TreeVisibilityEntry> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            ValidateSectorSize(sectorSizeMetres);
            output.Clear();
            if (source == null) return;

            IReadOnlyList<TreeInstance> instances = source.Instances;
            IReadOnlyList<TreeDamageState> damage = source.Damage;
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                int sectorX = Sector(instance.PositionMetres.x, sectorSizeMetres);
                int sectorZ = Sector(instance.PositionMetres.z, sectorSizeMetres);
                if (!sectors.Contains(sectorX, sectorZ)) continue;
                TreeDamageState state = i < damage.Count ? damage[i] : new TreeDamageState(1f, false);
                ulong stableId = StableTreeId(instance);
                ulong revision = TreePresentationRevision(stableId, state, source.RemovedBranches(i));
                output.Add(new TreeVisibilityEntry(
                    stableId, i, sectorX, sectorZ, instance, state, revision));
            }
            output.Sort((a, b) => a.StableId.CompareTo(b.StableId));
        }

        public static ulong StableVegetationId(in VegetationInstance instance)
        {
            ulong hash = FnvOffset;
            hash = HashUInt(hash, instance.Seed);
            hash = HashInt(hash, QuantizePosition(instance.PositionMetres.x));
            hash = HashInt(hash, QuantizePosition(instance.PositionMetres.y));
            hash = HashInt(hash, QuantizePosition(instance.PositionMetres.z));
            hash = HashInt(hash, QuantizeUnit(instance.SurfaceNormal.x));
            hash = HashInt(hash, QuantizeUnit(instance.SurfaceNormal.y));
            hash = HashInt(hash, QuantizeUnit(instance.SurfaceNormal.z));
            hash = HashByte(hash, (byte)instance.Kind);
            hash = HashInt(hash, QuantizeScale(instance.Scale));
            return hash;
        }

        public static ulong StableTreeId(in TreeInstance instance)
        {
            ulong hash = FnvOffset;
            hash = HashUInt(hash, instance.Seed);
            hash = HashInt(hash, QuantizePosition(instance.PositionMetres.x));
            hash = HashInt(hash, QuantizePosition(instance.PositionMetres.y));
            hash = HashInt(hash, QuantizePosition(instance.PositionMetres.z));
            hash = HashByte(hash, (byte)instance.Species);
            hash = HashInt(hash, QuantizeScale(instance.Scale));
            return hash;
        }

        /// <summary>
        /// Returns a deterministic invalidation token for one tree's far presentation using only
        /// authoritative state already exposed by <see cref="ITreeWorldReadSource"/>. Branch removal
        /// contribution is order-independent so HashSet-backed state remains deterministic.
        /// </summary>
        public static ulong TreePresentationRevision(
            ulong stableId,
            in TreeDamageState damage,
            IReadOnlyCollection<int> removedBranches)
        {
            ulong hash = HashUlong(FnvOffset, stableId);
            hash = HashInt(hash, QuantizeHealth(damage.FoliageHealth));
            hash = HashByte(hash, damage.Severed ? (byte)1 : (byte)0);

            ulong branchXor = 0UL;
            ulong branchSum = 0UL;
            int branchCount = 0;
            if (removedBranches != null)
            {
                foreach (int branchIndex in removedBranches)
                {
                    ulong member = HashInt(FnvOffset, branchIndex);
                    branchXor ^= member;
                    branchSum = unchecked(branchSum + member);
                    branchCount++;
                }
            }

            hash = HashUlong(hash, branchXor);
            hash = HashUlong(hash, branchSum);
            return HashInt(hash, branchCount);
        }

        private static int Sector(float metres, float sectorSizeMetres) =>
            (int)math.floor(metres / sectorSizeMetres);

        private static int QuantizePosition(float metres) => checked((int)math.round(metres * 10f));
        private static int QuantizeUnit(float value) => checked((int)math.round(value * 4096f));
        private static int QuantizeScale(float value) => checked((int)math.round(value * 4096f));
        private static int QuantizeHealth(float value) => checked((int)math.round(math.saturate(value) * 4096f));

        private static void ValidateSectorSize(float sectorSizeMetres)
        {
            if (!(sectorSizeMetres > 0f) || !math.isfinite(sectorSizeMetres))
                throw new ArgumentOutOfRangeException(nameof(sectorSizeMetres));
        }

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong HashByte(ulong hash, byte value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }

        private static ulong HashInt(ulong hash, int value) => HashUInt(hash, unchecked((uint)value));

        private static ulong HashUInt(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= FnvPrime;
            }
            return hash;
        }

        private static ulong HashUlong(ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}
