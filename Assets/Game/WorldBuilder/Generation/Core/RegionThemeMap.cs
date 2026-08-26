using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen
{
    /// <summary>
    /// Which theme applies where, as authored bands rather than as a noise field.
    ///
    /// A themed region has to be a decision someone made — "the forest is between the towns, on
    /// this side of the river" — because that is what the road, the bridge and the two settlements
    /// were also placed against. Deriving it from noise instead would put the pine forest somewhere
    /// different every seed and stop it being the same place the bridge crosses.
    /// </summary>
    public sealed class RegionThemeMap
    {
        private readonly struct Band
        {
            public readonly int FromZDm;
            public readonly int ToZDm;
            public readonly RegionThemeKind Theme;

            public Band(int fromZDm, int toZDm, RegionThemeKind theme)
            {
                FromZDm = fromZDm;
                ToZDm = toZDm;
                Theme = theme;
            }
        }

        private readonly List<Band> _bands = new List<Band>();
        private readonly RegionThemeKind _fallback;

        public RegionThemeMap(RegionThemeKind fallback) => _fallback = fallback;

        public RegionThemeMap WithBand(int fromZDm, int toZDm, RegionThemeKind theme)
        {
            if (toZDm <= fromZDm)
                throw new ArgumentException("Theme band must span a positive distance.");
            _bands.Add(new Band(fromZDm, toZDm, theme));
            return this;
        }

        public RegionThemeKind ThemeAt(int zDm)
        {
            for (int i = 0; i < _bands.Count; i++)
                if (zDm >= _bands[i].FromZDm && zDm < _bands[i].ToZDm)
                    return _bands[i].Theme;
            return _fallback;
        }

        public RegionThemeProfile ProfileAt(int zDm) => RegionThemeCatalog.Get(ThemeAt(zDm));

        /// <summary>
        /// The authored country of the Kentridge-Hightown slice: farmland around each town, a pine
        /// forest filling the middle, and a riverbank band at the crossing itself.
        /// </summary>
        public static RegionThemeMap ForKentridgeHightown(
            int kentridgeCentreZDm, int hightownCentreZDm, int crossingZDm)
        {
            int south = Math.Min(kentridgeCentreZDm, hightownCentreZDm);
            int north = Math.Max(kentridgeCentreZDm, hightownCentreZDm);

            const int RiverbankHalfDm = 260;

            return new RegionThemeMap(RegionThemeKind.TemperateFarmland)
                // The riverbank is listed first so it wins where it overlaps the forest.
                .WithBand(crossingZDm - RiverbankHalfDm, crossingZDm + RiverbankHalfDm,
                      RegionThemeKind.Riverbank)
                .WithBand(south + 900, north - 900, RegionThemeKind.PineForest)
                .WithBand(north - 900, north + 2000, RegionThemeKind.Highland);
        }
    }
}
