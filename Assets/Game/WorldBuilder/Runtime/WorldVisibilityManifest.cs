using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Architecture;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Deterministic spatial index of renderer-neutral semantic far-presentation records.
    /// This class owns descriptors and sector membership only; it has no voxel/storage/render hooks.
    /// </summary>
    public sealed class WorldVisibilityManifest : IWorldVisibilitySource
    {
        public const int DefaultSectorSizeDm = 2560;

        private readonly int _sectorSizeDm;
        private readonly Dictionary<ulong, Entry> _entries = new Dictionary<ulong, Entry>();
        private readonly Dictionary<Sector, List<ulong>> _sectorMembers =
            new Dictionary<Sector, List<ulong>>();

        public WorldVisibilityManifest(int sectorSizeDm = DefaultSectorSizeDm)
        {
            if (sectorSizeDm <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSizeDm));
            _sectorSizeDm = sectorSizeDm;
        }

        public int Count => _entries.Count;

        public void Upsert(StructureFarPresentation value)
        {
            Entry previous;
            if (_entries.TryGetValue(value.StructureKey, out previous))
                RemoveMembership(value.StructureKey, previous.Sectors);

            Sector[] sectors = ResolveSectors(value);
            _entries[value.StructureKey] = new Entry(value, sectors);
            for (int i = 0; i < sectors.Length; i++)
            {
                List<ulong> members;
                if (!_sectorMembers.TryGetValue(sectors[i], out members))
                {
                    members = new List<ulong>();
                    _sectorMembers.Add(sectors[i], members);
                }
                members.Add(value.StructureKey);
            }
        }

        public bool Remove(ulong structureKey)
        {
            Entry entry;
            if (!_entries.TryGetValue(structureKey, out entry)) return false;
            RemoveMembership(structureKey, entry.Sectors);
            return _entries.Remove(structureKey);
        }

        public bool TryGet(ulong structureKey, out StructureFarPresentation value)
        {
            Entry entry;
            if (_entries.TryGetValue(structureKey, out entry))
            {
                value = entry.Value;
                return true;
            }
            value = default(StructureFarPresentation);
            return false;
        }

        public IReadOnlyList<StructureFarPresentation> Query(WorldVisibilityBoundsDm bounds)
        {
            int minSectorX = FloorDiv(bounds.MinX, _sectorSizeDm);
            int minSectorY = FloorDiv(bounds.MinY, _sectorSizeDm);
            int maxSectorX = FloorDiv(bounds.MaxX - 1, _sectorSizeDm);
            int maxSectorY = FloorDiv(bounds.MaxY - 1, _sectorSizeDm);

            var keys = new HashSet<ulong>();
            for (int y = minSectorY; y <= maxSectorY; y++)
            for (int x = minSectorX; x <= maxSectorX; x++)
            {
                List<ulong> members;
                if (!_sectorMembers.TryGetValue(new Sector(x, y), out members)) continue;
                for (int i = 0; i < members.Count; i++) keys.Add(members[i]);
            }

            var orderedKeys = new List<ulong>(keys);
            orderedKeys.Sort();
            var result = new List<StructureFarPresentation>(orderedKeys.Count);
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                Entry entry = _entries[orderedKeys[i]];
                if (bounds.Intersects(entry.Value)) result.Add(entry.Value);
            }
            return result;
        }

        private Sector[] ResolveSectors(StructureFarPresentation value)
        {
            int minX = FloorDiv(value.FootprintMinDm.X, _sectorSizeDm);
            int minY = FloorDiv(value.FootprintMinDm.Y, _sectorSizeDm);
            int maxX = FloorDiv(value.FootprintMaxDm.X - 1, _sectorSizeDm);
            int maxY = FloorDiv(value.FootprintMaxDm.Y - 1, _sectorSizeDm);
            var sectors = new Sector[(maxX - minX + 1) * (maxY - minY + 1)];
            int index = 0;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                sectors[index++] = new Sector(x, y);
            return sectors;
        }

        private void RemoveMembership(ulong structureKey, Sector[] sectors)
        {
            for (int i = 0; i < sectors.Length; i++)
            {
                List<ulong> members;
                if (!_sectorMembers.TryGetValue(sectors[i], out members)) continue;
                members.Remove(structureKey);
                if (members.Count == 0) _sectorMembers.Remove(sectors[i]);
            }
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private readonly struct Entry
        {
            public readonly StructureFarPresentation Value;
            public readonly Sector[] Sectors;

            public Entry(StructureFarPresentation value, Sector[] sectors)
            {
                Value = value;
                Sectors = sectors;
            }
        }

        private readonly struct Sector : IEquatable<Sector>
        {
            public readonly int X;
            public readonly int Y;

            public Sector(int x, int y)
            {
                X = x;
                Y = y;
            }

            public bool Equals(Sector other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is Sector other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (X * 397) ^ Y; }
            }
        }
    }
}
