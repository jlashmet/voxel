using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Vegetation.Api
{
    public readonly struct ForestCanopyCluster
    {
        private readonly ulong[] _memberIds;

        internal ForestCanopyCluster(
            ulong stableId,
            int sectorX,
            int sectorZ,
            float3 centreMetres,
            float2 halfExtentMetres,
            float maxHeightMetres,
            float meanHeightMetres,
            float meanFoliageHealth,
            ulong revision,
            ulong[] memberIds)
        {
            StableId = stableId;
            SectorX = sectorX;
            SectorZ = sectorZ;
            CentreMetres = centreMetres;
            HalfExtentMetres = halfExtentMetres;
            MaxHeightMetres = maxHeightMetres;
            MeanHeightMetres = meanHeightMetres;
            MeanFoliageHealth = meanFoliageHealth;
            Revision = revision;
            _memberIds = memberIds ?? Array.Empty<ulong>();
        }

        public ulong StableId { get; }
        public int SectorX { get; }
        public int SectorZ { get; }
        public float3 CentreMetres { get; }
        public float2 HalfExtentMetres { get; }
        public float MaxHeightMetres { get; }
        public float MeanHeightMetres { get; }
        public float MeanFoliageHealth { get; }
        public ulong Revision { get; }
        public IReadOnlyList<ulong> MemberIds => _memberIds;
        public int MemberCount => _memberIds.Length;
    }

    /// <summary>
    /// Deterministic forest HLOD facts derived from current tree visibility records. The builder owns
    /// no tree persistence. Landmark selection is injected policy; severed trees are omitted and each
    /// member's authoritative presentation revision invalidates only its affected cluster.
    /// </summary>
    public static class ForestCanopyClusterBuilder
    {
        public static IReadOnlyList<ForestCanopyCluster> Build(
            IReadOnlyList<TreeVisibilityEntry> trees,
            Func<TreeVisibilityEntry, bool> isIndependentLandmark = null)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));

            var groups = new Dictionary<SectorKey, List<TreeVisibilityEntry>>();
            for (int i = 0; i < trees.Count; i++)
            {
                TreeVisibilityEntry tree = trees[i];
                if (tree.Damage.Severed || (isIndependentLandmark != null && isIndependentLandmark(tree)))
                    continue;

                var key = new SectorKey(tree.SectorX, tree.SectorZ);
                if (!groups.TryGetValue(key, out List<TreeVisibilityEntry> members))
                {
                    members = new List<TreeVisibilityEntry>();
                    groups.Add(key, members);
                }
                members.Add(tree);
            }

            var sectors = new List<SectorKey>(groups.Keys);
            sectors.Sort(SectorKey.Compare);
            var result = new List<ForestCanopyCluster>(sectors.Count);
            for (int i = 0; i < sectors.Count; i++)
                result.Add(BuildCluster(sectors[i], groups[sectors[i]]));
            return result;
        }

        private static ForestCanopyCluster BuildCluster(SectorKey sector, List<TreeVisibilityEntry> members)
        {
            members.Sort((left, right) => left.StableId.CompareTo(right.StableId));
            float minX = float.PositiveInfinity;
            float minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxZ = float.NegativeInfinity;
            float maxHeight = 0f;
            float heightSum = 0f;
            float healthSum = 0f;
            var ids = new ulong[members.Count];

            ulong revision = ClusterId(sector.X, sector.Z);
            for (int i = 0; i < members.Count; i++)
            {
                TreeVisibilityEntry member = members[i];
                TreeInstance tree = member.Instance;
                TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(tree.Species);
                float height = math.max(0.1f, profile.MidHeight * math.max(0.05f, tree.Scale));
                float canopyRadius = math.max(0.5f, height * math.max(0.12f, profile.BranchLengthFactor));

                ids[i] = member.StableId;
                minX = math.min(minX, tree.PositionMetres.x - canopyRadius);
                minZ = math.min(minZ, tree.PositionMetres.z - canopyRadius);
                maxX = math.max(maxX, tree.PositionMetres.x + canopyRadius);
                maxZ = math.max(maxZ, tree.PositionMetres.z + canopyRadius);
                maxHeight = math.max(maxHeight, height);
                heightSum += height;
                healthSum += math.saturate(member.Damage.FoliageHealth);
                revision = HashUlong(revision, member.StableId);
                revision = HashUlong(revision, member.PresentationRevision);
                // Preserve deterministic revision changes for manually-authored visibility fixtures
                // that predate PresentationRevision and therefore carry its default zero value.
                revision = HashInt(revision, QuantizeHealth(member.Damage.FoliageHealth));
            }

            float2 centreXZ = new float2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            return new ForestCanopyCluster(
                ClusterId(sector.X, sector.Z),
                sector.X,
                sector.Z,
                new float3(centreXZ.x, maxHeight * 0.5f, centreXZ.y),
                new float2((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f),
                maxHeight,
                heightSum / members.Count,
                healthSum / members.Count,
                revision,
                ids);
        }

        private static int QuantizeHealth(float value) => (int)math.round(math.saturate(value) * 4096f);

        private readonly struct SectorKey : IEquatable<SectorKey>
        {
            public SectorKey(int x, int z) { X = x; Z = z; }
            public int X { get; }
            public int Z { get; }
            public bool Equals(SectorKey other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is SectorKey other && Equals(other);
            public override int GetHashCode() => unchecked((X * 397) ^ Z);
            public static int Compare(SectorKey a, SectorKey b)
            {
                int x = a.X.CompareTo(b.X);
                return x != 0 ? x : a.Z.CompareTo(b.Z);
            }
        }

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong ClusterId(int x, int z)
        {
            ulong hash = HashInt(FnvOffset, x);
            return HashInt(hash, z);
        }

        private static ulong HashInt(ulong hash, int value) => HashUlong(hash, unchecked((uint)value));

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
