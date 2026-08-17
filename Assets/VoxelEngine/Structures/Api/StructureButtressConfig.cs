namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Shared repeated buttress configuration. Flying buttresses are a bounded stepped bridge from
    /// an outer pier toward the supported wall; no curves, global searches, or floating point state.
    /// </summary>
    public struct ButtressConfig
    {
        public int Width;
        public int Depth;
        public int Height;
        public int Spacing;
        public int StartMargin;
        public int EndMargin;
        public int TaperPercent;

        public bool FlyingEnabled;
        public int FlyingSpan;
        public int FlyingRise;
        public int FlyingThickness;
        public int FlyingSupportHeight;

        public StructureMaterialRole MaterialRole;
        public StructureMaterialRole FlyingMaterialRole;

        public bool IsWellFormed =>
            Width > 0 && Depth > 0 && Height > 0 &&
            Spacing >= Width && StartMargin >= 0 && EndMargin >= 0 &&
            TaperPercent >= 0 && TaperPercent <= 80 &&
            (!FlyingEnabled ||
             (FlyingSpan > 0 && FlyingRise >= 0 && FlyingThickness > 0 &&
              FlyingThickness <= Width && FlyingSupportHeight > 0 &&
              FlyingSupportHeight < Height));

        public int MaxCountForSpan(int span)
        {
            if (!IsWellFormed || span <= StartMargin + EndMargin + Width) return 0;
            int usable = span - StartMargin - EndMargin;
            return 1 + (usable - Width) / Spacing;
        }
    }
}
