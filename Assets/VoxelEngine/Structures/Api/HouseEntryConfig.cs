namespace VoxelEngine.Structures.Api
{
    /// <summary>House facade identity in definition-local coordinates.</summary>
    public enum HouseFacade : byte
    {
        Front = 0,
        Rear = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>Deterministic placement policy for one or more doors on a facade.</summary>
    public enum HouseDoorPlacement : byte
    {
        Centered = 0,
        FromStart = 1,
        FromEnd = 2,
        EvenlySpaced = 3,
    }

    /// <summary>
    /// Optional porch/step treatment at a house entry. Zero dimensions/counts disable each part so
    /// compatibility presets can opt out without a parallel entry representation.
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
            (StepCount == 0 || (StepDepth > 0 && StepHeight > 0));
    }

    /// <summary>
    /// Bounded door authoring for one facade. Door geometry and frames reuse <see cref="OpeningConfig"/>;
    /// this layer adds facade-specific count/placement and optional porch/step treatment.
    /// </summary>
    public struct HouseFacadeDoorConfig
    {
        public HouseFacade Facade;
        public int Count;
        public HouseDoorPlacement Placement;
        public int PlacementOffset;
        public OpeningConfig Door;
        public HouseEntryTreatmentConfig EntryTreatment;

        public bool Enabled => Count > 0;

        public bool IsWellFormed => Count == 0 ||
            (Count > 0 && PlacementOffset >= 0 &&
             Door.Kind == StructureOpeningKind.Door && Door.IsWellFormed &&
             EntryTreatment.IsWellFormed);
    }
}
