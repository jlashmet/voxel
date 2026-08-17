namespace VoxelEngine.Structures.Api
{
    /// <summary>Horizontal plan shape for a reusable tower or turret.</summary>
    public enum StructureTowerShape : byte
    {
        Square = 0,
        Round = 1,
    }

    /// <summary>How repeated towers are positioned by an owning structure component.</summary>
    public enum StructureTowerPlacement : byte
    {
        Explicit = 0,
        Corners = 1,
        EvenlySpaced = 2,
    }

    /// <summary>Shared tower top treatment. Archetypes may further constrain valid combinations.</summary>
    public enum StructureTowerTopStyle : byte
    {
        Flat = 0,
        Parapet = 1,
        Roof = 2,
        Spire = 3,
    }

    /// <summary>
    /// Reusable tower/turret configuration. Square towers use Width/Depth; round towers use Radius.
    /// Count/placement are deterministic composition semantics. Roof and opening configuration are
    /// shared contracts rather than tower-specific copies.
    /// </summary>
    public struct TowerConfig
    {
        public StructureTowerShape Shape;
        public StructureTowerPlacement Placement;
        public StructureTowerTopStyle TopStyle;
        public int Width;
        public int Depth;
        public int Radius;
        public int Height;
        public int TaperPercent;
        public int Count;
        public int Spacing;
        public bool OpeningsEnabled;
        public OpeningConfig Opening;
        public RoofConfig Roof;
        public StructureMaterialRole WallMaterialRole;
        public StructureMaterialRole TrimMaterialRole;

        public bool IsWellFormed
        {
            get
            {
                if (Height <= 0 || TaperPercent < 0 || TaperPercent >= 100 || Count <= 0 || Spacing < 0)
                    return false;
                if (Shape == StructureTowerShape.Round)
                {
                    if (Radius <= 0) return false;
                }
                else if (Width <= 0 || Depth <= 0)
                {
                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Reusable parapet/battlement/crenellation cadence. Merlon and gap dimensions are separate so
    /// castles and other fortified structures can share the same deterministic repetition logic.
    /// </summary>
    public struct BattlementConfig
    {
        public int ParapetThickness;
        public int ParapetHeight;
        public int MerlonWidth;
        public int MerlonHeight;
        public int GapWidth;
        public int CornerMerlonWidth;
        public StructureMaterialRole MaterialRole;

        public bool IsWellFormed =>
            ParapetThickness > 0 && ParapetHeight >= 0
            && MerlonWidth > 0 && MerlonHeight > 0
            && GapWidth > 0 && CornerMerlonWidth >= 0;
    }

    /// <summary>Geometry families that share a narrow vertical protrusion contract.</summary>
    public enum StructureVerticalAccentStyle : byte
    {
        Chimney = 0,
        Spire = 1,
        Pinnacle = 2,
        Vent = 3,
    }

    /// <summary>
    /// Shared geometry for chimneys, spires, pinnacles, vents, and similar vertical accents.
    /// Fireplace ownership, bells, worship semantics, smoke, and other archetype/gameplay behavior
    /// intentionally stay outside this geometry configuration.
    /// </summary>
    public struct VerticalAccentConfig
    {
        public StructureVerticalAccentStyle Style;
        public int Width;
        public int Depth;
        public int Height;
        public int Taper;
        public int Count;
        public int Spacing;
        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole TrimMaterialRole;

        public bool IsWellFormed =>
            Width > 0 && Depth > 0 && Height > 0
            && Taper >= 0 && Count > 0 && Spacing >= 0;
    }
}
