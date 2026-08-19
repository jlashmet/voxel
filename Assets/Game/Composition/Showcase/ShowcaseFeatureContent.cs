namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Which authored content a showcase world generates, alongside terrain.
    /// </summary>
    public enum ShowcaseFeatureContent
    {
        /// <summary>
        /// Terrain, the castle, and the authored Kentridge town — the full production showcase.
        /// </summary>
        Full = 0,

        /// <summary>
        /// Terrain and the castle only.
        ///
        /// The feature catalogue is left uncreated, which is the world's existing signal to skip
        /// settlement placement entirely rather than a filter applied afterwards. The castle is
        /// landmark work and is unaffected. This exists so the renderer can be measured against a
        /// scene whose geometry is terrain plus one landmark, instead of terrain plus a town.
        /// </summary>
        CastleOnly = 1,

        /// <summary>
        /// Terrain and a single authored house — the detailed farmhouse, with its windows and
        /// front door — placed where the spawn camera already looks.
        ///
        /// The castle is skipped entirely. It is tens of millions of voxels sculpted over hundreds
        /// of frames, which is the right landmark for the production showcase and far too slow for
        /// a scene whose purpose is to iterate on frame cost.
        /// </summary>
        HouseOnly = 2,
    }

    /// <summary>
    /// Where a showcase world's startup state comes from.
    /// </summary>
    public enum ShowcaseStartupSource
    {
        /// <summary>
        /// Restore the offline bake. The production showcase path: the castle is complete on the
        /// first frame because authoring it takes minutes.
        /// </summary>
        Bake = 0,

        /// <summary>
        /// Generate during the scene, as the showcase did before the bake existed.
        ///
        /// Only the origin region is produced up front — enough to place the player and queue the
        /// castle. Terrain and castle authoring then advance on the ordinary per-frame budget, so
        /// the world assembles while it is being looked at. Viable only for a small radius; the
        /// full showcase is why the bake exists.
        /// </summary>
        Generate = 1,
    }
}
