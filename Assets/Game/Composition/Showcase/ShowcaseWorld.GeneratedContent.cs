using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Generates the origin terrain needed to place the player and queue the showcase castle,
        /// but deliberately does not run the generic feature catalogue in the region the castle
        /// owns. The normal blocking generator used to queue landmarks at the end of terrain and
        /// then immediately rasterise the origin's generic features anyway. That made the castle's
        /// pre-authoring input differ from the other eight castle regions and forced async castle
        /// authoring to serialize the whole live region just to preserve accidental dressing.
        ///
        /// Castle-owned regions are now plain deterministic terrain until the castle commits, which
        /// matches the streaming path's existing deferred-feature rule and lets the worker recreate
        /// the exact source from the world seed without touching live storage.
        /// </summary>
        public void GenerateCastleOriginBlocking()
        {
            int3 regionCoord = int3.zero;
            if (_generated.Contains(regionCoord)) return;
            if (_gen.Active) FinishRegionForced();

            BeginRegion(regionCoord);
            while (!StepRegion()) { }
            FinishRegion();

            if (!_castleTerrainQueued || !_castleRegions.Contains(regionCoord))
                throw new InvalidOperationException(
                    "Origin generation did not establish the showcase castle footprint.");

            // FinishRegion had to make the castle plan first, so it could not know this was a
            // castle-owned region when it made its feature-queue decision. Remove that one stale
            // queue entry before the blocking caller can rasterise it.
            _pendingFeatureRegions.Remove(regionCoord);
        }

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
