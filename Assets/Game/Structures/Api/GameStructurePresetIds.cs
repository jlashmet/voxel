namespace Game.Structures.Api
{
    /// <summary>
    /// Game-owned stable preset IDs. Each constant names an ordinary pure config factory; IDs are
    /// authoring/selection metadata and never a mutable generation registry.
    /// </summary>
    public static class GameStructurePresetIds
    {
        public const string ShedStorageV1 = "shed.storage.v1";
        public const string ShedWorkshopV1 = "shed.workshop.v1";
        public const string ShedLeanToV1 = "shed.lean-to.v1";

        public const string ChurchChapelV1 = "church.chapel.v1";
        public const string ChurchParishV1 = "church.parish.v1";

        public const string CathedralSimpleV1 = "cathedral.simple.v1";
        public const string CathedralGothicV1 = "cathedral.gothic.v1";

        public const string TempleClassicalColumnedV1 = "temple.classical-columned.v1";
        public const string TempleCourtyardV1 = "temple.courtyard.v1";

        public const string CastleCompatibilityV1 = "castle.compatibility.v1";
        public const string CastleKeepOnlyV1 = "castle.keep-only.v1";
        public const string CastleWalledV1 = "castle.walled.v1";
    }
}
