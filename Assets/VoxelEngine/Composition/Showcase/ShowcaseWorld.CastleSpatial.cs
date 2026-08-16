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
    /// lifecycle and mutation, while this partial owns the planned layout/projection state and all
    /// interaction geometry that would otherwise duplicate castle placement math in application code.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private void PreparePendingCastleSpatialPlan()
        {
            _pendingCastleSpatialPlan = StructuresComposition.PlanCastleSpatial(
                in _pendingCastlePlan, Seed);
            QueuePendingCastleDependencyRegions();
        }

        /// <summary>
        /// Queues every region intersected by the conservative spatial build envelope before any
        /// castle mutation begins. This includes upper Y layers: generating an unqueued Y=1 region
        /// after the keep has already written into it would overwrite the completed structure.
        /// </summary>
        private void QueuePendingCastleDependencyRegions()
        {
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(
                in _pendingCastlePlan, _pendingCastleSpatialPlan);
            int shift = VoxelDimensions.RegionVoxelEdgeLog2;
            int3 minRegion = bounds.Min >> shift;
            int3 maxRegion = (bounds.MaxExclusive - 1) >> shift;

            _castleRegions.Clear();
            for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
            for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
            for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
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
            // QueueLandmarks still carries its historical y=0 dependency loop. Reassert the
            // authoritative spatial envelope at admission so that legacy list cannot permit voxel
            // mutation before upper or offset castle regions have been generated.
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
                        in _world._pendingCastlePlan,
                        _world._pendingCastleSpatialPlan,
                        _world.Seed,
                        _materials);
                }

                return _inner.Step();
            }
        }

        private void CommitPendingCastleSpatialPlan()
        {
            _castlePlan = _pendingCastlePlan;
            _castleSpatialProjection = CastleSpatialProjection.Create(
                in _castlePlan, _pendingCastleSpatialPlan);
            _hasCastlePlan = true;
            _castleTrapdoorOpen = false;
            _castleFrontGateOpen = false;

            ShowcaseCastleSpatialLayout.BuildPresentationLights(
                in _castleSpatialProjection,
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
