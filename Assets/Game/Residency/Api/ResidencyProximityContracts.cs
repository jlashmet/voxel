using System;

namespace Game.Residency.Api
{
    /// <summary>Integer-metre semantic proximity hysteresis. Explicit non-proximity demands bypass it through normal max aggregation.</summary>
    public readonly struct ResidencyProximityPolicy
    {
        public int DetailedEnterMetres { get; }
        public int DetailedExitMetres { get; }
        public int CoarseEnterMetres { get; }
        public int CoarseExitMetres { get; }

        public ResidencyProximityPolicy(
            int detailedEnterMetres,
            int detailedExitMetres,
            int coarseEnterMetres,
            int coarseExitMetres)
        {
            if (detailedEnterMetres < 0) throw new ArgumentOutOfRangeException(nameof(detailedEnterMetres));
            if (detailedExitMetres <= detailedEnterMetres) throw new ArgumentOutOfRangeException(nameof(detailedExitMetres));
            if (coarseEnterMetres < detailedExitMetres) throw new ArgumentOutOfRangeException(nameof(coarseEnterMetres));
            if (coarseExitMetres <= coarseEnterMetres) throw new ArgumentOutOfRangeException(nameof(coarseExitMetres));
            DetailedEnterMetres = detailedEnterMetres;
            DetailedExitMetres = detailedExitMetres;
            CoarseEnterMetres = coarseEnterMetres;
            CoarseExitMetres = coarseExitMetres;
        }

        public ResidencyFidelity Select(ResidencyFidelity currentProximityFidelity, int distanceMetres)
        {
            if (distanceMetres < 0) throw new ArgumentOutOfRangeException(nameof(distanceMetres));
            switch (currentProximityFidelity)
            {
                case ResidencyFidelity.Detailed:
                    if (distanceMetres <= DetailedExitMetres) return ResidencyFidelity.Detailed;
                    return distanceMetres <= CoarseExitMetres ? ResidencyFidelity.Coarse : ResidencyFidelity.Dormant;
                case ResidencyFidelity.Coarse:
                    if (distanceMetres <= DetailedEnterMetres) return ResidencyFidelity.Detailed;
                    return distanceMetres <= CoarseExitMetres ? ResidencyFidelity.Coarse : ResidencyFidelity.Dormant;
                case ResidencyFidelity.Dormant:
                    if (distanceMetres <= DetailedEnterMetres) return ResidencyFidelity.Detailed;
                    return distanceMetres <= CoarseEnterMetres ? ResidencyFidelity.Coarse : ResidencyFidelity.Dormant;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentProximityFidelity), currentProximityFidelity, null);
            }
        }
    }
}
