namespace VoxelEngine.Structures.Api
{
    public enum StructureDiagnosticCode : ushort
    {
        None = 0,
        InvalidPresetId,
        InvalidFootprint,
        InvalidFoundation,
        InvalidWalls,
        InvalidFloors,
        InvalidOpening,
        InvalidOpeningLayout,
        InvalidRoof,
        InvalidDormer,
        InvalidChimney,
        InvalidFacing,
        InvalidDimensions,
        InvalidProbability,
        InvalidBranching,
        InvalidChamber,
        InvalidRoughness,
        InvalidBounds,
        InvalidVerticalRange,
        UnsupportedFeature,
        InvalidSeed,
        InvalidAttachment,
        InvalidComposition,
    }

    /// <summary>
    /// Side-effect-free authoring validation result. Field is a stable config path suitable for
    /// inspector/debug UI; Message is human readable. Generation does not persist or consult it.
    /// </summary>
    public readonly struct StructureDiagnostic
    {
        public readonly StructureDiagnosticCode Code;
        public readonly string Field;
        public readonly string Message;

        public bool IsValid => Code == StructureDiagnosticCode.None;

        public StructureDiagnostic(
            StructureDiagnosticCode code,
            string field,
            string message)
        {
            Code = code;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString() => IsValid
            ? "Valid"
            : Code + " at " + Field + ": " + Message;

        public static StructureDiagnostic Valid =>
            new StructureDiagnostic(StructureDiagnosticCode.None, string.Empty, string.Empty);
    }

    /// <summary>
    /// Author-facing validation for reusable engine-owned structure configs. These checks mirror the
    /// bounded compiler/generator contracts but return the first precise failure instead of bool-only
    /// rejection or an exception from deep inside authoring.
    /// </summary>
    public static class StructureConfigDiagnostics
    {
        public static StructureDiagnostic PresetId(string presetId)
        {
            return StructurePresetId.IsWellFormed(presetId)
                ? StructureDiagnostic.Valid
                : Invalid(
                    StructureDiagnosticCode.InvalidPresetId,
                    "PresetId",
                    "Expected <archetype>.<variant>.v<positive-version> using lowercase ASCII/kebab segments.");
        }

        public static StructureDiagnostic House(in HouseConfig config)
        {
            if (!config.Footprint.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidFootprint, "Footprint",
                    "Primary footprint/base-plane/foundation fields are not well formed.");
            if (config.Footprint.FoundationDepth <= 0)
                return Invalid(StructureDiagnosticCode.InvalidFoundation, "Footprint.FoundationDepth",
                    "Compiled houses require a positive foundation depth.");
            if (!config.Walls.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidWalls, "Walls",
                    "Wall length, height, thickness, or repetition fields are invalid.");
            if (config.Width <= config.WallThickness * 2 || config.Depth <= config.WallThickness * 2)
                return Invalid(StructureDiagnosticCode.InvalidDimensions, "Footprint.Primary.Size",
                    "House width and depth must leave positive interior space after wall thickness.");
            if (config.Walls.Length != config.Width)
                return Invalid(StructureDiagnosticCode.InvalidDimensions, "Walls.Length",
                    "Wall-run length must equal the primary footprint width.");
            if (!config.Floors.IsWellFormed || config.Floors.FloorCount <= 0 ||
                config.Floors.LevelHeight <= 0 || config.Floors.SlabThickness < 0)
                return Invalid(StructureDiagnosticCode.InvalidFloors, "Floors",
                    "Floor count/level height/slab thickness are invalid.");
            if (config.Walls.Height < config.Floors.FloorCount * config.Floors.LevelHeight)
                return Invalid(StructureDiagnosticCode.InvalidFloors, "Walls.Height",
                    "Wall height does not contain all configured floor levels.");

            StructureDiagnostic openings = HouseOpenings(in config);
            if (!openings.IsValid) return openings;

            if (!config.Roof.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidRoof, "Roof",
                    "Roof style, pitch, thickness, overhang, or parapet values are invalid.");
            if (!config.Dormers.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidDormer, "Dormers",
                    "Enabled dormers require positive dimensions and a non-flat roof style.");
            if (!config.Chimney.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidChimney, "Chimney",
                    "Chimney geometry, local position, or fireplace attachment is invalid.");

            return StructureDiagnostic.Valid;
        }

        public static StructureDiagnostic Cave(in CaveConfig config)
        {
            if (config.TunnelWidth < 3 || config.TunnelHeight < 3 ||
                config.SegmentLength < 2 || config.MainSegmentCount < 1 || config.MainSegmentCount > 512)
                return Invalid(StructureDiagnosticCode.InvalidDimensions, "Tunnel",
                    "Tunnel width/height must be >= 3, segment length >= 2, and main segment count 1..512.");
            if (!Percent(config.TurnChancePercent) || !Percent(config.VerticalChancePercent) ||
                !Percent(config.BranchChancePercent) || !Percent(config.ChamberChancePercent))
                return Invalid(StructureDiagnosticCode.InvalidProbability, "ChancePercent",
                    "Turn, vertical, branch, and chamber probabilities must each be in 0..100.");
            if (config.MaxVerticalStepPerSegment < 0 ||
                config.MaxVerticalStepPerSegment > config.SegmentLength ||
                config.SurfaceDescentSegments < 0 ||
                config.SurfaceDescentSegments > config.MainSegmentCount ||
                config.SurfaceDescentPerSegment < 0 ||
                config.SurfaceDescentPerSegment > config.SegmentLength ||
                config.MinimumSurfaceCover < 0)
                return Invalid(StructureDiagnosticCode.InvalidVerticalRange, "Vertical/SurfaceDescent",
                    "Vertical step/descent values exceed the bounded tunnel segment contract.");
            if (config.MaxBranches < 0 || config.MaxBranches > 32 ||
                config.MaxBranchDepth < 0 || config.MaxBranchDepth > 8 ||
                config.BranchSegmentCount < 1 || config.BranchSegmentCount > 256 ||
                config.MinBranchSeparation < 0)
                return Invalid(StructureDiagnosticCode.InvalidBranching, "Branches",
                    "Branches require max count 0..32, depth 0..8, segment count 1..256, and non-negative separation.");
            if ((config.ChamberShape != CaveChamberShape.Round && config.ChamberShape != CaveChamberShape.Box) ||
                config.MinChamberRadius < 2 || config.MaxChamberRadius < config.MinChamberRadius ||
                config.MinChamberHeight < 3 || config.MaxChamberHeight < config.MinChamberHeight)
                return Invalid(StructureDiagnosticCode.InvalidChamber, "Chambers",
                    "Chamber shape/radius/height range is invalid.");
            if (config.FloorRoughness < 0 || config.CeilingRoughness < 0 || config.WallRoughness < 0)
                return Invalid(StructureDiagnosticCode.InvalidRoughness, "Roughness",
                    "Floor, ceiling, and wall roughness must be non-negative.");
            if (config.BoundsHalfExtents.x <= config.TunnelWidth ||
                config.BoundsHalfExtents.y <= config.TunnelHeight ||
                config.BoundsHalfExtents.z <= config.TunnelWidth ||
                config.MaxChamberRadius + config.WallRoughness >= config.BoundsHalfExtents.x ||
                config.MaxChamberRadius + config.WallRoughness >= config.BoundsHalfExtents.z)
                return Invalid(StructureDiagnosticCode.InvalidBounds, "BoundsHalfExtents",
                    "Generation bounds must contain the tunnel and maximum rough chamber radius.");
            if (config.MinVerticalOffset > config.MaxVerticalOffset ||
                config.MinVerticalOffset < -config.BoundsHalfExtents.y ||
                config.MaxVerticalOffset > config.BoundsHalfExtents.y)
                return Invalid(StructureDiagnosticCode.InvalidVerticalRange, "VerticalOffset",
                    "Vertical offsets must be ordered and remain inside local generation bounds.");
            if (config.EnableLoops)
                return Invalid(StructureDiagnosticCode.UnsupportedFeature, "EnableLoops",
                    "Cave loops are intentionally unsupported until bounded deterministic reconnection exists.");
            return StructureDiagnostic.Valid;
        }

        public static StructureDiagnostic CaveRequest(
            in CaveGenerationRequest request,
            in CaveConfig config)
        {
            StructureDiagnostic cave = Cave(in config);
            if (!cave.IsValid) return cave;
            if (request.Seed == 0)
                return Invalid(StructureDiagnosticCode.InvalidSeed, "Seed",
                    "Cave generation requires a non-zero stable seed.");
            if (!request.Entrance.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidAttachment, "Entrance",
                    "Entrance requires a supported mode, cardinal facing, width/height >= 3, and positive clearance.");
            if (!request.TryGetWorldBounds(in config, out _))
                return Invalid(StructureDiagnosticCode.InvalidBounds, "Origin/BoundsHalfExtents",
                    "Resolved world bounds overflow integer structure-generation bounds.");
            return StructureDiagnostic.Valid;
        }

        private static StructureDiagnostic HouseOpenings(in HouseConfig config)
        {
            if (!config.MainDoor.IsWellFormed || config.MainDoor.Kind != StructureOpeningKind.Door)
                return Invalid(StructureDiagnosticCode.InvalidOpening, "MainDoor",
                    "MainDoor must be a well-formed door opening.");
            if (!config.FrontDoors.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "FrontDoors",
                    "Front-door count/placement/opening/explicit offsets are invalid.");
            if (!config.RearDoors.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "RearDoors",
                    "Rear-door count/placement/opening/explicit offsets are invalid.");
            if (!config.LeftDoors.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "LeftDoors",
                    "Left-door count/placement/opening/explicit offsets are invalid.");
            if (!config.RightDoors.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "RightDoors",
                    "Right-door count/placement/opening/explicit offsets are invalid.");
            if (!config.FrontWindows.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "FrontWindows",
                    "Front-window count/placement/opening/explicit offsets are invalid.");
            if (!config.RearWindows.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "RearWindows",
                    "Rear-window count/placement/opening/explicit offsets are invalid.");
            if (!config.LeftWindows.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "LeftWindows",
                    "Left-window count/placement/opening/explicit offsets are invalid.");
            if (!config.RightWindows.IsWellFormed)
                return Invalid(StructureDiagnosticCode.InvalidOpeningLayout, "RightWindows",
                    "Right-window count/placement/opening/explicit offsets are invalid.");
            return StructureDiagnostic.Valid;
        }

        private static bool Percent(int value) => value >= 0 && value <= 100;

        private static StructureDiagnostic Invalid(
            StructureDiagnosticCode code,
            string field,
            string message) => new StructureDiagnostic(code, field, message);
    }
}
