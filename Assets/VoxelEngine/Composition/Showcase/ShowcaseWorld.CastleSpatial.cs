using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Spatial-castle activation seam for the showcase. The large streaming world keeps owning
    /// lifecycle and mutation, while this partial owns the runtime-ready planning bundle and all
    /// interaction geometry that would otherwise duplicate castle placement math in application code.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private void PreparePendingCastleSpatialPlan()
        {
            _pendingPlannedCastle = StructuresComposition.PlanCastleBuild(
                _pendingCastlePlan.Centre,
                _pendingCastlePlan.Seed,
                Seed);
            _pendingCastlePlan = _pendingPlannedCastle.Dimensions;
            QueuePendingCastleDependencyRegions();
        }

        /// <summary>
        /// Queues every region intersected by the conservative spatial build envelope before any
        /// castle mutation begins. This includes upper and negative Y layers: generating an
        /// unqueued region after the castle has already written into it would overwrite the
        /// completed structure.
        /// </summary>
        private void QueuePendingCastleDependencyRegions()
        {
            CastlePlan dimensions = _pendingPlannedCastle.Dimensions;
            CastleSpatialPlan spatial = _pendingPlannedCastle.Spatial;
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(
                in dimensions, spatial);
            ShowcaseCastleDependencyRegionRange regionRange =
                ShowcaseCastleDependencyRegionRange.FromCastleBounds(in bounds);

            _castleRegions.Clear();
            for (int rz = regionRange.Min.z; rz <= regionRange.MaxInclusive.z; rz++)
            for (int ry = regionRange.Min.y; ry <= regionRange.MaxInclusive.y; ry++)
            for (int rx = regionRange.Min.x; rx <= regionRange.MaxInclusive.x; rx++)
                _castleRegions.Add(new int3(rx, ry, rz));
        }

        private bool PendingCastleDependenciesReady()
        {
            for (int i = 0; i < _castleRegions.Count; i++)
                if (!_generated.Contains(_castleRegions[i])) return false;
            return true;
        }

        private ICastleBuildSession BeginPendingSpatialCastleBuild(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials)
        {
            // Reassert the authoritative spatial envelope at admission so mutation cannot start
            // after a caller or future planner changed dependency geometry without refreshing the
            // queue. The runtime-ready bundle keeps that geometry tied to its terrain seed.
            QueuePendingCastleDependencyRegions();
            return new DependencyGatedCastleBuildSession(this, materials);
        }

        /// <summary>
        /// Quiescent gate between dependency streaming and mutation ownership. Before the real
        /// pipeline exists IsComplete intentionally reports true so StepStreaming keeps servicing
        /// region loads instead of granting exclusive castle-write ownership too early. Once every
        /// dependency is resident, the next landmark step creates the real session and normal
        /// incremental ownership semantics take over.
        /// </summary>
        private sealed class DependencyGatedCastleBuildSession : ICastleBuildSession
        {
            private readonly ShowcaseWorld _world;
            private readonly IMaterialAuthoringCatalogue _materials;
            private ICastleBuildSession _inner;

            internal DependencyGatedCastleBuildSession(
                ShowcaseWorld world,
                IMaterialAuthoringCatalogue materials)
            {
                _world = world;
                _materials = materials;
            }

            public bool IsComplete => _inner == null || _inner.IsComplete;
            public int StageNumber => _inner != null ? _inner.StageNumber : 0;
            public long TotalVoxelsWritten => _inner != null ? _inner.TotalVoxelsWritten : 0L;

            public bool Step()
            {
                if (_inner == null)
                {
                    if (!_world.PendingCastleDependenciesReady())
                        return false;

                    var readyReads = _world._readSource;
                    var readyMutations = _world._mutationStore;
                    readyReads.Refresh(in _world._table, in _world._pool);
                    readyMutations.Refresh(in _world._table, in _world._pool);
                    _inner = StructuresComposition.BeginCastleBuild(
                        readyReads,
                        readyMutations,
                        in _world._pendingPlannedCastle,
                        _materials);
                }

                return _inner.Step();
            }
        }

        private void CommitPendingCastleSpatialPlan()
        {
            _plannedCastle = _pendingPlannedCastle;
            _castlePlan = _plannedCastle.Dimensions;
            _castleSpatialProjection = _plannedCastle.Projection;
            _hasCastlePlan = true;
            _castleTrapdoorOpen = false;
            _castleFrontGateOpen = false;

            ShowcaseCastleSpatialLayout.BuildPresentationLights(
                in _castleSpatialProjection,
                _plannedCastle.Spatial.Dungeon,
                out Vector4[] lights,
                out Vector4[] colours);
            CastlePresentationLights = lights;
            CastlePresentationLightColours = colours;
        }

        private Vector3 ActiveCastleFrontGatePosition() =>
            ShowcaseCastleSpatialLayout.PrimaryGateInteractionPosition(
                in _castleSpatialProjection, VoxelSize);

        private void OpenActiveCastleFrontGate()
        {
            int3[] voxels = ShowcaseCastleSpatialLayout.PrimaryGateLeafVoxels(
                in _castleSpatialProjection);
            var gateVoxels = new List<FallingVoxel>(voxels.Length);
            for (int i = 0; i < voxels.Length; i++)
            {
                gateVoxels.Add(new FallingVoxel
                {
                    Position = voxels[i],
                    Material = MatWood,
                });
            }
            ClearVoxelsBulk(gateVoxels);
        }

        private int3 ActiveCastleTrapdoorCentre() =>
            ShowcaseCastleSpatialLayout.TrapdoorCentre(in _castleSpatialProjection);

        private Vector3 ActiveCastleTrapdoorPosition() =>
            ShowcaseCastleSpatialLayout.TrapdoorInteractionPosition(
                in _castleSpatialProjection, VoxelSize);

        private void OpenActiveCastleTrapdoor()
        {
            int3 centre = ActiveCastleTrapdoorCentre();
            int half = CastleLayout.TrapdoorHalfSize;
            for (int y = centre.y; y < centre.y + 4; y++)
            for (int z = centre.z - half; z < centre.z + half; z++)
            for (int x = centre.x - half; x < centre.x + half; x++)
            {
                var voxel = new int3(x, y, z);
                if (SetMaterialApi(voxel, VoxelGrid.MaterialEmpty))
                    MarkDirty(voxel);
            }
        }
    }
}
