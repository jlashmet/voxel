using System;
using Game.Materials.Api;
using Game.Materials.Runtime;
using Game.WorldBuilder.Api;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Composition.Api;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private ShowcaseMaterialSet _materials;

        // Temporary compatibility for the few residual semantic lookups in the legacy showcase
        // implementation. This is deliberately private to the game-owned partial class: the
        // VoxelEngine Structures material facade is gone and must not be reintroduced. Remove
        // this nested shim when ShowcaseWorld.cs is split into smaller game-composition pieces.
        private static class Mat
        {
            public const byte DarkStone = GameMaterialIds.DarkStone;
            public const byte Grass = GameMaterialIds.Grass;
            public const byte MasonrySmall = GameMaterialIds.MasonrySmall;
            public const byte MasonryMedium = GameMaterialIds.MasonryMedium;
            public const byte MasonryLarge = GameMaterialIds.MasonryLarge;
        }

        /// <summary>
        /// Composition entry point for application-owned material definitions and role binding.
        /// Serialized showcase presets are compatibility input only: before any storage/catalogue
        /// backend is selected they are translated to a WorldBuilder semantic environment request
        /// and resolved by <see cref="WorldBuilderEnvironmentComposition"/>.
        /// </summary>
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions,
                             ShowcaseMaterialSet materialRoles,
                             long maxMixedBrickAllocationBytes =
                                 VoxelEngineBootstrap.MaximumMixedBrickAllocationBytes,
                             ShowcaseFeatureContent features = ShowcaseFeatureContent.Full,
                             ShowcaseStartupSource startup = ShowcaseStartupSource.Bake)
        {
            if (materialDefinitions == null)
                throw new ArgumentNullException(nameof(materialDefinitions));

            WorldEnvironmentSpec semanticSpec =
                WorldBuilderEnvironmentComposition.SemanticSpec(seed, features);
            WorldBuilderEnvironmentComposition.Plan composition =
                WorldBuilderEnvironmentComposition.Resolve(in semanticSpec);
            if (composition.Seed != seed)
                throw new InvalidOperationException(
                    "WorldBuilder environment composition changed the deterministic world seed.");
            features = composition.ShowcaseContent;

            Seed = seed;
            LoadRadiusRegions = math.max(1, loadRadiusRegions);
            UnloadRadiusRegions = math.max(LoadRadiusRegions + 1, unloadRadiusRegions);
            _materials = materialRoles;
            _startupSource = startup;

            _storage = new VoxelEngineBootstrap.StorageRuntimeLifetime(
                64, brickPoolCapacity, 4096, maxMixedBrickAllocationBytes);

            for (int i = 0; i < materialDefinitions.Length; i++)
                _palette.Register(in materialDefinitions[i]);
            _materialSimulation = _palette.SimulationView;
            _materialAdjacencyCatalogue = default;

            // CastleOnly leaves the catalogue uncreated on purpose: ShowcaseWorld already treats
            // that as "no settlement content" and skips feature placement wholesale, so there is
            // no second code path to keep in step with this one.
            _includeCastle = features != ShowcaseFeatureContent.HouseOnly;
            _catalogue = features switch
            {
                ShowcaseFeatureContent.CastleOnly => default,
                // Sited where the castle would have stood, which is where the spawn camera aims.
                ShowcaseFeatureContent.HouseOnly => ShowcaseDetailedHouseCatalogue.Build(
                    seed, in materialRoles, Allocator.Persistent,
                    LandmarkCentreX, LandmarkCentreZ),
                _ => ShowcaseCatalogue.Build(seed, in materialRoles, Allocator.Persistent),
            };
        }

        /// <summary>
        /// Game-showcase convenience constructor. Because this composition root is itself game-owned,
        /// it may bind the game's default showcase roles directly without a global engine-side
        /// configuration channel.
        /// </summary>
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions,
                             long maxMixedBrickAllocationBytes =
                                 VoxelEngineBootstrap.MaximumMixedBrickAllocationBytes,
                             ShowcaseFeatureContent features = ShowcaseFeatureContent.Full,
                             ShowcaseStartupSource startup = ShowcaseStartupSource.Bake)
            : this(seed, brickPoolCapacity, loadRadiusRegions, unloadRadiusRegions,
                   materialDefinitions, GameShowcaseMaterials.Default,
                   maxMixedBrickAllocationBytes, features, startup)
        {
        }

        /// <summary>
        /// Creates a Structures.Api authoring capability against this world's owned storage and
        /// mutable material catalogue. The mutable catalogue deliberately remains private to the
        /// composition root; consumers receive only the semantic authoring session.
        /// </summary>
        public IStructureAuthoringSession CreateStructureAuthoringSession(int writeBudget)
        {
            return StructuresComposition.CreateAuthoringSession(
                ReadStorage,
                MutationStorage,
                _palette,
                writeBudget);
        }
    }
}
