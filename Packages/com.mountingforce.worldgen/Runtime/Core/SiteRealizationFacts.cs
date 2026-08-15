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
    /// Backend-neutral realized placement facts for stable semantic site roles. Core owns only the
    /// contract; terrain/voxel backends implement it after applying their exact placement rules.
    /// </summary>
    public interface ISettlementSiteRealizationFacts
    {
        bool TryGetPublicEntrance(int roleId, out RealizedWorldPoint entrance);
    }
}
