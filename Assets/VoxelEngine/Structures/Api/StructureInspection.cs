using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Ephemeral authoring/debug view of a resolved config. This is recomputed from config + preset ID
    /// and must never be persisted as generation truth.
    /// </summary>
    public readonly struct StructureInspection
    {
        public readonly string PresetId;
        public readonly string Archetype;
        public readonly int3 LocalMinimum;
        public readonly int3 LocalSize;
        public readonly Facing Facing;
        public readonly int3 PrimaryAnchor;
        public readonly string PrimaryAnchorName;
        public readonly StructureDiagnostic Diagnostic;
        public readonly string Summary;

        public bool IsValid => Diagnostic.IsValid;

        public StructureInspection(
            string presetId,
            string archetype,
            int3 localMinimum,
            int3 localSize,
            Facing facing,
            int3 primaryAnchor,
            string primaryAnchorName,
            StructureDiagnostic diagnostic,
            string summary)
        {
            PresetId = presetId ?? string.Empty;
            Archetype = archetype ?? string.Empty;
            LocalMinimum = localMinimum;
            LocalSize = localSize;
            Facing = facing;
            PrimaryAnchor = primaryAnchor;
            PrimaryAnchorName = primaryAnchorName ?? string.Empty;
            Diagnostic = diagnostic;
            Summary = summary ?? string.Empty;
        }

        public override string ToString() =>
            Archetype + " " + PresetId + " bounds=" + LocalMinimum + "+" + LocalSize +
            " facing=" + Facing + " anchor=" + PrimaryAnchorName + "@" + PrimaryAnchor +
            " status=" + Diagnostic;
    }

    public static class StructureInspectionTools
    {
        public static StructureInspection House(string presetId, in HouseConfig config)
        {
            StructureDiagnostic id = StructureConfigDiagnostics.PresetId(presetId);
            StructureDiagnostic diagnostic = id.IsValid
                ? StructureConfigDiagnostics.House(in config)
                : id;

            int roofHeight = RoofHeight(in config.Roof, config.Width, config.Depth);
            int height = config.FoundationDepth + config.Walls.Height + roofHeight;
            int3 minimum = new int3(
                config.Footprint.Primary.Min.x,
                0,
                config.Footprint.Primary.Min.y);
            int3 size = new int3(config.Width, math.max(1, height), config.Depth);
            int3 door = new int3(
                minimum.x + config.Width / 2,
                config.FoundationDepth + config.MainDoor.BottomOffset,
                minimum.z);

            string summary = config.FloorCount + " floor(s), " + config.Roof.Style +
                             " roof, " + config.Width + "x" + config.Depth + " footprint";
            return new StructureInspection(
                presetId,
                "house",
                minimum,
                size,
                Facing.South,
                door,
                "door",
                diagnostic,
                summary);
        }

        public static StructureInspection Cave(
            string presetId,
            in CaveConfig config,
            in CaveGenerationRequest request)
        {
            StructureDiagnostic id = StructureConfigDiagnostics.PresetId(presetId);
            StructureDiagnostic diagnostic = id.IsValid
                ? StructureConfigDiagnostics.CaveRequest(in request, in config)
                : id;

            int3 half = config.BoundsHalfExtents;
            int3 minimum = -half;
            int3 size = new int3(
                half.x * 2 + 1,
                half.y * 2 + 1,
                half.z * 2 + 1);
            string summary = config.MainSegmentCount + " main segments, up to " +
                             config.MaxBranches + " branches, " + config.ChamberShape + " chambers";
            return new StructureInspection(
                presetId,
                "cave",
                minimum,
                size,
                request.Entrance.Facing,
                request.Entrance.LocalPosition,
                "entrance",
                diagnostic,
                summary);
        }

        private static int RoofHeight(in RoofConfig roof, int width, int depth)
        {
            if (roof.Style == RoofStyle.Flat)
                return math.max(1, roof.Thickness + roof.ParapetHeight);
            if (roof.PitchRun <= 0) return math.max(1, roof.Thickness);
            int span = roof.RidgeAxis == RoofAxis.Z ? width : depth;
            int half = math.max(1, span / 2);
            return math.max(1, half * roof.PitchRise / roof.PitchRun + roof.Thickness);
        }
    }
}
