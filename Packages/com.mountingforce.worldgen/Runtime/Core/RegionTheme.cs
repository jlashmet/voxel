namespace MountingForce.WorldGen
{
    /// <summary>
    /// Named identity for a stretch of country. A theme is what a region <em>is</em> — the thing an
    /// author picks — and everything that grows or lives there is derived from it.
    ///
    /// This deliberately sits in Core rather than in the voxel backend: which trees belong in a
    /// pine forest is a worldbuilding decision, not a rendering one, and the same answer has to
    /// serve vegetation, wildlife and any later system that asks what a place is like.
    /// </summary>
    public enum RegionThemeKind : byte
    {
        /// <summary>Mixed farmland and hedgerow. The default settled country around Kentridge.</summary>
        TemperateFarmland = 0,

        /// <summary>Dense evergreen woodland: the forest between the two towns.</summary>
        PineForest = 1,

        /// <summary>Open high ground, sparser and rockier, around Hightown.</summary>
        Highland = 2,

        /// <summary>Wet ground along a watercourse.</summary>
        Riverbank = 3,
    }

    /// <summary>
    /// What a theme means, expressed as weights rather than as a fixed list so a region reads as a
    /// place with a dominant character rather than as one repeated asset.
    ///
    /// Weights are per-mille of the trees placed in the region. They are deliberately not
    /// normalized for the caller: a theme that sums to less than 1000 is leaving that share of its
    /// ground deliberately open, which is how a forest edge differs from a forest.
    /// </summary>
    public readonly struct RegionThemeProfile
    {
        public readonly RegionThemeKind Kind;

        /// <summary>Per-mille share of each tree species, indexed by <c>TreeSpeciesSlot</c>.</summary>
        public readonly int OakPerMille;
        public readonly int PinePerMille;
        public readonly int BirchPerMille;
        public readonly int MaplePerMille;
        public readonly int WillowPerMille;
        public readonly int DeadPerMille;

        /// <summary>Trees per hectare. A pine forest is dense; farmland is hedgerow and copse.</summary>
        public readonly int TreesPerHectare;

        /// <summary>0-1000. Ground cover density, and how much ambient life the region carries.</summary>
        public readonly int UndergrowthPerMille;
        public readonly int WildlifePerMille;

        public RegionThemeProfile(
            RegionThemeKind kind,
            int oak, int pine, int birch, int maple, int willow, int dead,
            int treesPerHectare, int undergrowthPerMille, int wildlifePerMille)
        {
            Kind = kind;
            OakPerMille = oak;
            PinePerMille = pine;
            BirchPerMille = birch;
            MaplePerMille = maple;
            WillowPerMille = willow;
            DeadPerMille = dead;
            TreesPerHectare = treesPerHectare;
            UndergrowthPerMille = undergrowthPerMille;
            WildlifePerMille = wildlifePerMille;
        }
    }

    public static class RegionThemeCatalog
    {
        public static RegionThemeProfile Get(RegionThemeKind kind)
        {
            switch (kind)
            {
                case RegionThemeKind.PineForest:
                    // Overwhelmingly pine, with just enough birch and standing deadwood that the
                    // stand reads as a wood rather than as a plantation.
                    return new RegionThemeProfile(
                        kind, oak: 40, pine: 810, birch: 90, maple: 0, willow: 0, dead: 60,
                        treesPerHectare: 220, undergrowthPerMille: 620, wildlifePerMille: 540);

                case RegionThemeKind.Highland:
                    return new RegionThemeProfile(
                        kind, oak: 90, pine: 470, birch: 330, maple: 30, willow: 0, dead: 80,
                        treesPerHectare: 45, undergrowthPerMille: 300, wildlifePerMille: 260);

                case RegionThemeKind.Riverbank:
                    return new RegionThemeProfile(
                        kind, oak: 150, pine: 60, birch: 250, maple: 60, willow: 460, dead: 20,
                        treesPerHectare: 130, undergrowthPerMille: 780, wildlifePerMille: 700);

                case RegionThemeKind.TemperateFarmland:
                default:
                    return new RegionThemeProfile(
                        RegionThemeKind.TemperateFarmland,
                        oak: 430, pine: 90, birch: 210, maple: 230, willow: 20, dead: 20,
                        treesPerHectare: 35, undergrowthPerMille: 520, wildlifePerMille: 430);
            }
        }

        /// <summary>
        /// Chooses a species for one tree from a roll in [0, 1000). Returns the slot index rather
        /// than a rendering enum so Core keeps no dependency on the vegetation runtime.
        /// </summary>
        public static TreeSpeciesSlot SpeciesFor(in RegionThemeProfile profile, int rollPerMille)
        {
            int cursor = profile.OakPerMille;
            if (rollPerMille < cursor) return TreeSpeciesSlot.Oak;
            cursor += profile.PinePerMille;
            if (rollPerMille < cursor) return TreeSpeciesSlot.Pine;
            cursor += profile.BirchPerMille;
            if (rollPerMille < cursor) return TreeSpeciesSlot.Birch;
            cursor += profile.MaplePerMille;
            if (rollPerMille < cursor) return TreeSpeciesSlot.Maple;
            cursor += profile.WillowPerMille;
            if (rollPerMille < cursor) return TreeSpeciesSlot.Willow;
            cursor += profile.DeadPerMille;
            if (rollPerMille < cursor) return TreeSpeciesSlot.Dead;
            return TreeSpeciesSlot.None;
        }
    }

    /// <summary>
    /// Core's own species vocabulary. The renderer has an equivalent enum; keeping a separate one
    /// here is what lets worldbuilding decide what grows somewhere without Core depending on the
    /// vegetation renderer.
    /// </summary>
    public enum TreeSpeciesSlot : byte
    {
        None = 0,
        Oak = 1,
        Pine = 2,
        Birch = 3,
        Maple = 4,
        Willow = 5,
        Dead = 6,
    }
}
