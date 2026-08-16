using System;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Replaces the showcase feature catalogue with production-generated gameplay content.
        /// This must happen on a fresh world before any region is generated. Ownership of the
        /// supplied catalogue transfers to this world and it is disposed with the world.
        ///
        /// ShowcaseWorld still contains the showcase castle bootstrap because the streaming,
        /// storage, collision, and rendering composition currently live in the same application
        /// world. Marking the landmark lifecycle complete keeps that demo-only castle out of
        /// production worlds until the shared streaming world is extracted from the showcase.
        /// </summary>
        public void ConfigureGeneratedContentForGameplay(FeatureCatalogue catalogue)
        {
            if (!catalogue.IsCreated)
                throw new ArgumentException("Gameplay generated content requires a created feature catalogue.", nameof(catalogue));
            if (_generated.Count != 0 || _gen.Active || RegionsGenerated != 0
                || _pendingLoads.Count != 0 || _pendingFeatureRegions.Count != 0
                || _featureBuild != null)
                throw new InvalidOperationException(
                    "Gameplay generated content must be configured before any world generation begins.");

            if (_catalogue.IsCreated)
                _catalogue.Dispose();
            _catalogue = catalogue;

            _castleBuild = null;
            _castleRegions.Clear();
            _deferredFeatureRegions.Clear();
            _castleTerrainQueued = true;
            _hasCastlePlan = true;
            _castleTrapdoorOpen = false;
            _castleFrontGateOpen = false;
        }
    }
}
