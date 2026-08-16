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

            ShowcaseCastleSpatialLayout.BuildPresentationLights(
                in _castleSpatialProjection,
                out Vector4[] lights,
                out Vector4[] colours);
            CastlePresentationLights = lights;
            CastlePresentationLightColours = colours;
        }

        private Vector3 ActiveCastleFrontGatePosition()
        {
            if (!_hasCastleSpatialProjection)
            {
                CastleGateGeometry legacy = CastleGateGeometryResolver.LegacyFront(in _castlePlan);
                float3 legacyPoint = legacy.InteractionPointVoxels;
                return new Vector3(legacyPoint.x, legacyPoint.y, legacyPoint.z) * VoxelSize;
            }

            return ShowcaseCastleSpatialLayout.PrimaryGateInteractionPosition(
                in _castleSpatialProjection, VoxelSize);
        }

        private void OpenActiveCastleFrontGate()
        {
            if (!_hasCastleSpatialProjection)
            {
                CastleGateGeometry legacy = CastleGateGeometryResolver.LegacyFront(in _castlePlan);
                var legacyVoxels = new List<FallingVoxel>(
                    legacy.Width * legacy.Height * legacy.Depth);
                for (int d = 0; d < legacy.Depth; d++)
                for (int w = 0; w < legacy.Width; w++)
                for (int h = 0; h < legacy.Height; h++)
                {
                    if (!legacy.ContainsArchVoxel(w, h)) continue;
                    legacyVoxels.Add(new FallingVoxel
                    {
                        Position = legacy.WorldVoxel(w, h, d),
                        Material = MatWood,
                    });
                }
                ClearVoxelsBulk(legacyVoxels);
                return;
            }

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

        private int3 ActiveCastleTrapdoorCentre()
        {
            if (_hasCastleSpatialProjection)
                return ShowcaseCastleSpatialLayout.TrapdoorCentre(in _castleSpatialProjection);
            return CastleLayout.TrapdoorCentre(in _castlePlan);
        }

        private Vector3 ActiveCastleTrapdoorPosition()
        {
            if (_hasCastleSpatialProjection)
            {
                return ShowcaseCastleSpatialLayout.TrapdoorInteractionPosition(
                    in _castleSpatialProjection, VoxelSize);
            }

            int3 centre = CastleLayout.TrapdoorCentre(in _castlePlan);
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
