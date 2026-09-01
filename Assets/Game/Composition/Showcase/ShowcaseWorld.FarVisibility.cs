using System;
using Game.Structures.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private readonly FeaturePresentationManifest _featurePresentation = new();
        private bool _featurePresentationSeeded;
        private bool _castlePresentationCaptured;

        /// <summary>
        /// Generic derived presentation source for planned/generated world features. Accessing the
        /// source is allowed before streaming starts: canonical catalogue features and the planned
        /// castle are baked without allocating or generating any detailed voxel region.
        /// </summary>
        public IFeaturePresentationSource FeaturePresentation
        {
            get
            {
                EnsureFeaturePresentationReady();
                return _featurePresentation;
            }
        }

        private void EnsureFeaturePresentationReady()
        {
            if (!_featurePresentationSeeded)
            {
                FeaturePresentationCatalogueBaker.Populate(
                    in _catalogue, Seed, _featurePresentation);
                _featurePresentationSeeded = true;
            }

            if (!_includeCastle) return;

            // QueueLandmarks only plans and enumerates dependencies. It does not generate terrain
            // or begin the detailed castle build, so presentation queries remain nonresident.
            if (!_castleTerrainQueued && !_hasCastlePlan)
                QueueLandmarks();

            if (_castlePresentationCaptured || !_castleTerrainQueued) return;

            CastlePlan plan = _hasCastlePlan ? _castlePlan : _pendingCastlePlan;
            FeaturePresentationBake bake = ShowcaseStructurePresentation.BakeCastle(
                in plan,
                Seed,
                (x, y, z) => MaterialAt(y, SurfaceHeight(x, z)));
            _featurePresentation.Upsert(bake);
            _castlePresentationCaptured = true;
        }

        /// <summary>
        /// Legacy semantic event retained until T013 removes the superseded castle-specific
        /// visibility path. New far-world presentation must consume <see cref="FeaturePresentation"/>.
        /// </summary>
        public static event Action<ShowcaseWorld, CastlePlan> CastlePlanned;

        /// <summary>
        /// Returns the exact deterministic castle plan already owned by this world as soon as
        /// landmark planning has completed. This compatibility surface is not used by the generic
        /// presentation source and is scheduled for removal with the old far-visibility path.
        /// </summary>
        public bool TryGetPlannedCastle(out CastlePlan plan)
        {
            if (_castleTerrainQueued)
            {
                plan = _hasCastlePlan ? _castlePlan : _pendingCastlePlan;
                return true;
            }

            plan = default;
            return false;
        }

        private void PublishCastlePlanned(in CastlePlan plan) => CastlePlanned?.Invoke(this, plan);
    }
}
