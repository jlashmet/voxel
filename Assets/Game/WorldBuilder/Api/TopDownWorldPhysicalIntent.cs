using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public enum TopDownWorldRegionKind
    {
        Generic,
        WaterBody,
        MountainRidge,
        ValleyPass,
        PlainsMeadow,
        ForestWoodland
    }

    public enum TopDownWorldRegionRelationKind
    {
        AnchoredAt,
        Between,
        AdjacentTo,
        Contains,
        Separates
    }

    public enum TopDownWorldRouteRegionSolutionKind
    {
        GoAround,
        PassThrough,
        DesignatedCrossing
    }

    public enum TopDownWorldSettlementRealizationKind
    {
        GenericBlockout,
        ExistingRichGeneration
    }

    /// <summary>
    /// Controls whether a semantic geography solution may recover when one of its route endpoints
    /// is authored inside the blocking region. Strict mode preserves fail-fast authoring/tests;
    /// EndpointEscape permits only the contiguous entry/exit portion needed to leave that region.
    /// </summary>
    public enum TopDownWorldConstraintRelaxationMode
    {
        Strict,
        EndpointEscape
    }

    /// <summary>
    /// Semantic authored geography for a macro world. Positions are relationships to the existing
    /// source-backed graph, never captured scene coordinates. The physical planner resolves these
    /// relationships deterministically for a layout/seed.
    /// </summary>
    public sealed class TopDownWorldRegionSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public TopDownWorldRegionKind Kind { get; }
        public TopDownWorldRegionRelationKind Relation { get; }
        public string PrimaryNodeId { get; }
        public string SecondaryNodeId { get; }
        public int HalfExtentXDm { get; }
        public int HalfExtentZDm { get; }
        public int ElevationDeltaDm { get; }
        public int VariationDm { get; }
        public int OffsetXDm { get; }
        public int OffsetZDm { get; }
        public string Source { get; }

        public bool BlocksUnsolvedHardRoutes =>
            Kind == TopDownWorldRegionKind.WaterBody || Kind == TopDownWorldRegionKind.MountainRidge;

        public TopDownWorldRegionSpec(
            string id,
            string displayName,
            TopDownWorldRegionKind kind,
            TopDownWorldRegionRelationKind relation,
            string primaryNodeId,
            string secondaryNodeId,
            int halfExtentXDm,
            int halfExtentZDm,
            int elevationDeltaDm,
            int variationDm = 0,
            int offsetXDm = 0,
            int offsetZDm = 0,
            string source = "authored macro-region intent")
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A macro region requires an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A macro region requires a display name.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(primaryNodeId))
                throw new ArgumentException("A macro region requires a primary graph anchor.", nameof(primaryNodeId));
            if ((relation == TopDownWorldRegionRelationKind.Between
                 || relation == TopDownWorldRegionRelationKind.Separates)
                && string.IsNullOrWhiteSpace(secondaryNodeId))
                throw new ArgumentException(
                    "Between/separates macro regions require a secondary graph anchor.",
                    nameof(secondaryNodeId));
            if (halfExtentXDm < 1) throw new ArgumentOutOfRangeException(nameof(halfExtentXDm));
            if (halfExtentZDm < 1) throw new ArgumentOutOfRangeException(nameof(halfExtentZDm));
            if (variationDm < 0) throw new ArgumentOutOfRangeException(nameof(variationDm));
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("A macro region requires source/design provenance.", nameof(source));

            Id = id;
            DisplayName = displayName;
            Kind = kind;
            Relation = relation;
            PrimaryNodeId = primaryNodeId;
            SecondaryNodeId = secondaryNodeId ?? string.Empty;
            HalfExtentXDm = halfExtentXDm;
            HalfExtentZDm = halfExtentZDm;
            ElevationDeltaDm = elevationDeltaDm;
            VariationDm = variationDm;
            OffsetXDm = offsetXDm;
            OffsetZDm = offsetZDm;
            Source = source;
        }
    }

    /// <summary>
    /// Explicit semantic solution for a verified route that encounters authored geography.
    /// A blocking water/ridge region is never crossed implicitly: the definition must say whether
    /// to go around, pass through, or use a designated crossing/pass region. Recoverable endpoint
    /// overlap is opt-in per constraint and is always reported by the physical plan.
    /// </summary>
    public sealed class TopDownWorldRouteRegionConstraintSpec
    {
        public string FromId { get; }
        public string ToId { get; }
        public string RegionId { get; }
        public TopDownWorldRouteRegionSolutionKind SolutionKind { get; }
        public string SolutionRegionId { get; }
        public int ClearanceDm { get; }
        public string Source { get; }
        public TopDownWorldConstraintRelaxationMode RelaxationMode { get; }
        public string RouteKey => FromId + "->" + ToId;

        public TopDownWorldRouteRegionConstraintSpec(
            string fromId,
            string toId,
            string regionId,
            TopDownWorldRouteRegionSolutionKind solutionKind,
            string solutionRegionId = "",
            int clearanceDm = 60,
            string source = "authored macro-route geography solution",
            TopDownWorldConstraintRelaxationMode relaxationMode = TopDownWorldConstraintRelaxationMode.Strict)
        {
            if (string.IsNullOrWhiteSpace(fromId))
                throw new ArgumentException("A route constraint requires a source node.", nameof(fromId));
            if (string.IsNullOrWhiteSpace(toId))
                throw new ArgumentException("A route constraint requires a destination node.", nameof(toId));
            if (string.IsNullOrWhiteSpace(regionId))
                throw new ArgumentException("A route constraint requires a region.", nameof(regionId));
            if (solutionKind == TopDownWorldRouteRegionSolutionKind.DesignatedCrossing
                && string.IsNullOrWhiteSpace(solutionRegionId))
                throw new ArgumentException(
                    "A designated crossing requires a crossing/pass region.",
                    nameof(solutionRegionId));
            if (clearanceDm < 0) throw new ArgumentOutOfRangeException(nameof(clearanceDm));
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("A route constraint requires provenance.", nameof(source));

            FromId = fromId;
            ToId = toId;
            RegionId = regionId;
            SolutionKind = solutionKind;
            SolutionRegionId = solutionRegionId ?? string.Empty;
            ClearanceDm = clearanceDm;
            Source = source;
            RelaxationMode = relaxationMode;
        }
    }

    public sealed class TopDownWorldSettlementPhysicalSpec
    {
        public string NodeId { get; }
        public TopDownWorldSettlementRealizationKind RealizationKind { get; }
        public int MinimumBuildingCount { get; }

        public TopDownWorldSettlementPhysicalSpec(
            string nodeId,
            TopDownWorldSettlementRealizationKind realizationKind,
            int minimumBuildingCount = 4)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("A settlement physical spec requires a graph node.", nameof(nodeId));
            if (minimumBuildingCount < 0) throw new ArgumentOutOfRangeException(nameof(minimumBuildingCount));
            if (realizationKind == TopDownWorldSettlementRealizationKind.GenericBlockout
                && minimumBuildingCount < 4)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumBuildingCount),
                    "Generic macro settlements require at least four blockout buildings.");

            NodeId = nodeId;
            RealizationKind = realizationKind;
            MinimumBuildingCount = minimumBuildingCount;
        }
    }

    public sealed class TopDownWorldPhysicalIntentSpec
    {
        private readonly TopDownWorldRegionSpec[] _regions;
        private readonly TopDownWorldRouteRegionConstraintSpec[] _routeConstraints;
        private readonly TopDownWorldSettlementPhysicalSpec[] _settlements;

        public IReadOnlyList<TopDownWorldRegionSpec> Regions => _regions;
        public IReadOnlyList<TopDownWorldRouteRegionConstraintSpec> RouteConstraints => _routeConstraints;
        public IReadOnlyList<TopDownWorldSettlementPhysicalSpec> Settlements => _settlements;

        public TopDownWorldPhysicalIntentSpec(
            IReadOnlyList<TopDownWorldRegionSpec> regions,
            IReadOnlyList<TopDownWorldRouteRegionConstraintSpec> routeConstraints,
            IReadOnlyList<TopDownWorldSettlementPhysicalSpec> settlements)
        {
            _regions = Copy(regions ?? throw new ArgumentNullException(nameof(regions)));
            _routeConstraints = Copy(routeConstraints ?? throw new ArgumentNullException(nameof(routeConstraints)));
            _settlements = Copy(settlements ?? throw new ArgumentNullException(nameof(settlements)));
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var result = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
                result[i] = source[i] ?? throw new ArgumentException("Physical intent contains a null entry.");
            return result;
        }
    }
}
