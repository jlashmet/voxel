namespace VoxelEngine.Structures.Api
{
    public enum StructureColumnShape : byte
    {
        Square = 0,
        Round = 1,
    }

    /// <summary>Reusable deterministic column/colonnade geometry contract.</summary>
    public struct ColumnConfig
    {
        public StructureColumnShape Shape;
        public int Width;
        public int Height;
        public int BaseHeight;
        public int CapitalHeight;
        public int Spacing;
        public StructureMaterialRole ShaftMaterialRole;
        public StructureMaterialRole BaseMaterialRole;
        public StructureMaterialRole CapitalMaterialRole;

        // Compatibility aliases for the earlier shared vertical-feature contract. New composition
        // should prefer Width plus MaxCountForSpan; these keep older presets/tests source-compatible.
        public int Count;
        public int Radius
        {
            get => Width / 2;
            set => Width = value * 2;
        }
        public StructureMaterialRole TrimMaterialRole
        {
            get => CapitalMaterialRole;
            set
            {
                BaseMaterialRole = value;
                CapitalMaterialRole = value;
            }
        }

        public bool IsWellFormed =>
            (Shape == StructureColumnShape.Square || Shape == StructureColumnShape.Round) &&
            Width >= 2 && Height > BaseHeight + CapitalHeight &&
            BaseHeight >= 0 && CapitalHeight >= 0 && Spacing >= Width && Count >= 0;

        public int MaxCountForSpan(int span, int margin)
        {
            if (!IsWellFormed || margin < 0 || span < margin * 2 + Width) return 0;
            return 1 + (span - margin * 2 - Width) / Spacing;
        }
    }
}
