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
        private CastleSpatialPlan _pendingCastleSpatialPlan;
        private CastleSpatialPlan _castleSpatialPlan;
        private CastleSpatialProjection _castleSpatialProjection;
        private bool _hasCastleSpatialProjection;

        private void PreparePendingCastleSpatialPlan()
        {
            _pendingCastleSpatialPlan = StructuresComposition.PlanCastleSpatial(
                in _pendingCastlePlan, Seed);
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
            _castleSpatialPlan = _pendingCastleSpatialPlan;
            _castleSpatialProjection = CastleSpatialProjection.Create(
                in _castlePlan, _castleSpatialPlan);
            _hasCastleSpatialProjection = true;
            _hasCastlePlan = true;
            _castleTrapdoorOpen = false;
            _castleFrontGateOpen = false;

            CastlePlan presentationPlan = _castleSpatialProjection.KeepPlan;
            BuildCastlePresentationLights(in presentationPlan);
        }

        private CastleGateGeometry ActiveCastleFrontGateGeometry()
        {
            if (_hasCastleSpatialProjection)
                return _castleSpatialProjection.PrimaryGateGeometry;
            return CastleGateGeometryResolver.LegacyFront(in _castlePlan);
        }

        private Vector3 ActiveCastleFrontGatePosition()
        {
            float3 point = ActiveCastleFrontGateGeometry().InteractionPointVoxels;
            return new Vector3(point.x, point.y, point.z) * VoxelSize;
        }

        private void OpenActiveCastleFrontGate()
        {
            CastleGateGeometry geometry = ActiveCastleFrontGateGeometry();
            var gateVoxels = new List<FallingVoxel>(
                geometry.Width * geometry.Height * geometry.Depth);

            for (int d = 0; d < geometry.Depth; d++)
            for (int w = 0; w < geometry.Width; w++)
            for (int h = 0; h < geometry.Height; h++)
            {
                if (!geometry.ContainsArchVoxel(w, h)) continue;
                gateVoxels.Add(new FallingVoxel
                {
                    Position = geometry.WorldVoxel(w, h, d),
                    Material = MatWood,
                });
            }

            ClearVoxelsBulk(gateVoxels);
        }

        private int3 ActiveCastleTrapdoorCentre()
        {
            if (_hasCastleSpatialProjection)
                return _castleSpatialProjection.TrapdoorCentre;
            return CastleLayout.TrapdoorCentre(in _castlePlan);
        }

        private Vector3 ActiveCastleTrapdoorPosition()
        {
            int3 centre = ActiveCastleTrapdoorCentre();
            return ((Vector3)(float3)centre + new Vector3(0.5f, 0.2f, 0.5f)) * VoxelSize;
        }

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
