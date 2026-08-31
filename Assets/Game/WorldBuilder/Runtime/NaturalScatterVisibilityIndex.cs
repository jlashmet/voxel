using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;

namespace Game.WorldBuilder.Runtime
{
    public enum NaturalScatterKind : byte
    {
        Boulder = 0,
        RockSpire = 1,
        FallenLog = 2,
        NaturalArch = 3,
    }

    public enum NaturalScatterImportance : byte
    {
        Ordinary = 0,
        Landmark = 1,
        HorizonLandmark = 2,
    }

    public readonly struct NaturalScatterRecord : IEquatable<NaturalScatterRecord>
    {
        public NaturalScatterRecord(
            ulong stableId,
            Int2 positionDm,
            int radiusDm,
            int heightDm,
            NaturalScatterKind kind,
            NaturalScatterImportance importance,
            ulong revision)
        {
            if (radiusDm <= 0) throw new ArgumentOutOfRangeException(nameof(radiusDm));
            if (heightDm <= 0) throw new ArgumentOutOfRangeException(nameof(heightDm));
            StableId = stableId;
            PositionDm = positionDm;
            RadiusDm = radiusDm;
            HeightDm = heightDm;
            Kind = kind;
            Importance = importance;
            Revision = revision;
        }

        public ulong StableId { get; }
        public Int2 PositionDm { get; }
        public int RadiusDm { get; }
        public int HeightDm { get; }
        public NaturalScatterKind Kind { get; }
        public NaturalScatterImportance Importance { get; }
        public ulong Revision { get; }

        public bool Equals(NaturalScatterRecord other) =>
            StableId == other.StableId && PositionDm.X == other.PositionDm.X
            && PositionDm.Y == other.PositionDm.Y && RadiusDm == other.RadiusDm
            && HeightDm == other.HeightDm && Kind == other.Kind
            && Importance == other.Importance && Revision == other.Revision;
        public override bool Equals(object obj) => obj is NaturalScatterRecord other && Equals(other);
        public override int GetHashCode() => unchecked((int)(StableId ^ (StableId >> 32)));
    }

    /// <summary>
    /// Renderer-neutral deterministic natural-scatter records. Ordinary boulders can be regenerated
    /// from world seed + fixed sector; exceptional features are explicit records with landmark
    /// importance. Neither path depends on camera position, voxel residency, or render output.
    /// </summary>
    public static class NaturalScatterVisibilityIndex
    {
        public static IReadOnlyList<NaturalScatterRecord> GenerateOrdinaryBoulders(
            uint worldSeed,
            int sectorX,
            int sectorZ,
            int sectorSizeDm,
            int count)
        {
            if (sectorSizeDm <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSizeDm));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            var records = new List<NaturalScatterRecord>(count);
            ulong sectorKey = SectorKey(worldSeed, sectorX, sectorZ);
            for (int i = 0; i < count; i++)
            {
                ulong state = Mix(sectorKey, unchecked((ulong)(uint)i + 1UL));
                int localX = (int)(state % (uint)sectorSizeDm);
                state = Mix(state, 0x9E3779B97F4A7C15UL);
                int localZ = (int)(state % (uint)sectorSizeDm);
                state = Mix(state, 0xD1B54A32D192ED03UL);
                int radiusDm = 4 + (int)(state % 17UL);
                state = Mix(state, 0x94D049BB133111EBUL);
                int heightDm = 3 + (int)(state % 25UL);
                ulong stableId = Mix(sectorKey, unchecked((ulong)(uint)i));
                ulong revision = Mix(stableId, ((ulong)(uint)radiusDm << 32) | (uint)heightDm);

                records.Add(new NaturalScatterRecord(
                    stableId,
                    new Int2(
                        checked(sectorX * sectorSizeDm + localX),
                        checked(sectorZ * sectorSizeDm + localZ)),
                    radiusDm,
                    heightDm,
                    NaturalScatterKind.Boulder,
                    NaturalScatterImportance.Ordinary,
                    revision));
            }
            records.Sort((a, b) => a.StableId.CompareTo(b.StableId));
            return records;
        }

        public static IReadOnlyList<NaturalScatterRecord> Query(
            IReadOnlyList<NaturalScatterRecord> records,
            int sectorSizeDm,
            int minSectorX,
            int minSectorZ,
            int maxSectorX,
            int maxSectorZ)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            if (sectorSizeDm <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSizeDm));
            if (maxSectorX < minSectorX || maxSectorZ < minSectorZ)
                throw new ArgumentOutOfRangeException(nameof(maxSectorX));

            var result = new List<NaturalScatterRecord>();
            for (int i = 0; i < records.Count; i++)
            {
                NaturalScatterRecord record = records[i];
                int x = FloorDiv(record.PositionDm.X, sectorSizeDm);
                int z = FloorDiv(record.PositionDm.Y, sectorSizeDm);
                if (x < minSectorX || x > maxSectorX || z < minSectorZ || z > maxSectorZ)
                    continue;
                result.Add(record);
            }
            result.Sort((a, b) => a.StableId.CompareTo(b.StableId));
            return result;
        }

        public static ulong SectorKey(uint worldSeed, int sectorX, int sectorZ)
        {
            ulong hash = 14695981039346656037UL;
            hash = HashUInt(hash, worldSeed);
            hash = HashUInt(hash, unchecked((uint)sectorX));
            hash = HashUInt(hash, unchecked((uint)sectorZ));
            return hash;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && value < 0) quotient--;
            return quotient;
        }

        private static ulong Mix(ulong value, ulong salt)
        {
            ulong x = value ^ salt;
            x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27; x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x == 0UL ? 1UL : x;
        }

        private static ulong HashUInt(ulong hash, uint value)
        {
            const ulong prime = 1099511628211UL;
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= prime;
            }
            return hash;
        }
    }
}
