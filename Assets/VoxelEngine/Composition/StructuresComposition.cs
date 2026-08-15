using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>Application wiring for structure planning backed by Structures.Runtime.</summary>
    public static partial class StructuresComposition
    {
        /// <summary>
        /// Draws the deterministic castle plan while keeping the concrete runtime planner private
        /// to Composition. The returned plan is a Structures.Api value contract.
        /// </summary>
        public static CastlePlan PlanCastle(int3 centre, uint seed) =>
            CastleBuilder.Plan(centre, seed);
    }
}
