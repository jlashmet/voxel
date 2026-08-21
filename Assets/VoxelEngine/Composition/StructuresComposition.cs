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

    /// <summary>Stable retained-profile handle; mutable Runtime storage stays private.</summary>
    public interface IStructureProfileStore : IProfileBlockReadSource
    {
    }

    public readonly struct ReferenceArchBuildResult
    {
        public readonly int3 Min;
        public readonly int3 Max;
        public readonly int VoxelsWritten;

        public ReferenceArchBuildResult(int3 min, int3 max, int voxelsWritten)
        {
            Min = min;
            Max = max;
            VoxelsWritten = voxelsWritten;
        }
    }

    /// <summary>Totals from rasterising an explicit feature catalogue into voxel storage.</summary>
    public readonly struct FeatureCatalogueBuildResult
    {
        public readonly int RegionsVisited;
        public readonly int InstancesRasterised;
        public readonly int VoxelsWritten;

        public FeatureCatalogueBuildResult(
            int regionsVisited,
            int instancesRasterised,
            int voxelsWritten)
        {
            RegionsVisited = regionsVisited;
            InstancesRasterised = instancesRasterised;
            VoxelsWritten = voxelsWritten;
        }
    }

    /// <summary>Application wiring for generic structure authoring.</summary>
    public static class StructuresComposition
    {
        /// <summary>
        /// Wires the hero-arch lookdev request into Structures.Runtime without exposing concrete
        /// feature definitions, profile storage, rasterizers, brushes, or weathering helpers to
        /// scene code. Structure semantics remain outside the reusable engine Runtime.
        /// </summary>
        public static ArchLookdevBuildResult BuildArchLookdev(
            IVoxelStorageRuntime storage,
            in ArchLookdevBuildRequest request)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

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

        public static IStructureProfileStore CreateProfileStore() => new StructureProfileStore();

        /// <summary>
        /// Rasterises the explicit placements in a catalogue through the production feature
        /// evaluator, then publishes the touched storage. This is intended for bounded lookdev and
        /// capture scenes; streamed gameplay should continue to generate one region at a time.
        /// </summary>
        public static FeatureCatalogueBuildResult BuildExplicitFeatureCatalogue(
            IVoxelStorageRuntime storage,
            in FeatureCatalogue catalogue,
            uint seed)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!catalogue.IsCreated)
                throw new ArgumentException("A created catalogue is required.", nameof(catalogue));

            bool hasPlacement = false;
            int3 min = new int3(int.MaxValue);
            int3 max = new int3(int.MinValue);
            for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)catalogue.Definitions.Length) continue;
                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                for (int i = 0; i < rule.ExplicitCount; i++)
                {
                    int placementIndex = rule.ExplicitOffset + i;
                    if ((uint)placementIndex >= (uint)catalogue.ExplicitPlacements.Length) continue;
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    int3 footprint = definition.Footprint;
                    if ((placement.Orientation & 1) != 0)
                        footprint = new int3(footprint.z, footprint.y, footprint.x);
                    int baseY = definition.BasePlane == BasePlaneRule.FixedAltitude
                        ? definition.FixedAltitude
                        : placement.Position.y;
                    int3 origin = new int3(placement.Position.x, baseY, placement.Position.z);
                    min = math.min(min, origin);
                    max = math.max(max, origin + footprint);
                    hasPlacement = true;
                }
            }

            if (!hasPlacement)
                return new FeatureCatalogueBuildResult(0, 0, 0);

            int edge = VoxelGrid.RegionVoxelEdge;
            int3 firstRegion = (int3)math.floor((float3)min / edge);
            int3 lastRegion = (int3)math.floor((float3)(max - 1) / edge);
            int regions = 0, instances = 0, voxels = 0;
            for (int y = firstRegion.y; y <= lastRegion.y; y++)
            for (int z = firstRegion.z; z <= lastRegion.z; z++)
            for (int x = firstRegion.x; x <= lastRegion.x; x++)
            {
                FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                    in catalogue, seed, new int3(x, y, z), storage.Reads, storage.Mutations);
                if (report.BudgetExceeded)
                    throw new InvalidOperationException(
                        "Feature comparison rasterisation exceeded its primitive or voxel budget.");
                regions++;
                instances += report.InstancesRasterised;
                voxels += report.VoxelsWritten;
            }

            storage.PublishAllResidentRegions();
            return new FeatureCatalogueBuildResult(regions, instances, voxels);
        }

        /// <summary>
        /// Creates the generic structure-authoring capability backed by the engine's optimized
        /// runtime brush. Callers own all semantic content and see only Structures.Api; this
        /// method does not know which kind of structure will be authored.
        /// </summary>
        public static IStructureAuthoringSession CreateAuthoringSession(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            int writeBudget = VoxelBrush.DefaultWriteBudget)
        {
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            return new StructureAuthoringSession(reads, mutations, materials, writeBudget);
        }

        public static ReferenceArchBuildResult BuildReferenceArch(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            ISurfaceStyleAuthoringCatalogue surfaces,
            ICoatingAuthoringCatalogue coatings,
            IStructureProfileStore profiles,
            int3 origin,
            byte stoneMaterial,
            ushort pierStyle,
            ushort ringStyle,
            byte coating)
        {
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
            if (coatings == null) throw new ArgumentNullException(nameof(coatings));
            if (!(profiles is StructureProfileStore profileStore))
                throw new ArgumentException("Profiles must be created by StructuresComposition.", nameof(profiles));

            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 64,
                PierHeight = 48,
                RingThickness = 10,
                Depth = 12,
                VoussoirCount = 13,
                StoneMaterial = stoneMaterial,
                PierStyle = pierStyle,
                RingStyle = ringStyle,
                Coating = coating
            };

            var primitives = new NativeList<Primitive>(arch.Metadata.MaxPrimitives, Allocator.Temp);
            try
            {
                ArchValidationError validation = arch.Validate(materials, surfaces, coatings);
                if (validation != ArchValidationError.None || !arch.Emit(origin, primitives, profileStore.Runtime))
                    throw new InvalidOperationException($"The built-in reference arch is invalid: {validation}.");

                int3 max = origin + arch.Metadata.Footprint;
                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), origin, max, reads, mutations);
                return new ReferenceArchBuildResult(origin, max, result.VoxelsWritten);
            }
            finally
            {
                primitives.Dispose();
            }
        }

        private sealed class StructureProfileStore : IStructureProfileStore
        {
            internal ProfileBlockStore Runtime { get; } = new ProfileBlockStore();
            public uint Version => Runtime.Version;
            public int Count => Runtime.Count;
            public ProfileBlock[] Snapshot() => Runtime.Snapshot();
        }
    }
}
