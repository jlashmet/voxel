using System;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Compact authored solid-boundary sample. Zero means occupancy-only reconstruction; all
    /// other values encode signed distance in eighths of a voxel plus an optional extrusion axis,
    /// positive inside solid. Profile constraints apply perpendicular to that axis; depth faces
    /// remain occupancy-planar so sharp profile/extrusion intersections are not rounded.
    /// Keeping this independent from surface semantics prevents materials and shading styles from
    /// inventing geometry that the feature authoring stage did not preserve.
    /// </summary>
    public struct VoxelBoundarySample : IEquatable<VoxelBoundarySample>
    {
        public byte Packed;

        public bool IsAuthored => Packed != 0;
        public int SignedQ3 => IsAuthored ? (Packed & 0x3F) - 32 : 0;
        public int SignedQ4 => SignedQ3 * 2;
        public int ExtrusionAxis
        {
            get
            {
                int code = Packed >> 6;
                return code == 0 ? 3 : code - 1;
            }
        }

        public bool AppliesAlong(int edgeAxis) =>
            IsAuthored && (ExtrusionAxis == 3 || edgeAxis != ExtrusionAxis);

        public static VoxelBoundarySample FromSignedQ4(int signedQ4, int extrusionAxis = 3)
        {
            int signedQ3 = signedQ4 >= 0 ? (signedQ4 + 1) / 2 : (signedQ4 - 1) / 2;
            signedQ3 = Math.Clamp(signedQ3, -31, 31);
            int axisCode = extrusionAxis >= 0 && extrusionAxis <= 2 ? extrusionAxis + 1 : 0;
            return new VoxelBoundarySample
            {
                Packed = (byte)((axisCode << 6) | (signedQ3 + 32))
            };
        }

        public bool Equals(VoxelBoundarySample other) => Packed == other.Packed;
        public override bool Equals(object obj) => obj is VoxelBoundarySample other && Equals(other);
        public override int GetHashCode() => Packed;
    }

    /// <summary>Stable built-in surface-style identifiers used by compiled feature data.</summary>
    public static class SurfaceStyles
    {
        public const ushort MaterialDefault = 0;
        public const ushort Smooth = 1;
        public const ushort Planar = 2;
        public const ushort Rounded = 3;
        public const ushort Sharp = 4;
        public const ushort MasonryJoint = 5;
        public const ushort Beveled = 6;
        public const ushort Cubic = 7;
    }

    /// <summary>Stable built-in coating identifiers. Coatings never replace base material.</summary>
    public static class Coatings
    {
        public const byte None = 0;
        public const byte Moss = 1;
        public const byte Snow = 2;
        public const byte Soot = 3;
        public const byte Wet = 4;
        public const byte Fire = 5;
    }

    [Flags]
    public enum VoxelSurfaceFlags : byte
    {
        None = 0,
        PreserveFeature = 1 << 0,
        IntentionalSeam = 1 << 1,
    }

    /// <summary>
    /// Reconstruction and coating state stored independently from a voxel's base material.
    /// Zero is the compact default: use the base material's default style with no coating.
    /// </summary>
    public struct VoxelSurfaceSemantics : IEquatable<VoxelSurfaceSemantics>
    {
        public ushort StyleId;
        public byte CoatingId;
        public VoxelSurfaceFlags Flags;
        /// <summary>
        /// Generic authored detail code. Zero is neutral, 1-15 is a signed piece-variation
        /// channel, and 16-31 is continuous high detail; presentation remains style-defined.
        /// </summary>
        public byte Detail;

        public static VoxelSurfaceSemantics Default => default;

        public uint Packed => StyleId | ((uint)CoatingId << 16)
            | ((uint)(((byte)Flags & 0x07) | ((Detail & 0x1F) << 3)) << 24);

        /// <summary>Compact layout: style 5, coating 4, flags 2, detail 5 bits.</summary>
        public ushort PackedStorage => (ushort)((StyleId & 0x1F)
            | ((CoatingId & 0x0F) << 5) | (((byte)Flags & 0x03) << 9)
            | ((Detail & 0x1F) << 11));

        public static VoxelSurfaceSemantics FromPacked(uint packed) => new()
        {
            StyleId = (ushort)packed,
            CoatingId = (byte)(packed >> 16),
            Flags = (VoxelSurfaceFlags)((packed >> 24) & 0x07),
            Detail = (byte)((packed >> 27) & 0x1F)
        };

        public static VoxelSurfaceSemantics FromStorage(ushort packed) => new()
        {
            StyleId = (byte)(packed & 0x1F),
            CoatingId = (byte)((packed >> 5) & 0x0F),
            Flags = (VoxelSurfaceFlags)((packed >> 9) & 0x03),
            Detail = (byte)((packed >> 11) & 0x1F)
        };

        public bool Equals(VoxelSurfaceSemantics other) => Packed == other.Packed;
        public override bool Equals(object obj) => obj is VoxelSurfaceSemantics other && Equals(other);
        public override int GetHashCode() => (int)Packed;
    }

    /// <summary>
    /// Logical authoritative voxel value. Base material owns simulation; surface semantics own
    /// reconstruction and presentation overlays.
    /// </summary>
    public struct VoxelCell : IEquatable<VoxelCell>
    {
        public byte BaseMaterialId;
        public VoxelSurfaceSemantics Surface;
        public VoxelBoundarySample Boundary;

        public bool IsSolid => BaseMaterialId != VoxelGrid.MaterialEmpty;

        public bool Equals(VoxelCell other) =>
            BaseMaterialId == other.BaseMaterialId && Surface.Equals(other.Surface)
            && Boundary.Equals(other.Boundary);

        public override bool Equals(object obj) => obj is VoxelCell other && Equals(other);
        public override int GetHashCode() =>
            ((BaseMaterialId * 397) ^ Surface.GetHashCode()) * 397 ^ Boundary.GetHashCode();
    }
}
