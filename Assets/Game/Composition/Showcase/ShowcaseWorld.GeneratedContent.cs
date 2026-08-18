using System;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Restores the pre-generated showcase startup world.
        ///
        /// This method intentionally keeps its old name because the scene driver's Spawn path
        /// already calls it before touching the player. It no longer generates terrain or runs
        /// castle authoring during play. The expensive source generation lives exclusively behind
        /// <see cref="GenerateForBakeBlocking"/> and the editor bake command.
        /// </summary>
        public void GenerateCastleOriginBlocking()
        {
            // Respawn/re-entry after the startup image has already been installed is free, but the
            // gameplay scene may have deliberately unloaded presentation/runtime state. Re-establish
            // the authoritative WorldObject scene before returning.
            if (_hasCastlePlan && _generated.Contains(int3.zero))
            {
                EnsureCastleWorldObjectSceneLoaded();
                return;
            }

            if (_generated.Count != 0 || _gen.Active || RegionsGenerated != 0
                || _pendingLoads.Count != 0 || _pendingFeatureRegions.Count != 0
                || _featureBuild != null || _castleBuild != null || _castleTerrainQueued
                || _hasCastlePlan)
                throw new InvalidOperationException(
                    "The baked showcase world must be loaded before runtime world generation starts.");

            TextAsset asset = Resources.Load<TextAsset>(ShowcaseWorldBakeCodec.ResourcePath);
            if (asset == null)
                throw new InvalidOperationException(
                    "The Voxel Showcase startup bake is missing. Run " +
                    "Tools > Voxel Engine > Bake Showcase World before entering Play mode. " +
                    "Runtime generation is deliberately disabled for the showcase startup path.");

            ShowcaseWorldBake bake;
            try
            {
                bake = ShowcaseWorldBakeCodec.Deserialize(asset.bytes);
            }
            catch (Exception ex) when (ex is InvalidDataException
                                       || ex is EndOfStreamException
                                       || ex is ArgumentException)
            {
                throw new InvalidOperationException(
                    "The Voxel Showcase startup bake is invalid or stale. Re-run " +
                    "Tools > Voxel Engine > Bake Showcase World.", ex);
            }

            LoadBake(bake);
            EnsureCastleWorldObjectSceneLoaded();
        }

        /// <summary>
        /// Builds only the source origin used by the offline baker. Keeping this separate from the
        /// runtime compatibility entry point above makes it impossible for Play mode to silently
        /// fall back to procedural castle generation when a bake is absent.
        /// </summary>
        private void GenerateCastleOriginForBakeBlocking()
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
            // queue entry before the blocking baker can rasterise it.
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
