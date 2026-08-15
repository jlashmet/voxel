using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Gives the Civic Crown a formal summit ground plane distinct from the commercial Market Square.
    /// The existing terrain/profile owns grading; this thin hard layer is completely flush. A dark
    /// perimeter makes the space read as a room while a narrow north/south stone axis reinforces the
    /// processional climb between the stable Church and Mayor House anchors.
    /// </summary>
    public static class KentridgeCivicForecourtCatalogue
    {
        public const byte ForecourtPrecedence = 62;
        public const int SurfaceThicknessDm = 2;
        public const int BorderWidthDm = 4;
        public const int AxisWidthDm = 12;

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeCivicForecourtPlan plan = KentridgeCivicForecourtPlanner.Build(seed);
            int s = settings.VoxelsPerDecimetre;
            int width = plan.WidthDm * s;
            int depth = plan.DepthDm * s;
            int thickness = SurfaceThicknessDm * s;
            int[] program = ForecourtProgram(width, depth, settings);

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: program.Length,
                materials: 0,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            for (int i = 0; i < program.Length; i++)
                catalogue.Program[i] = program[i];

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-civic-crown-forecourt"),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(width, thickness, depth),
                MaxSlope = 32,
                Precedence = ForecourtPrecedence,
                ProgramOffset = 0,
                ProgramLength = program.Length,
                MaxPrimitives = 7,
            };

            int surfaceY = KentridgeVerticalProfile.SurfaceYAtDm(
                plan.CentreDm.X,
                plan.CentreDm.Y,
                seed,
                s);
            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = new int3(
                    plan.MinXDm * s,
                    surfaceY - thickness,
                    plan.MinZDm * s),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = 0,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 1024,
                MaxSlope = 32,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge civic forecourt catalogue failed validation: " + result);
            }
            return catalogue;
        }

        private static int[] ForecourtProgram(
            int width,
            int depth,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int border = BorderWidthDm * s;
            int axis = AxisWidthDm * s;
            int thickness = SurfaceThicknessDm * s;
            int innerWidth = width - border * 2;
            int innerDepth = depth - border * 2;
            int axisStart = width / 2 - axis / 2;
            int axisEnd = axisStart + axis;

            if (innerWidth <= axis || innerDepth <= 0)
                throw new InvalidOperationException(
                    "Kentridge civic forecourt is too small for its formal paving grammar.");

            byte pale = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();

            // Dark perimeter: a formal room boundary without any height break.
            b.Box(0, 0, 0, width, thickness, border, dark);
            b.Box(0, 0, depth - border, width, thickness, border, dark);
            b.Box(0, 0, border, border, thickness, innerDepth, dark);
            b.Box(width - border, 0, border, border, thickness, innerDepth, dark);

            // Pale court split around a narrow dark processional axis so no same-precedence material
            // overlap is needed to create the paving pattern.
            b.Box(border, 0, border,
                axisStart - border,
                thickness,
                innerDepth,
                pale);
            b.Box(axisEnd, 0, border,
                width - border - axisEnd,
                thickness,
                innerDepth,
                pale);
            b.Box(axisStart, 0, border,
                axis,
                thickness,
                innerDepth,
                dark);

            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox,
                    x, y, z,
                    sx, sy, sz,
                    material,
                    0,
                    0,
                    (int)PrimitiveMode.Fill);
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
