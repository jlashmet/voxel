using System;
using Game.WorldBuilder.Api;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Selects the public-space backend for Kentridge. Modern organic plans lower through the shared
    /// WorldRoadNetwork; legacy authored streets retain the directed-ramp compatibility adapter.
    /// </summary>
    public static class KentridgeDirectedTownSurfaceCatalogue
    {
        private const int RampAxisOperand = 6;

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            if (plan.Routes.Count > 0)
            {
                WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, seed, settings);
                return WorldRoadNetworkVoxelCatalogue.Build(network, settings, allocator, precedence: 20);
            }

            FeatureCatalogue catalogue = KentridgeVerticalTownSurfaceCatalogue.Build(
                seed, settings, allocator);

            try
            {
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    if (rule.ExplicitCount != 1)
                        throw new InvalidOperationException(
                            "Directed Kentridge public-space definitions must own exactly one placement: "
                            + definition.Name);

                    int placementIndex = rule.ExplicitOffset;
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    int orientation = placement.Orientation & 3;
                    if (orientation == 0) continue;
                    if (orientation != 2)
                        throw new InvalidOperationException(
                            "Kentridge public roads only support direct or reversed half-turn ramps.");

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
                if (op == ShapeOp.End) break;
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
