using System;

namespace MountingForce.WorldGen
{
    /// <summary>
    /// Exact realized world point in a backend-defined integer unit. UnitsPerDecimetre makes scale
    /// explicit so consumers can either convert losslessly or fail closed; no rounding contract is
    /// hidden in this DTO.
    /// </summary>
    public readonly struct RealizedWorldPoint
    {
        public Int3 Position { get; }
        public int UnitsPerDecimetre { get; }

        public RealizedWorldPoint(Int3 position, int unitsPerDecimetre)
        {
            if (unitsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitsPerDecimetre));
            Position = position;
            UnitsPerDecimetre = unitsPerDecimetre;
        }
    }

    /// <summary>
    /// Exact inclusive integer bounds in the same backend-defined world units used by realization.
    /// Inclusive min/max mirrors VoxelEngine primitive membership exactly: one-unit thickness is
    /// represented by MinInclusive == MaxInclusive rather than an empty half-open interval.
    /// </summary>
    public readonly struct RealizedWorldBounds
    {
        public Int3 MinInclusive { get; }
        public Int3 MaxInclusive { get; }
        public int UnitsPerDecimetre { get; }

        public RealizedWorldBounds(
            Int3 minInclusive,
            Int3 maxInclusive,
            int unitsPerDecimetre)
        {
            if (unitsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitsPerDecimetre));
            if (maxInclusive.X < minInclusive.X
                || maxInclusive.Y < minInclusive.Y
                || maxInclusive.Z < minInclusive.Z)
                throw new ArgumentException("Realized world bounds require max >= min on every axis.");

            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
            UnitsPerDecimetre = unitsPerDecimetre;
        }

        public RealizedWorldPoint CentreFloor()
        {
            return new RealizedWorldPoint(
                new Int3(
                    MinInclusive.X + (MaxInclusive.X - MinInclusive.X) / 2,
                    MinInclusive.Y,
                    MinInclusive.Z + (MaxInclusive.Z - MinInclusive.Z) / 2),
                UnitsPerDecimetre);
        }
    }

    /// <summary>
    /// Backend-neutral realized placement facts for stable semantic site roles. Core owns only the
    /// contract; terrain/voxel backends implement it after applying their exact placement rules.
    /// </summary>
    public interface ISettlementSiteRealizationFacts
    {
        bool TryGetPublicEntrance(int roleId, out RealizedWorldPoint entrance);
    }

    /// <summary>
    /// Exact physical lookup for generated hidden-space candidates. IDs are the stable correlation
    /// identifiers returned by SiteHiddenSpaceRealization; callers never reconstruct positions from IDs.
    /// </summary>
    public interface IHiddenSpaceRealizationFacts
    {
        bool TryGetCandidateBounds(string candidateId, out RealizedWorldBounds bounds);
        bool TryGetEntranceBounds(string entranceId, out RealizedWorldBounds bounds);
    }
}
