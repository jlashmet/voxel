using System;
using Unity.Collections;
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
        /// The world consumes only opaque indices and generic properties.
        /// </summary>
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions,
                             ShowcaseMaterialSet materialRoles)
            : this(seed, brickPoolCapacity, loadRadiusRegions, unloadRadiusRegions)
        {
            if (materialDefinitions == null)
                throw new ArgumentNullException(nameof(materialDefinitions));

            _materials = materialRoles;
            _palette.Clear();
            for (int i = 0; i < materialDefinitions.Length; i++)
                _palette.Register(in materialDefinitions[i]);
            _materialSimulation = _palette.SimulationView;

            // Replace the legacy base catalogue before the world becomes observable so active
            // generation is always driven by the application-owned role binding.
            if (_catalogue.IsCreated) _catalogue.Dispose();
            _catalogue = ShowcaseCatalogue.Build(seed, in materialRoles, Allocator.Persistent);
        }

        /// <summary>
        /// Compatibility overload used by the existing showcase driver. Normal game startup
        /// configures roles before scene load, so this path is game-owned in production. The
        /// numeric fallback exists only for isolated legacy/editor harnesses that skip bootstrap.
        /// </summary>
        [Obsolete("Prefer the overload with an explicit ShowcaseMaterialSet.")]
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions)
            : this(seed, brickPoolCapacity, loadRadiusRegions, unloadRadiusRegions,
                   materialDefinitions, ConfiguredOrLegacyRoles())
        {
        }

        private static ShowcaseMaterialSet ConfiguredOrLegacyRoles()
        {
            if (ShowcaseMaterialComposition.TryGet(out ShowcaseMaterialSet roles))
                return roles;
            return LegacyCompatibilityRoles();
        }

        private static ShowcaseMaterialSet LegacyCompatibilityRoles()
        {
            const uint structuralMask = (1u << 2) | (1u << 4) | (1u << 6) | (1u << 7)
                                      | (1u << 8) | (1u << 9) | (1u << 12) | (1u << 15);
            return new ShowcaseMaterialSet(
                terrainDeep: 5,
                terrainSubsurface: 1,
                terrainLowSurface: 3,
                terrainHighSurface: 10,
                gate: 2,
                referenceArch: 6,
                farStructure: 1,
                worldgenFoundation: 1,
                worldgenMasonry: 1,
                worldgenDarkMasonry: 6,
                worldgenTimber: 2,
                worldgenGlass: 4,
                worldgenWarmWindow: 15,
                worldgenRoofTile: 8,
                worldgenSlate: 7,
                worldgenCloth: 9,
                worldgenMoss: 14,
                worldgenWater: 11,
                worldgenRoadSurface: 13,
                structuralMask: structuralMask);
        }
    }
}
