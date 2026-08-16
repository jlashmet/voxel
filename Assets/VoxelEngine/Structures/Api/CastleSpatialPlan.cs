using System;
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
        public int HeightVariation;
        public bool HasRoof;
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
        private readonly CastleTowerPlacementSpec[] _innerTowers;

        public CastleTopologyPlan Topology { get; }
        public int2[] OuterWardVertices { get; }
        public int2[] InnerWardVertices { get; }
        public CastleTowerPlacementSpec[] Towers { get; }
        public CastleTowerPlacementSpec[] InnerTowers => _innerTowers;
        public CastleGatePlacementSpec PrimaryGate { get; }
        public bool HasPosternGate { get; }
        public CastleGatePlacementSpec PosternGate { get; }
        public bool HasInnerGate { get; }
        public CastleGatePlacementSpec InnerGate { get; }
        public bool HasWell { get; }
        public int2 WellCentre { get; }
        public CastleCourtyardBuildingSpec[] CourtyardBuildings { get; }
        public CastleKeepFloorPlan[] KeepFloors { get; }
        public CastleKeepCirculationPlan KeepCirculation { get; }
        public DungeonPlan Dungeon { get; }
        public CavePlan Cave { get; }
        public int2 KeepCentre { get; }
        public bool KeepRequiresTerrainResolution { get; }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
            : this(
                in topology,
                outerWardVertices,
                innerWardVertices,
                towers,
                in primaryGate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                false,
                default,
                Array.Empty<CastleCourtyardBuildingSpec>(),
                null,
                keepCentre,
                keepRequiresTerrainResolution)
        {
        }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            bool hasWell,
            int2 wellCentre,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
            : this(
                in topology,
                outerWardVertices,
                innerWardVertices,
                towers,
                in primaryGate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                Array.Empty<CastleCourtyardBuildingSpec>(),
                null,
                keepCentre,
                keepRequiresTerrainResolution)
        {
        }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            bool hasWell,
            int2 wellCentre,
            CastleCourtyardBuildingSpec[] courtyardBuildings,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
            : this(
                in topology,
                outerWardVertices,
                innerWardVertices,
                towers,
                in primaryGate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                courtyardBuildings,
                null,
                keepCentre,
                keepRequiresTerrainResolution)
        {
        }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            bool hasWell,
            int2 wellCentre,
            CastleCourtyardBuildingSpec[] courtyardBuildings,
            DungeonPlan dungeon,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
            : this(
                in topology,
                outerWardVertices,
                innerWardVertices,
                towers,
                in primaryGate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                courtyardBuildings,
                dungeon,
                null,
                keepCentre,
                keepRequiresTerrainResolution)
        {
        }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            bool hasWell,
            int2 wellCentre,
            CastleCourtyardBuildingSpec[] courtyardBuildings,
            DungeonPlan dungeon,
            CavePlan cave,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
            : this(
                in topology,
                outerWardVertices,
                innerWardVertices,
                towers,
                in primaryGate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                courtyardBuildings,
                Array.Empty<CastleKeepFloorPlan>(),
                dungeon,
                cave,
                keepCentre,
                keepRequiresTerrainResolution)
        {
        }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            bool hasWell,
            int2 wellCentre,
            CastleCourtyardBuildingSpec[] courtyardBuildings,
            CastleKeepFloorPlan[] keepFloors,
            DungeonPlan dungeon,
            CavePlan cave,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
            : this(
                in topology,
                outerWardVertices,
                innerWardVertices,
                towers,
                in primaryGate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                courtyardBuildings,
                keepFloors,
                default,
                dungeon,
                cave,
                keepCentre,
                keepRequiresTerrainResolution)
        {
        }

        internal CastleSpatialPlan(
            in CastleTopologyPlan topology,
            int2[] outerWardVertices,
            int2[] innerWardVertices,
            CastleTowerPlacementSpec[] towers,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            bool hasWell,
            int2 wellCentre,
            CastleCourtyardBuildingSpec[] courtyardBuildings,
            CastleKeepFloorPlan[] keepFloors,
            CastleKeepCirculationPlan keepCirculation,
            DungeonPlan dungeon,
            CavePlan cave,
            int2 keepCentre,
            bool keepRequiresTerrainResolution)
        {
            Topology = topology;
            OuterWardVertices = outerWardVertices;
            InnerWardVertices = innerWardVertices;
            Towers = towers;
            // Compute planned inner tower placements once while the plan is assembled. Runtime
            // must consume stable plan data rather than trigger planning through a property read.
            _innerTowers = CastleInnerWardTowerPlanner.Create(innerWardVertices);
            PrimaryGate = primaryGate;
            HasPosternGate = hasPosternGate;
            PosternGate = posternGate;
            HasInnerGate = hasInnerGate;
            InnerGate = innerGate;
            HasWell = hasWell;
            WellCentre = wellCentre;
            CourtyardBuildings = courtyardBuildings ?? Array.Empty<CastleCourtyardBuildingSpec>();
            KeepFloors = keepFloors ?? Array.Empty<CastleKeepFloorPlan>();
            KeepCirculation = keepCirculation;
            Dungeon = dungeon;
            Cave = cave;
            KeepCentre = keepCentre;
            KeepRequiresTerrainResolution = keepRequiresTerrainResolution;
        }
    }
}
