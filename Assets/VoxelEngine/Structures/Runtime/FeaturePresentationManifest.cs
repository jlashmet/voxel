using System;
using System.Collections.Generic;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Deterministic sparse spatial index over derived feature-presentation bakes.
    /// Horizontal sector membership keeps queries bounded while full 3D bake bounds remain the
    /// final intersection test. The manifest owns no world truth and may be rebuilt at any time.
    /// </summary>
    public sealed class FeaturePresentationManifest : IFeaturePresentationSource
    {
        public const int DefaultSectorSizeVoxels = 2560;

        private readonly int _sectorSize;
        private readonly Dictionary<ulong, Entry> _entries = new Dictionary<ulong, Entry>();
        private readonly Dictionary<Sector, List<ulong>> _sectorMembers = new Dictionary<Sector, List<ulong>>();

        public FeaturePresentationManifest(int sectorSizeVoxels = DefaultSectorSizeVoxels)
        {
            if (sectorSizeVoxels <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSizeVoxels));
            _sectorSize = sectorSizeVoxels;
        }

        public int Count => _entries.Count;

        public void Upsert(FeaturePresentationBake bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));

            if (_entries.TryGetValue(bake.SourceId, out Entry previous))
                RemoveMembership(bake.SourceId, previous.Sectors);

            Sector[] sectors = ResolveSectors(bake);
            _entries[bake.SourceId] = new Entry(bake, sectors);
            for (int i = 0; i < sectors.Length; i++)
            {
                if (!_sectorMembers.TryGetValue(sectors[i], out List<ulong> members))
                {
                    members = new List<ulong>();
                    _sectorMembers.Add(sectors[i], members);
                }
                members.Add(bake.SourceId);
            }
        }

        public bool Remove(ulong sourceId)
        {
            if (!_entries.TryGetValue(sourceId, out Entry entry)) return false;
            RemoveMembership(sourceId, entry.Sectors);
            return _entries.Remove(sourceId);
        }

        public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
        {
            if (_entries.TryGetValue(sourceId, out Entry entry))
            {
                bake = entry.Bake;
                return true;
            }
            bake = null;
            return false;
        }

        public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds)
        {
            int minSectorX = FloorDiv(bounds.Min.x, _sectorSize);
            int minSectorZ = FloorDiv(bounds.Min.z, _sectorSize);
            int maxSectorX = FloorDiv(bounds.Max.x - 1, _sectorSize);
            int maxSectorZ = FloorDiv(bounds.Max.z - 1, _sectorSize);

            var keys = new HashSet<ulong>();
            for (int z = minSectorZ; z <= maxSectorZ; z++)
            for (int x = minSectorX; x <= maxSectorX; x++)
            {
                if (!_sectorMembers.TryGetValue(new Sector(x, z), out List<ulong> members)) continue;
                for (int i = 0; i < members.Count; i++) keys.Add(members[i]);
            }

            var orderedKeys = new List<ulong>(keys);
            orderedKeys.Sort();
            var result = new List<FeaturePresentationBake>(orderedKeys.Count);
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                FeaturePresentationBake bake = _entries[orderedKeys[i]].Bake;
                if (bounds.Intersects(bake)) result.Add(bake);
            }
            return result;
        }

        private Sector[] ResolveSectors(FeaturePresentationBake bake)
        {
            int minX = FloorDiv(bake.BoundsMin.x, _sectorSize);
            int minZ = FloorDiv(bake.BoundsMin.z, _sectorSize);
            int maxX = FloorDiv(bake.BoundsMax.x, _sectorSize);
            int maxZ = FloorDiv(bake.BoundsMax.z, _sectorSize);
            var sectors = new Sector[(maxX - minX + 1) * (maxZ - minZ + 1)];
            int index = 0;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
                sectors[index++] = new Sector(x, z);
            return sectors;
        }

        private void RemoveMembership(ulong sourceId, Sector[] sectors)
        {
            for (int i = 0; i < sectors.Length; i++)
            {
                if (!_sectorMembers.TryGetValue(sectors[i], out List<ulong> members)) continue;
                members.Remove(sourceId);
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
            public Entry(FeaturePresentationBake bake, Sector[] sectors)
            {
                Bake = bake;
                Sectors = sectors;
            }

            public FeaturePresentationBake Bake { get; }
            public Sector[] Sectors { get; }
        }

        private readonly struct Sector : IEquatable<Sector>
        {
            public Sector(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }
            public int Z { get; }
            public bool Equals(Sector other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is Sector other && Equals(other);
            public override int GetHashCode() => unchecked((X * 397) ^ Z);
        }
    }
}
