namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Acceptance-facing diagnostics for the gallery structural proof sites. These values remain
    /// showcase composition policy; the shared structural solver stays terrain/site agnostic.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        public int WorldbuildingGalleryStructuralCliffTerrainRise => FindCliffSite().Rise;
    }
}
