using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// First physical macro-world geography/settlement pass for the source-backed Mounting Force
    /// graph. The graph remains topology authority; these are semantic spatial constraints used by
    /// the reusable physical planner.
    /// </summary>
    public static class KentridgeTopDownWorldPhysicalIntent
    {
        public const string RossdamLake = "rossdam-lake";
        public const string SouthernRidge = "southern-ridge";
        public const string SouthernPass = "southern-pass";
        public const string KentridgeMeadow = "kentridge-meadow";
        public const string NorthernWoodland = "northern-woodland";
        public const string RossdamCountryside = "rossdam-countryside";

        public static TopDownWorldPhysicalIntentSpec Build()
        {
            var regions = new[]
            {
                new TopDownWorldRegionSpec(
                    KentridgeMeadow,
                    "Kentridge Meadow Country",
                    TopDownWorldRegionKind.PlainsMeadow,
                    TopDownWorldRegionRelationKind.AnchoredAt,
                    KentridgeTopDownWorldLayout.Overworld,
                    string.Empty,
                    halfExtentXDm: 520,
                    halfExtentZDm: 520,
                    elevationDeltaDm: 0,
                    variationDm: 25,
                    source: "first macro geography pass: open meadow country around Kentridge"),
                new TopDownWorldRegionSpec(
                    NorthernWoodland,
                    "Northern Woodland",
                    TopDownWorldRegionKind.ForestWoodland,
                    TopDownWorldRegionRelationKind.AnchoredAt,
                    KentridgeTopDownWorldLayout.Forest,
                    string.Empty,
                    halfExtentXDm: 470,
                    halfExtentZDm: 520,
                    elevationDeltaDm: 10,
                    variationDm: 20,
                    source: "first macro geography pass: forest region around recovered forest route"),
                new TopDownWorldRegionSpec(
                    RossdamCountryside,
                    "Rossdam Rolling Country",
                    TopDownWorldRegionKind.Generic,
                    TopDownWorldRegionRelationKind.AnchoredAt,
                    KentridgeTopDownWorldLayout.RossdamRegion,
                    string.Empty,
                    halfExtentXDm: 440,
                    halfExtentZDm: 460,
                    elevationDeltaDm: 18,
                    variationDm: 20,
                    source: "first macro geography pass: rolling country approaching Rossdam"),
                new TopDownWorldRegionSpec(
                    RossdamLake,
                    "Rossdam Lake",
                    TopDownWorldRegionKind.WaterBody,
                    TopDownWorldRegionRelationKind.Between,
                    KentridgeTopDownWorldLayout.MoordellCorridor,
                    KentridgeTopDownWorldLayout.RossdamApproach,
                    // Stable seeded variation is part of the authoring contract. These nominal
                    // margins resolve to exactly 450 x 225 dm half-extents and 24 dm depth for
                    // Kentridge's fixed seed instead of letting negative variation shrink the
                    // landmark below its 90 m x 45 m acceptance floor. The semantic southward
                    // offset keeps the bounded lake on the real direct Rossdam road so GoAround
                    // remains an exercised geography solution rather than metadata-only intent.
                    halfExtentXDm: 456,
                    halfExtentZDm: 228,
                    elevationDeltaDm: -23,
                    variationDm: 12,
                    offsetXDm: -300,
                    offsetZDm: -210,
                    source: "first macro geography pass: substantial bounded lake separating the Moordell corridor from Rossdam approach while remaining streamable at gameplay budgets"),
                new TopDownWorldRegionSpec(
                    SouthernRidge,
                    "Southern Ridge",
                    TopDownWorldRegionKind.MountainRidge,
                    TopDownWorldRegionRelationKind.Separates,
                    KentridgeTopDownWorldLayout.SouthFightingArea,
                    KentridgeTopDownWorldLayout.LoganApproach,
                    halfExtentXDm: 420,
                    halfExtentZDm: 120,
                    elevationDeltaDm: 110,
                    variationDm: 8,
                    source: "first macro geography pass: substantial ridge barrier across the Logan route, bounded to keep the adjacent Orc settlement envelope buildable"),
                new TopDownWorldRegionSpec(
                    SouthernPass,
                    "Southern Ridge Pass",
                    TopDownWorldRegionKind.ValleyPass,
                    TopDownWorldRegionRelationKind.Between,
                    KentridgeTopDownWorldLayout.SouthFightingArea,
                    KentridgeTopDownWorldLayout.LoganApproach,
                    halfExtentXDm: 80,
                    halfExtentZDm: 300,
                    elevationDeltaDm: 24,
                    variationDm: 0,
                    source: "explicit north-south pass through the authored southern ridge barrier")
            };

            var routeConstraints = new[]
            {
                new TopDownWorldRouteRegionConstraintSpec(
                    KentridgeTopDownWorldLayout.FightingArea1,
                    KentridgeTopDownWorldLayout.FightingArea2,
                    RossdamLake,
                    TopDownWorldRouteRegionSolutionKind.GoAround,
                    clearanceDm: 75,
                    source: "northern road stays on dry ground around the lake's eastern shore"),
                new TopDownWorldRouteRegionConstraintSpec(
                    KentridgeTopDownWorldLayout.FightingArea1,
                    KentridgeTopDownWorldLayout.BanditHideout,
                    RossdamLake,
                    TopDownWorldRouteRegionSolutionKind.GoAround,
                    clearanceDm: 75,
                    source: "modern 3D blockout: the verified bandit spur follows dry western shoreline around the authored lake; this is a routing solution, not legacy geography evidence"),
                new TopDownWorldRouteRegionConstraintSpec(
                    KentridgeTopDownWorldLayout.MoordellCorridor,
                    KentridgeTopDownWorldLayout.RossdamApproach,
                    RossdamLake,
                    TopDownWorldRouteRegionSolutionKind.GoAround,
                    clearanceDm: 75,
                    source: "Rossdam approach follows dry ground around the substantial lake"),
                new TopDownWorldRouteRegionConstraintSpec(
                    KentridgeTopDownWorldLayout.SouthFightingArea,
                    KentridgeTopDownWorldLayout.OrcVillage,
                    SouthernRidge,
                    TopDownWorldRouteRegionSolutionKind.GoAround,
                    clearanceDm: 45,
                    source: "modern 3D blockout: the verified Orc Village branch skirts the western shoulder of the Logan ridge; this is a routing solution, not legacy geography evidence"),
                new TopDownWorldRouteRegionConstraintSpec(
                    KentridgeTopDownWorldLayout.SouthFightingArea,
                    KentridgeTopDownWorldLayout.LoganApproach,
                    SouthernRidge,
                    TopDownWorldRouteRegionSolutionKind.DesignatedCrossing,
                    SouthernPass,
                    clearanceDm: 35,
                    source: "Logan approach uses the designated pass through the southern ridge")
            };

            var settlements = new[]
            {
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.Kentridge,
                    TopDownWorldSettlementRealizationKind.ExistingRichGeneration,
                    minimumBuildingCount: 0),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.Hightown,
                    TopDownWorldSettlementRealizationKind.ExistingRichGeneration,
                    minimumBuildingCount: 0),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.Moordell,
                    TopDownWorldSettlementRealizationKind.GenericBlockout,
                    minimumBuildingCount: 4),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.Rossdam,
                    TopDownWorldSettlementRealizationKind.GenericBlockout,
                    minimumBuildingCount: 4),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.FairyVillage,
                    TopDownWorldSettlementRealizationKind.GenericBlockout,
                    minimumBuildingCount: 4),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.OrcVillage,
                    TopDownWorldSettlementRealizationKind.GenericBlockout,
                    minimumBuildingCount: 4)
            };

            return new TopDownWorldPhysicalIntentSpec(regions, routeConstraints, settlements);
        }
    }
}
