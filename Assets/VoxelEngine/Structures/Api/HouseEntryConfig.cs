namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Optional porch/step treatment attached to a house facade door layout. Zero dimensions/counts
    /// disable each part so compatibility presets can opt out without introducing a second door
    /// representation. Door count, placement, dimensions, frames, and lintels remain in the shared
    /// <see cref="HouseDoorLayoutConfig"/> / <see cref="OpeningConfig"/> contracts.
    /// </summary>
    public struct HouseEntryTreatmentConfig
    {
        public int PorchWidth;
        public int PorchDepth;
        public int PorchHeight;
        public int StepCount;
        public int StepDepth;
        public int StepHeight;
        public StructureMaterialRole PorchMaterialRole;
        public StructureMaterialRole StepMaterialRole;

        public bool HasPorch => PorchWidth > 0 && PorchDepth > 0;
        public bool HasSteps => StepCount > 0;

        public bool IsWellFormed =>
            PorchWidth >= 0 && PorchDepth >= 0 && PorchHeight >= 0 &&
            StepCount >= 0 && StepDepth >= 0 && StepHeight >= 0 &&
            (PorchWidth == 0) == (PorchDepth == 0) &&
            (!HasPorch || PorchHeight > 0) &&
            (StepCount == 0 || (StepDepth > 0 && StepHeight > 0));
    }
}
