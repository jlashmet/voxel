using Unity.Mathematics;
using VoxelEngine.Storage.Api;
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

        /// <summary>
        /// Authors one deterministic masonry arch bay while keeping all concrete Structures.Runtime
        /// implementation types behind the Composition boundary. Retained profile output is exposed
        /// only through the Storage.Api read capability consumed by Rendering.
        /// </summary>
        public static void AuthorArchBay(
            IVoxelStorageRuntime storage,
            int clearSpan,
            int pierHeight,
            int ringThickness,
            int depth,
            int voussoirCount,
            int shoulderWidth,
            int topMargin,
            int faceRecess,
            int plinthHeight,
            int impostHeight,
            int damage,
            int damageScale,
            uint seed,
            int profileJointHalfWidthQ4,
            int profileBevelQ4,
            int profileProjectionQ4,
            int profileDepthQ4,
            byte stoneMaterial,
            ushort surfaceStyle,
            byte weatheringCoating,
            byte weatheringCoverage,
            int writeBudget,
            out IProfileBlockReadSource profiles,
            out int width,
            out int height)
        {
            if (storage == null)
                throw new System.ArgumentNullException(nameof(storage));

            ArchBayAuthoringPipeline.Author(
                storage.Reads,
                storage.Mutations,
                storage.MaterialAuthoring,
                clearSpan,
                pierHeight,
                ringThickness,
                depth,
                voussoirCount,
                shoulderWidth,
                topMargin,
                faceRecess,
                plinthHeight,
                impostHeight,
                damage,
                damageScale,
                seed,
                profileJointHalfWidthQ4,
                profileBevelQ4,
                profileProjectionQ4,
                profileDepthQ4,
                stoneMaterial,
                surfaceStyle,
                weatheringCoating,
                weatheringCoverage,
                writeBudget,
                out profiles,
                out width,
                out height);
        }
    }
}
