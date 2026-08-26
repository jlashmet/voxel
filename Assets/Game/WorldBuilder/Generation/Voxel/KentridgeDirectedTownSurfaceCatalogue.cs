using System;
using Unity.Collections;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Builds the macro-vertical public-space catalogue and replaces its old 180-degree ramp
    /// rotations with an explicit negative-axis ramp direction.
    ///
    /// Full-footprint road support, fill, and carve boxes are symmetric under a half-turn, so their
    /// world occupancy is unchanged when the placement orientation is reset to zero. The ramp is the
    /// only directional primitive in these definitions. Marking that primitive as reversed preserves
    /// the intended northbound climb without asking ShapeProgram's generic bounding-box rotation to
    /// infer slope direction from normalized bounds.
    /// </summary>
    public static class KentridgeDirectedTownSurfaceCatalogue
    {
        private const int RampAxisOperand = 6;

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            FeatureCatalogue catalogue = KentridgeVerticalTownSurfaceCatalogue.Build(
                seed, settings, allocator);

            try
            {
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];

                    // Direction adaptation mutates a definition's program, not one evaluated copy of
                    // it. The source catalogue intentionally owns one definition per road/plaza piece;
                    // enforce that contract so a future deduplication cannot reverse a shared program.
                    if (rule.ExplicitCount != 1)
                        throw new InvalidOperationException(
                            "Directed Kentridge public-space definitions must own exactly one placement: "
                            + definition.Name);

                    int placementIndex = rule.ExplicitOffset;
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    int orientation = placement.Orientation & 3;

                    if (orientation == 0)
                        continue;
                    if (orientation != 2)
                        throw new InvalidOperationException(
                            "Kentridge public roads only support direct or reversed half-turn ramps.");

                    // Level road pieces contain only full-footprint boxes. Those are symmetric
                    // under a half-turn, so there is no directional primitive to rewrite.
                    ReverseRamps(ref catalogue, in definition);

                    placement.Orientation = 0;
                    catalogue.ExplicitPlacements[placementIndex] = placement;
                }

                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static bool ReverseRamps(ref FeatureCatalogue catalogue,
                                         in FeatureDefinition definition)
        {
            bool found = false;
            int pc = definition.ProgramOffset;
            int end = definition.ProgramOffset + definition.ProgramLength;

            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                if (length < 0 || pc + length > end)
                    throw new InvalidOperationException(
                        "Malformed Kentridge public-space program while directing ramps.");

                if (op == ShapeOp.End)
                    break;

                if (op == ShapeOp.EmitRamp)
                {
                    int axisIndex = pc + 2 + RampAxisOperand;
                    int axis = catalogue.Program[axisIndex];
                    int baseAxis = axis & ShapeOps.RampAxisMask;
                    if (baseAxis != 0 && baseAxis != 2)
                        throw new InvalidOperationException(
                            "Kentridge public road ramp used an unsupported axis: " + axis);
                    if ((axis & ShapeOps.ReverseRampBit) != 0)
                        throw new InvalidOperationException(
                            "Kentridge ramp was already marked reversed before direction adaptation.");

                    catalogue.Program[axisIndex] = axis | ShapeOps.ReverseRampBit;
                    found = true;
                }

                pc += length;
            }

            return found;
        }
    }
}
