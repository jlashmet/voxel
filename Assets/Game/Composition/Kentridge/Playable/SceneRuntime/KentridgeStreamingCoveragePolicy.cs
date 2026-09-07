using System;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Scene-composition policy that converts ShowcaseWorld's discrete circular region residency
    /// into the continuous metric radius that the near-surface renderer may require to be complete.
    /// The renderer owns publication; this policy only prevents Kentridge from promising a larger
    /// fully resident metric disk than its configured region lattice can guarantee.
    /// </summary>
    public static class KentridgeStreamingCoveragePolicy
    {
        public static float GuaranteedNearSurfaceRadiusMetres(int loadRadiusRegions, float regionMetres)
        {
            if (loadRadiusRegions < 0)
                throw new ArgumentOutOfRangeException(nameof(loadRadiusRegions));
            if (!(regionMetres > 0f))
                throw new ArgumentOutOfRangeException(nameof(regionMetres));

            // ShowcaseWorld admits horizontal columns with dx^2 + dz^2 <= R^2. Because the
            // demand point may lie anywhere inside the centre region, the nearest excluded cell
            // can begin at (R - 1) full region widths from that point. Only that inset disk is
            // guaranteed resident for every within-cell demand position.
            return Math.Max(0, loadRadiusRegions - 1) * regionMetres;
        }
    }
}
