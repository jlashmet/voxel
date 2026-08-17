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
        public readonly int3 SecondaryAnchor;
        public readonly string SecondaryAnchorName;
        public readonly int PrimitiveCount;
        public readonly bool PrimitiveCountExact;
        public readonly StructureDiagnostic Diagnostic;
        public readonly string Summary;

        public bool IsValid => Diagnostic.IsValid;
        public bool HasSecondaryAnchor => !string.IsNullOrEmpty(SecondaryAnchorName);

        public StructureInspection(
            string presetId,
            string archetype,
            int3 localMinimum,
            int3 localSize,
            Facing facing,
            int3 primaryAnchor,
            string primaryAnchorName,
            int3 secondaryAnchor,
            string secondaryAnchorName,
            int primitiveCount,
            bool primitiveCountExact,
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
            SecondaryAnchor = secondaryAnchor;
            SecondaryAnchorName = secondaryAnchorName ?? string.Empty;
            PrimitiveCount = primitiveCount;
            PrimitiveCountExact = primitiveCountExact;
            Diagnostic = diagnostic;
            Summary = summary ?? string.Empty;
        }

        /// <summary>
        /// Returns a transient copy decorated with a primitive count obtained by an evaluator/tool.
        /// This is useful for generators such as caves whose exact count depends on the seed.
        /// </summary>
        public StructureInspection WithPrimitiveCount(int primitiveCount)
        {
            return new StructureInspection(
                PresetId,
                Archetype,
                LocalMinimum,
                LocalSize,
                Facing,
                PrimaryAnchor,
                PrimaryAnchorName,
                SecondaryAnchor,
                SecondaryAnchorName,
                primitiveCount,
                true,
                Diagnostic,
                Summary);
        }

        public override string ToString()
        {
            string primitiveText = PrimitiveCountExact
                ? PrimitiveCount.ToString()
                : "unresolved";
            string anchors = PrimaryAnchorName + "@" + PrimaryAnchor;
            if (HasSecondaryAnchor)
                anchors += "," + SecondaryAnchorName + "@" + SecondaryAnchor;
            return Archetype + " " + PresetId + " bounds=" + LocalMinimum + "+" + LocalSize +
                   " facing=" + Facing + " anchors=" + anchors +
                   " primitives=" + primitiveText + " status=" + Diagnostic;
        }
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
                config.FoundationDepth + config.FrontDoors.Opening.BottomOffset,
                minimum.z);
            int3 hearth = new int3(
                minimum.x + config.Width / 2,
                config.FoundationDepth,
                minimum.z + config.Depth / 2);

            int primitiveCount = HousePrimitiveCount(in config);
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
                hearth,
                "hearth",
                primitiveCount,
                true,
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
                int3.zero,
                string.Empty,
                -1,
                false,
                diagnostic,
                summary);
        }

        private static int HousePrimitiveCount(in HouseConfig config)
        {
            int count = 4; // foundation, outer shell, interior carve, roof
            count += math.max(0, config.FloorCount - 1);
            count += math.max(0, config.FrontDoors.Count);
            count += math.max(0, config.RearDoors.Count);
            count += math.max(0, config.LeftDoors.Count);
            count += math.max(0, config.RightDoors.Count);
            count += math.max(0, config.FrontWindows.Count);
            count += math.max(0, config.RearWindows.Count);
            count += math.max(0, config.LeftWindows.Count);
            count += math.max(0, config.RightWindows.Count);
            if (config.Chimney.Enabled) count++;
            return count;
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
