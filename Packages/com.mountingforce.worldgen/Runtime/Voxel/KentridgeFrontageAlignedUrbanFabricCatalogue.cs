using System;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Places generated anonymous buildings by their actual ground-floor facade instead of by the
    /// outer edge of the fixed safety envelope used by the detail grammar. The settlement layer owns
    /// the frontage line; the architecture layer may choose a shallower local depth; this adapter is
    /// the lowering seam that reconciles those two contracts without leaking local dimensions upward.
    /// </summary>
    public static class KentridgeFrontageAlignedUrbanFabricCatalogue
    {
        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            FeatureCatalogue catalogue = KentridgeUrbanFabricCatalogue.Build(
                seed, settings, allocator);

            try
            {
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    if (rule.ExplicitCount != 1)
                        throw new InvalidOperationException(
                            "Kentridge anonymous fabric definitions must own one placement.");

                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    int pc = definition.ProgramOffset;
                    ShapeOp firstOp = (ShapeOp)catalogue.Program[pc];
                    if (firstOp != ShapeOp.EmitBox && firstOp != ShapeOp.EmitRoundedBox)
                        throw new InvalidOperationException(
                            "Kentridge anonymous fabric must begin with its foundation solid.");

                    // EmitBox and EmitRoundedBox both begin x,y,z,sx,sy,sz after op/mask. The first
                    // semantic foundation is centred inside the fixed envelope, so local z is exactly
                    // the unwanted facade setback regardless of its rounded reconstruction policy.
                    int frontInset = catalogue.Program[pc + 4];
                    if (frontInset <= 0) continue;

                    int placementIndex = rule.ExplicitOffset;
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    int3 delta = (placement.Orientation & 3) switch
                    {
                        0 => new int3(0, 0, -frontInset), // South
                        1 => new int3(-frontInset, 0, 0), // West
                        2 => new int3(0, 0, frontInset),  // North
                        3 => new int3(frontInset, 0, 0),  // East
                        _ => int3.zero,
                    };

                    placement.Position += delta;
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
    }
}
