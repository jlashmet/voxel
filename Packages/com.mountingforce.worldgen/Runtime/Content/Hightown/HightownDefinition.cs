namespace MountingForce.WorldGen.Content.Hightown
{
    /// <summary>
    /// Authored identity for Hightown, the second recovered MountingForce settlement.
    ///
    /// Unlike Kentridge, Hightown has no recovered per-building role list yet, so its plots are
    /// generated rather than authored one by one. What is authored is its <em>identity</em>: where
    /// it sits relative to Kentridge, and a theme that has to read as a different town from a
    /// distance, not merely a reshuffle of the same houses.
    /// </summary>
    public static class HightownDefinition
    {
        public const string Id = "hightown";

        /// <summary>
        /// Hightown's centre, in decimetres, in the same world space as
        /// <see cref="Content.Kentridge.KentridgeDefinition.TownCentreDm"/> at (1170, 520).
        ///
        /// Roughly 400 m north of Kentridge: far enough that the two towns are separate places with
        /// open country, a river and a forest between them, and close enough that the road between
        /// them is a journey rather than an expedition.
        /// </summary>
        public static readonly Int2 TownCentreDm = new Int2(1170, 4520);

        /// <summary>
        /// Deliberately contrasted with Kentridge.
        ///
        /// Kentridge is masonry over stone footings with timber framing and clay roof tile —
        /// low, warm and rural. Hightown is pale ashlar with dark stone accents under slate, with
        /// slightly taller storeys: the same generators, reading as a wealthier stone town rather
        /// than as Kentridge with the plots moved.
        ///
        /// The vertical proportions match Kentridge's exactly, and that is a constraint rather
        /// than a choice: the buildings share Kentridge's plot envelopes, and the architecture pass
        /// rejects anything that grows out of the envelope it was handed. Giving Hightown taller
        /// storeys — the thing that would distinguish the two silhouettes at range — needs it to
        /// own its envelope table, which is the same piece of work as generalising the town pass.
        /// Until then the distinction is material, layout and building mix, which is what reads
        /// close up and from the road.
        /// </summary>
        public static ArchitectureTheme Theme => new ArchitectureTheme(
            id: Id,
            foundation: MaterialRole.DarkMasonry,
            wall: MaterialRole.Masonry,
            frame: MaterialRole.DarkMasonry,
            window: MaterialRole.WarmWindow,
            roof: MaterialRole.Slate,
            accentStone: MaterialRole.FoundationStone,
            foundationHeightDm: 7,
            wallThicknessDm: 5,
            floorHeightDm: 34,
            doorHeightDm: 24,
            windowBaseDm: 20,
            windowHeightDm: 12,
            beamWidthDm: 3,
            roofOverhangDm: 4,
            typicalRoofHeightDm: 24,
            grandRoofHeightDm: 32,
            upperStoreyOverhangDm: 5);

        public static SettlementPlan Build(uint seed) => HightownTownPlanner.Build(seed);

        /// <summary>
        /// Plot envelopes, shared with Kentridge.
        ///
        /// The town voxel pass derives plot dressing offsets from these and then validates that
        /// what it placed lands inside the plot it belongs to. Those two steps disagree the moment
        /// a settlement uses its own envelopes: Hightown's larger lots pushed kerbs and yard
        /// clutter tens of decimetres outside their own plots and generation failed outright.
        ///
        /// Giving Hightown its own envelope table is the right end state and needs the dressing
        /// pass to place against the lot rather than the structure footprint. Until then Hightown
        /// is distinguished by its theme, its street grid and its archetype mix — which is what
        /// reads at a distance anyway — rather than by lot size.
        /// </summary>
        public static Int3 FootprintDm(StructureArchetype archetype) =>
            Content.Kentridge.KentridgeDefinition.FootprintDm(archetype);
    }
}
