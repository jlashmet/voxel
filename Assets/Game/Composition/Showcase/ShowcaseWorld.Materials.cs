using System;
using Game.Materials.Api;
using Game.Materials.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Composition.Api;
using VoxelEngine.Storage.Api;

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
        /// This constructor initializes the world directly: it deliberately does not chain through
        /// the legacy constructor that still contains the pre-migration hardcoded demo palette.
        /// </summary>
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions,
                             ShowcaseMaterialSet materialRoles,
                             int maxMixedBrickAllocationBytes =
                                 VoxelEngineBootstrap.MaximumMixedBrickAllocationBytes)
        {
            if (materialDefinitions == null)
                throw new ArgumentNullException(nameof(materialDefinitions));

            Seed = seed;
            LoadRadiusRegions = math.max(1, loadRadiusRegions);
            UnloadRadiusRegions = math.max(LoadRadiusRegions + 1, unloadRadiusRegions);
            _materials = materialRoles;

            _storage = new VoxelEngineBootstrap.StorageRuntimeLifetime(
                64, brickPoolCapacity, 4096, maxMixedBrickAllocationBytes);

            for (int i = 0; i < materialDefinitions.Length; i++)
                _palette.Register(in materialDefinitions[i]);
            _materialSimulation = _palette.SimulationView;
            _materialAdjacencyCatalogue = default;

            _catalogue = ShowcaseCatalogue.Build(seed, in materialRoles, Allocator.Persistent);
        }

        /// <summary>
        /// Game-showcase convenience constructor. Because this composition root is itself game-owned,
        /// it may bind the game's default showcase roles directly without a global engine-side
        /// configuration channel.
        /// </summary>
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions,
                             int maxMixedBrickAllocationBytes =
                                 VoxelEngineBootstrap.MaximumMixedBrickAllocationBytes)
            : this(seed, brickPoolCapacity, loadRadiusRegions, unloadRadiusRegions,
                   materialDefinitions, GameShowcaseMaterials.Default,
                   maxMixedBrickAllocationBytes)
        {
        }
    }
}
