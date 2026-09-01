using System;
using Game.Structures.Api;
using Game.Structures.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using EnginePresentationComposition = VoxelEngine.Composition.StructurePresentationComposition;
using GameCastlePlan = Game.Structures.Api.CastlePlan;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private const ulong CastlePresentationSourceDomain = 0x434153544C455052ul; // "CASTLEPR"

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
            IStructurePresentationCaptureSession capture =
                EnginePresentationComposition.CreateCaptureSession(
                    (x, y, z) => MaterialAt(y, SurfaceHeight(x, z)));

            // Replay the same canonical semantic authoring recipe used by the detailed castle build.
            // The capture target is nonresident and records only coarse presentation semantics, so
            // this cannot allocate or generate any detailed voxel region.
            GameCastlePlan gamePlan = plan.Value;
            var build = new CastleAuthoringBuild(capture, in gamePlan, Seed);
            while (!build.Step()) { }

            ulong sourceId = MixCastlePresentationIdentity(
                CastlePresentationSourceDomain
                ^ unchecked((uint)plan.Centre.x)
                ^ ((ulong)unchecked((uint)plan.Centre.y) << 21)
                ^ ((ulong)unchecked((uint)plan.Centre.z) << 42));
            ulong revisionSeed = MixCastlePresentationIdentity(
                CastlePresentationSourceDomain ^ Seed ^ plan.Seed);
            _featurePresentation.Upsert(capture.Bake(
                sourceId,
                revisionSeed,
                FeatureKind.Structure,
                plan.Centre,
                0));
            _castlePresentationCaptured = true;
        }

        private static ulong MixCastlePresentationIdentity(ulong value)
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9ul;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBul;
            return value ^ (value >> 31);
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
