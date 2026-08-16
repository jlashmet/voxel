using System;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Composition entry point for a game-owned material vocabulary. The legacy four-argument
        /// constructor remains temporarily for existing callers, but this overload replaces its
        /// built-in palette before the world becomes observable.
        /// </summary>
        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions, MaterialDefinition[] materialDefinitions)
            : this(seed, brickPoolCapacity, loadRadiusRegions, unloadRadiusRegions)
        {
            if (materialDefinitions == null)
                throw new ArgumentNullException(nameof(materialDefinitions));

            _palette.Clear();
            for (int i = 0; i < materialDefinitions.Length; i++)
                _palette.Register(in materialDefinitions[i]);

            _materialSimulation = _palette.SimulationView;
        }
    }
}
