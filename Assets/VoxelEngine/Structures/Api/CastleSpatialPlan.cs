using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleTowerPlacementRole : byte
    {
        Corner,
        Wall,
    }

    public struct CastleTowerPlacementSpec
    {
        public int Id;
        public int2 Centre;
        public CastleTowerPlacementRole Role;
    }

    public struct CastleGatePlacementSpec
    {
        public int EdgeIndex;
        public int2 Centre;
        public float2 Outward;
    }

    /// <summary>
    /// Spatial planning result expressed in local X/Z coordinates relative to CastlePlan.Centre.
    /// It is still pure data: no voxel storage, materials, rendering, or runtime mutation state.
    /// </summary>
    public sealed class CastleSpatialPlan
    {
        public CastleTopologyPlan Topology { get; }
        public int2[] OuterWardVertices { get; }
        public int2[] InnerWardVertices { get; }
        public CastleTowerPlacementSpec[] Towers { get; }
        public CastleGatePlacementSpec PrimaryGate { get; }
        public bool HasInnerGate { get; }
        public CastleGatePlacementSpec InnerGate { get; }
        public int2 KeepCentre { get; }
        public bool KeepRequiresTerrainResolution { get; }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
        {
            Topology = topology;
            OuterWardVertices = outerWardVertices;
            InnerWardVertices = innerWardVertices;
            Towers = towers;
            PrimaryGate = primaryGate;
            HasInnerGate = hasInnerGate;
            InnerGate = innerGate;
            KeepCentre = keepCentre;
            KeepRequiresTerrainResolution = keepRequiresTerrainResolution;
        }
    }
}
