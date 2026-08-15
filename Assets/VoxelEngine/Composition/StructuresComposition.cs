using System;
using Unity.Collections;
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
        /// scene code. Per-voxel work still executes directly inside Structures.Runtime.
        /// </summary>
        public static ArchLookdevBuildResult BuildArchLookdev(
            IVoxelStorageRuntime storage,
            in ArchLookdevBuildRequest request)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (request.BrushBudget <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.BrushBudget));

            var profiles = new ProfileBlockStore();
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = request.ClearSpan,
                PierHeight = request.PierHeight,
                RingThickness = request.RingThickness,
                Depth = request.Depth,
                VoussoirCount = request.VoussoirCount,
                JointRecessDepth = 1,
                ProfileJointHalfWidthQ4 = (byte)request.ProfileJointHalfWidthQ4,
                ProfileBevelQ4 = (byte)request.ProfileBevelQ4,
                ProfileProjectionQ4 = (byte)request.ProfileProjectionQ4,
                ProfileDepthQ4 = (byte)request.ProfileDepthQ4,
                StoneMaterial = request.StoneMaterial,
                PierStyle = request.SurfaceStyle,
                RingStyle = request.SurfaceStyle,
            };
            var bay = new ArchBayFeatureDefinition
            {
                Arch = arch,
                ShoulderWidth = request.ShoulderWidth,
                TopMargin = request.TopMargin,
                FaceRecess = request.FaceRecess,
                PlinthHeight = request.PlinthHeight,
                ImpostHeight = request.ImpostHeight,
                Damage = (ArchRuinDamage)request.Damage,
                DamageSeed = request.DamageSeed,
                DamageScale = (byte)request.DamageScale,
            };

            int3 origin = new(-bay.Width / 2, 0, 0);
            using (var primitives = new NativeList<Primitive>(
                       bay.Metadata.MaxPrimitives,
                       Allocator.Temp))
            {
                if (!bay.Emit(origin, primitives, profiles))
                    throw new InvalidOperationException("Arch parameters did not emit.");

                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(),
                    origin,
                    origin + bay.Metadata.Footprint,
                    storage.Reads,
                    storage.Mutations);
                if (result.BudgetExceeded)
                    throw new InvalidOperationException("Arch exceeded the feature budget.");
            }

            var brush = new VoxelBrush(
                storage.Reads,
                storage.Mutations,
                storage.MaterialAuthoring,
                request.BrushBudget);
            MasonryWeathering.CoatExposedSurfaces(
                ref brush,
                origin - 2,
                bay.Metadata.Footprint + 4,
                request.Coating,
                request.DamageSeed,
                (byte)request.CoatingCoverage,
                dripPasses: 0);

            storage.PublishAllResidentRegions();
            return new ArchLookdevBuildResult(profiles, bay.Width, bay.Height);
        }
    }
}
