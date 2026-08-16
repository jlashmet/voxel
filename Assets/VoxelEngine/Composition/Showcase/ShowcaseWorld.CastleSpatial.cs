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

        private ICastleBuildSession BeginPendingSpatialCastleBuild(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials) =>
            StructuresComposition.BeginCastleBuild(
                reads,
                mutations,
                in _pendingCastlePlan,
                _pendingCastleSpatialPlan,
                Seed,
                materials);

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
