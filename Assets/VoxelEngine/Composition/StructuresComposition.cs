using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-owned parameters for the hero-arch look-development build. The scene owns
    /// all authoring policy; Composition only translates those stable values into the concrete
    /// Structures.Runtime implementation.
    /// </summary>
    public struct ArchLookdevBuildRequest
    {
        public int ClearSpan;
        public int PierHeight;
        public int RingThickness;
        public int Depth;
        public int VoussoirCount;
        public int ShoulderWidth;
        public int TopMargin;
        public int FaceRecess;
        public int PlinthHeight;
        public int ImpostHeight;
        public int Damage;
        public uint DamageSeed;
        public int DamageScale;
        public int ProfileJointHalfWidthQ4;
        public int ProfileBevelQ4;
        public int ProfileProjectionQ4;
        public int ProfileDepthQ4;
        public byte StoneMaterial;
        public ushort SurfaceStyle;
        public byte Coating;
        public int CoatingCoverage;
        public int BrushBudget;
    }

    /// <summary>Stable result of a Composition-owned arch authoring pass.</summary>
    public readonly struct ArchLookdevBuildResult
    {
        public readonly IProfileBlockReadSource ProfileBlocks;
        public readonly int Width;
        public readonly int Height;

        public ArchLookdevBuildResult(
            IProfileBlockReadSource profileBlocks,
            int width,
            int height)
        {
            ProfileBlocks = profileBlocks;
            Width = width;
            Height = height;
        }
    }

    /// <summary>Application wiring for structure planning and authoring.</summary>
    public static class StructuresComposition
    {
        /// <summary>
        /// Draws the deterministic castle plan while keeping the concrete runtime planner private
        /// to Composition. The returned plan is a Structures.Api value contract.
        /// </summary>
        public static CastlePlan PlanCastle(int3 centre, uint seed) =>
            CastleBuilder.Plan(centre, seed);

        /// <summary>
        /// Executes the hero-arch lookdev authoring pass without exposing concrete structure
        /// feature definitions, profile storage, rasterizers, brushes, or weathering helpers to
        /// scene code. Domain work stays in Structures.Runtime; Composition only wires capabilities.
        /// </summary>
        public static ArchLookdevBuildResult BuildArchLookdev(
            IVoxelStorageRuntime storage,
            in ArchLookdevBuildRequest request)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));
            if (request.BrushBudget <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.BrushBudget));

            ArchBayAuthoringPipeline.Author(
                storage.Reads,
                storage.Mutations,
                storage.MaterialAuthoring,
                request.ClearSpan,
                request.PierHeight,
                request.RingThickness,
                request.Depth,
                request.VoussoirCount,
                request.ShoulderWidth,
                request.TopMargin,
                request.FaceRecess,
                request.PlinthHeight,
                request.ImpostHeight,
                request.Damage,
                request.DamageScale,
                request.DamageSeed,
                request.ProfileJointHalfWidthQ4,
                request.ProfileBevelQ4,
                request.ProfileProjectionQ4,
                request.ProfileDepthQ4,
                request.StoneMaterial,
                request.SurfaceStyle,
                request.Coating,
                (byte)request.CoatingCoverage,
                request.BrushBudget,
                out IProfileBlockReadSource profiles,
                out int width,
                out int height);

            storage.PublishAllResidentRegions();
            return new ArchLookdevBuildResult(profiles, width, height);
        }
    }
}
