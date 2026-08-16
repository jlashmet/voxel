using System;
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

        /// <summary>
        /// Composition entry point for application-owned material definitions and role binding.
        /// This constructor initializes the world directly: it deliberately does not chain through
        /// the legacy constructor that still contains the pre-migration hardcoded demo palette.
        /// </summary>
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions,
                             ShowcaseMaterialSet materialRoles)
        {
            if (materialDefinitions == null)
                throw new ArgumentNullException(nameof(materialDefinitions));

            Seed = seed;
            LoadRadiusRegions = math.max(1, loadRadiusRegions);
            UnloadRadiusRegions = math.max(LoadRadiusRegions + 1, unloadRadiusRegions);
            _materials = materialRoles;

            _storage = new VoxelEngineBootstrap.StorageRuntimeLifetime(
                64, brickPoolCapacity, 4096);

            for (int i = 0; i < materialDefinitions.Length; i++)
                _palette.Register(in materialDefinitions[i]);
            _materialSimulation = _palette.SimulationView;
            _materialAdjacencyCatalogue = default;

            _catalogue = ShowcaseCatalogue.Build(seed, in materialRoles, Allocator.Persistent);
        }

        /// <summary>
        /// Compatibility overload for callers that rely on application composition. It deliberately
        /// has no numeric fallback: an engine world must never invent the game's material identities.
        /// </summary>
        [Obsolete("Prefer the overload with an explicit ShowcaseMaterialSet.")]
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions)
            : this(seed, brickPoolCapacity, loadRadiusRegions, unloadRadiusRegions,
                   materialDefinitions, RequireConfiguredRoles())
        {
        }

        private static ShowcaseMaterialSet RequireConfiguredRoles()
        {
            if (ShowcaseMaterialComposition.TryGet(out ShowcaseMaterialSet roles))
                return roles;

            throw new InvalidOperationException(
                "Showcase material roles were not configured. Pass a ShowcaseMaterialSet explicitly " +
                "or configure ShowcaseMaterialComposition from application/game composition before " +
                "constructing ShowcaseWorld.");
        }
    }
}
