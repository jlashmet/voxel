namespace MountingForce.WorldGen
{
    /// <summary>
    /// High-level contract from settlement planning to architectural generation.
    ///
    /// This intentionally contains no roof, window, facade, room, chimney, beam, or other local
    /// architectural decisions. The settlement layer owns identity, use, placement, frontage, style,
    /// and the maximum envelope. A lower architecture compiler is responsible for filling that
    /// envelope with detailed structure.
    /// </summary>
    public readonly struct StructureIntent
    {
        public readonly int RoleId;
        public readonly string StyleId;
        public readonly StructureArchetype Archetype;
        public readonly DistrictKind District;
        public readonly Int2 PositionDm;
        public readonly FrontageDirection Frontage;
        public readonly Int3 EnvelopeDm;

        public StructureIntent(
            int roleId,
            string styleId,
            StructureArchetype archetype,
            DistrictKind district,
            Int2 positionDm,
            FrontageDirection frontage,
            Int3 envelopeDm)
        {
            RoleId = roleId;
            StyleId = styleId;
            Archetype = archetype;
            District = district;
            PositionDm = positionDm;
            Frontage = frontage;
            EnvelopeDm = envelopeDm;
        }

        public StructureIntent(BuildingPlot plot, string styleId, Int3 envelopeDm)
            : this(
                plot.RoleId,
                styleId,
                plot.Archetype,
                plot.District,
                plot.PositionDm,
                plot.Frontage,
                envelopeDm)
        {
        }
    }

    /// <summary>
    /// High-level contract for one anonymous piece of street frontage. Urban planning owns the
    /// district hierarchy and allowable massing; architectural generation owns the local building
    /// shape and facade details inside that envelope.
    /// </summary>
    public readonly struct UrbanFabricIntent
    {
        public readonly string StyleId;
        public readonly DistrictKind District;
        public readonly int MinStoreys;
        public readonly int MaxStoreys;
        public readonly int EnvelopeDm;
        public readonly int VariationContext;

        public UrbanFabricIntent(
            string styleId,
            DistrictKind district,
            int minStoreys,
            int maxStoreys,
            int envelopeDm,
            int variationContext)
        {
            StyleId = styleId;
            District = district;
            MinStoreys = minStoreys;
            MaxStoreys = maxStoreys;
            EnvelopeDm = envelopeDm;
            VariationContext = variationContext;
        }
    }
}
