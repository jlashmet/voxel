using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Deterministic coarse settlement massing derived from semantic far-structure records.
    /// Ordinary structures participate in clusters; semantic anchors and landmarks remain
    /// independently addressable and are deliberately excluded from cluster membership.
    /// </summary>
    public static class WorldVisibilityClusterBuilder
    {
        public readonly struct Cluster
        {
            private readonly ulong[] _memberStructureKeys;

            internal Cluster(
                ulong clusterKey,
                ulong settlementKey,
                int sectorX,
                int sectorZ,
                int sectorSizeDm,
                Int2 footprintMinDm,
                Int2 footprintMaxDm,
                int maxHeightDm,
                int meanHeightDm,
                ulong dominantMaterialFamilyKey,
                ulong revision,
                ulong[] memberStructureKeys)
            {
                ClusterKey = clusterKey;
                SettlementKey = settlementKey;
                SectorX = sectorX;
                SectorZ = sectorZ;
                SectorSizeDm = sectorSizeDm;
                FootprintMinDm = footprintMinDm;
                FootprintMaxDm = footprintMaxDm;
                MaxHeightDm = maxHeightDm;
                MeanHeightDm = meanHeightDm;
                DominantMaterialFamilyKey = dominantMaterialFamilyKey;
                Revision = revision;
                _memberStructureKeys = memberStructureKeys ?? Array.Empty<ulong>();
            }

            public ulong ClusterKey { get; }
            public ulong SettlementKey { get; }
            public int SectorX { get; }
            public int SectorZ { get; }
            public int SectorSizeDm { get; }
            public Int2 FootprintMinDm { get; }
            public Int2 FootprintMaxDm { get; }
            public int MaxHeightDm { get; }
            public int MeanHeightDm { get; }
            public ulong DominantMaterialFamilyKey { get; }
            public ulong Revision { get; }
            public IReadOnlyList<ulong> MemberStructureKeys => _memberStructureKeys;
            public int MemberCount => _memberStructureKeys.Length;
        }

        public static IReadOnlyList<Cluster> Build(
            IReadOnlyList<StructureFarPresentation> structures,
            int sectorSizeDm)
        {
            if (structures == null) throw new ArgumentNullException(nameof(structures));
            if (sectorSizeDm <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSizeDm));

            var ordered = new List<StructureFarPresentation>(structures.Count);
            for (int i = 0; i < structures.Count; i++)
            {
                StructureFarPresentation structure = structures[i];
                if (structure.VisibilityClass != StructureVisibilityClass.OrdinaryStructure)
                    continue;
                ordered.Add(structure);
            }
            ordered.Sort((left, right) => left.StructureKey.CompareTo(right.StructureKey));

            var groups = new Dictionary<GroupKey, List<StructureFarPresentation>>();
            for (int i = 0; i < ordered.Count; i++)
            {
                StructureFarPresentation structure = ordered[i];
                long centerX = ((long)structure.FootprintMinDm.X + structure.FootprintMaxDm.X) / 2L;
                long centerZ = ((long)structure.FootprintMinDm.Y + structure.FootprintMaxDm.Y) / 2L;
                int sectorX = checked((int)FloorDiv(centerX, sectorSizeDm));
                int sectorZ = checked((int)FloorDiv(centerZ, sectorSizeDm));
                var key = new GroupKey(structure.SettlementKey, sectorX, sectorZ);
                if (!groups.TryGetValue(key, out List<StructureFarPresentation> members))
                {
                    members = new List<StructureFarPresentation>();
                    groups.Add(key, members);
                }
                members.Add(structure);
            }

            var groupKeys = new List<GroupKey>(groups.Keys);
            groupKeys.Sort(GroupKey.Compare);
            var clusters = new List<Cluster>(groupKeys.Count);
            for (int i = 0; i < groupKeys.Count; i++)
                clusters.Add(BuildCluster(groupKeys[i], groups[groupKeys[i]], sectorSizeDm));
            return clusters;
        }

        private static Cluster BuildCluster(
            GroupKey group,
            List<StructureFarPresentation> members,
            int sectorSizeDm)
        {
            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;
            int maxHeight = 0;
            long totalHeight = 0;
            var materialCounts = new Dictionary<ulong, int>();
            var memberKeys = new ulong[members.Count];

            ulong revision = FnvOffset;
            revision = HashUlong(revision, group.SettlementKey);
            revision = HashInt(revision, group.SectorX);
            revision = HashInt(revision, group.SectorZ);
            revision = HashInt(revision, sectorSizeDm);

            for (int i = 0; i < members.Count; i++)
            {
                StructureFarPresentation member = members[i];
                memberKeys[i] = member.StructureKey;
                minX = Math.Min(minX, member.FootprintMinDm.X);
                minZ = Math.Min(minZ, member.FootprintMinDm.Y);
                maxX = Math.Max(maxX, member.FootprintMaxDm.X);
                maxZ = Math.Max(maxZ, member.FootprintMaxDm.Y);
                maxHeight = Math.Max(maxHeight, member.HeightDm);
                totalHeight += member.HeightDm;

                materialCounts.TryGetValue(member.MaterialFamilyKey, out int materialCount);
                materialCounts[member.MaterialFamilyKey] = materialCount + 1;
                revision = HashUlong(revision, member.StructureKey);
                revision = HashUlong(revision, member.Revision);
            }

            ulong dominantMaterial = 0UL;
            int dominantCount = -1;
            foreach (KeyValuePair<ulong, int> pair in materialCounts)
            {
                if (pair.Value > dominantCount
                    || (pair.Value == dominantCount && pair.Key < dominantMaterial))
                {
                    dominantMaterial = pair.Key;
                    dominantCount = pair.Value;
                }
            }

            ulong clusterKey = FnvOffset;
            clusterKey = HashUlong(clusterKey, group.SettlementKey);
            clusterKey = HashInt(clusterKey, group.SectorX);
            clusterKey = HashInt(clusterKey, group.SectorZ);
            clusterKey = HashInt(clusterKey, sectorSizeDm);

            return new Cluster(
                clusterKey,
                group.SettlementKey,
                group.SectorX,
                group.SectorZ,
                sectorSizeDm,
                new Int2(minX, minZ),
                new Int2(maxX, maxZ),
                maxHeight,
                checked((int)(totalHeight / members.Count)),
                dominantMaterial,
                revision,
                memberKeys);
        }

        private static long FloorDiv(long value, int divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            if (remainder != 0 && value < 0) quotient--;
            return quotient;
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public GroupKey(ulong settlementKey, int sectorX, int sectorZ)
            {
                SettlementKey = settlementKey;
                SectorX = sectorX;
                SectorZ = sectorZ;
            }

            public ulong SettlementKey { get; }
            public int SectorX { get; }
            public int SectorZ { get; }

            public bool Equals(GroupKey other) =>
                SettlementKey == other.SettlementKey
                && SectorX == other.SectorX
                && SectorZ == other.SectorZ;

            public override bool Equals(object obj) => obj is GroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)(SettlementKey ^ (SettlementKey >> 32));
                    hash = hash * 397 ^ SectorX;
                    hash = hash * 397 ^ SectorZ;
                    return hash;
                }
            }

            public static int Compare(GroupKey left, GroupKey right)
            {
                int settlement = left.SettlementKey.CompareTo(right.SettlementKey);
                if (settlement != 0) return settlement;
                int x = left.SectorX.CompareTo(right.SectorX);
                return x != 0 ? x : left.SectorZ.CompareTo(right.SectorZ);
            }
        }

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong HashInt(ulong hash, int value) =>
            HashUlong(hash, unchecked((uint)value));

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
