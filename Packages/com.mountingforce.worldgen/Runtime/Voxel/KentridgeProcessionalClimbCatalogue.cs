using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Exact geometric realisation of the Market-to-Civic procession. The generic street remains the
    /// stable gameplay road underneath; this higher-precedence urban layer inserts the two semantic
    /// pauses and re-segments the connecting rises so road geometry cannot smooth straight through
    /// Upper Landing or Civic Gate.
    /// </summary>
    public static class KentridgeProcessionalClimbCatalogue
    {
        private const int SurfaceDepthDm = 5;
        private const int ClearAboveDm = 32;
        private const int Precedence = 28;

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeProcessionalClimbPlan plan = KentridgeProcessionalClimb.Build(seed);
            int s = settings.VoxelsPerDecimetre;
            int reference = KentridgeVerticalProfile.ReferenceSurfaceY(seed, s);
            var programs = new int[plan.Segments.Count][];
            int programLength = 0;

            for (int i = 0; i < plan.Segments.Count; i++)
            {
                programs[i] = SegmentProgram(plan.Segments[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: plan.Segments.Count,
                rules: plan.Segments.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: plan.Segments.Count,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                KentridgeProcessionalSegment segment = plan.Segments[i];
                int[] program = programs[i];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                int width = segment.WidthDm * s;
                int length = segment.LengthDm * s + 1;
                int surfaceDepth = SurfaceDepthDm * s;
                int clear = (ClearAboveDm + segment.RiseDm) * s;
                int lowSurface = reference + segment.SouthOffsetDm * s;

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-procession-" + segment.Id),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(
                        width,
                        surfaceDepth + clear,
                        length),
                    MaxSlope = 32,
                    Precedence = Precedence,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = 0,
                    AnchorCount = 0,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = segment.Kind == KentridgeProcessionalSegmentKind.Landing ? 2 : 3,
                };

                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(
                        KentridgeTownPlanner.MainSpineXDm * s - width / 2,
                        lowSurface - surfaceDepth,
                        segment.NorthZDm * s),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };

                catalogue.Rules[i] = new PlacementRule
                {
                    DefinitionId = i,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 32,
                    MinSpacing = 0,
                    ClusterMin = 0,
                    ClusterMax = 0,
                    ExclusionMask = 0,
                    ExplicitOffset = i,
                    ExplicitCount = 1,
                };

                programOffset += program.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge processional climb catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static int[] SegmentProgram(
            KentridgeProcessionalSegment segment,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int width = segment.WidthDm * s;
            int length = segment.LengthDm * s + 1;
            int surfaceDepth = SurfaceDepthDm * s;
            int rise = segment.RiseDm * s;
            int clear = (ClearAboveDm + segment.RiseDm) * s;
            byte paving = settings.Materials.Resolve(MaterialRole.RoadSurface);
            var b = new ProgramBuilder();

            // Remove the generic road/terrain above the exact low end of this segment, then rebuild
            // the desired flat or north-rising surface. The previous road support below this plane is
            // deliberately retained, so this stage changes the public section without creating a new
            // deep causeway.
            b.Carve(0, surfaceDepth, 0, width, clear, length);
            b.Box(0, 0, 0, width, surfaceDepth, length, paving);

            if (segment.Kind == KentridgeProcessionalSegmentKind.Rise && rise > 0)
            {
                b.Ramp(
                    0,
                    surfaceDepth,
                    0,
                    width,
                    rise,
                    length,
                    (byte)(2 | ShapeOps.ReverseRampBit),
                    paving);
            }

            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material,
                PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                    material, 0, 0, (int)mode);
            }

            public void Carve(int x, int y, int z, int sx, int sy, int sz)
            {
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);
            }

            public void Ramp(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte axis,
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitRamp, x, y, z, sx, sy, sz,
                    axis, material, 0, 0, (int)PrimitiveMode.Fill);
            }

            public int[] Finish()
            {
                Op(ShapeOp.End);
                return _code.ToArray();
            }

            private void Op(ShapeOp op, params int[] operands)
            {
                _code.Add((int)op);
                _code.Add(0);
                _code.AddRange(operands);
            }
        }
    }
}
