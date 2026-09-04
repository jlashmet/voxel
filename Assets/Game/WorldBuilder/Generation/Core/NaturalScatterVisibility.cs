using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen
{
    public enum NaturalScatterPresentationClass : byte
    {
        Ordinary = 0,
        Landmark = 1
    }

    /// <summary>
    /// Renderer-neutral semantic description of one authored/procedural natural-scatter placement.
    /// Coordinates and dimensions are integer decimetres so this record can exist before and without
    /// voxel realization. StableId is owned by the placement source; renderers must not derive a new
    /// identity from camera position.
    /// </summary>
    public readonly struct NaturalScatterDescriptor
    {
        public NaturalScatterDescriptor(
            ulong stableId,
            int archetypeId,
            int xDm,
            int yDm,
            int zDm,
            int radiusDm,
            int heightDm,
            NaturalScatterPresentationClass presentationClass)
        {
            if (stableId == 0UL) throw new ArgumentOutOfRangeException(nameof(stableId));
            if (radiusDm < 0) throw new ArgumentOutOfRangeException(nameof(radiusDm));
            if (heightDm <= 0) throw new ArgumentOutOfRangeException(nameof(heightDm));

            StableId = stableId;
            ArchetypeId = archetypeId;
            XDm = xDm;
            YDm = yDm;
            ZDm = zDm;
            RadiusDm = radiusDm;
            HeightDm = heightDm;
            PresentationClass = presentationClass;
        }

        public ulong StableId { get; }
        public int ArchetypeId { get; }
        public int XDm { get; }
        public int YDm { get; }
        public int ZDm { get; }
        public int RadiusDm { get; }
        public int HeightDm { get; }
        public NaturalScatterPresentationClass PresentationClass { get; }
        public bool IsLandmark => PresentationClass == NaturalScatterPresentationClass.Landmark;
    }

    public readonly struct NaturalScatterSectorBounds
    {
        public NaturalScatterSectorBounds(int minX, int minZ, int maxX, int maxZ)
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
    }

    public readonly struct NaturalScatterVisibilityEntry
    {
        internal NaturalScatterVisibilityEntry(
            int sourceIndex,
            int sectorX,
            int sectorZ,
            NaturalScatterDescriptor descriptor)
        {
            SourceIndex = sourceIndex;
            SectorX = sectorX;
            SectorZ = sectorZ;
            Descriptor = descriptor;
        }

        public int SourceIndex { get; }
        public int SectorX { get; }
        public int SectorZ { get; }
        public NaturalScatterDescriptor Descriptor { get; }
        public ulong StableId => Descriptor.StableId;
        public bool IsLandmark => Descriptor.IsLandmark;
    }

    /// <summary>
    /// Stateless sector projection over natural-scatter world facts. The caller owns placement and
    /// persistence. This query only exposes stable semantic records to downstream visibility/HLOD
    /// policy, so distant scatter never requires a voxel region to be generated or retained.
    /// </summary>
    public static class NaturalScatterVisibility
    {
        public static void Query(
            IReadOnlyList<NaturalScatterDescriptor> descriptors,
            int sectorSizeDm,
            in NaturalScatterSectorBounds sectors,
            List<NaturalScatterVisibilityEntry> output)
        {
            if (sectorSizeDm <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSizeDm));
            if (output == null) throw new ArgumentNullException(nameof(output));

            output.Clear();
            if (descriptors == null) return;

            for (int i = 0; i < descriptors.Count; i++)
            {
                NaturalScatterDescriptor descriptor = descriptors[i];
                int sectorX = FloorDiv(descriptor.XDm, sectorSizeDm);
                int sectorZ = FloorDiv(descriptor.ZDm, sectorSizeDm);
                if (!sectors.Contains(sectorX, sectorZ)) continue;

                output.Add(new NaturalScatterVisibilityEntry(i, sectorX, sectorZ, descriptor));
            }

            output.Sort(CompareStable);
        }

        private static int CompareStable(NaturalScatterVisibilityEntry left, NaturalScatterVisibilityEntry right)
        {
            int stable = left.StableId.CompareTo(right.StableId);
            if (stable != 0) return stable;
            return left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
