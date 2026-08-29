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
                    halfExtentXDm: 520,
                    halfExtentZDm: 270,
                    elevationDeltaDm: -45,
                    variationDm: 12,
                    offsetXDm: -300,
                    source: "first macro geography pass: substantial lake separating the Moordell corridor from Rossdam approach"),
                new TopDownWorldRegionSpec(
                    SouthernRidge,
                    "Southern Ridge",
                    TopDownWorldRegionKind.MountainRidge,
                    TopDownWorldRegionRelationKind.Separates,
                    KentridgeTopDownWorldLayout.SouthFightingArea,
                    KentridgeTopDownWorldLayout.LoganApproach,
                    halfExtentXDm: 420,
                    halfExtentZDm: 270,
                    elevationDeltaDm: 110,
                    variationDm: 8,
                    source: "first macro geography pass: substantial ridge barrier across the Logan route"),
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
                    KentridgeTopDownWorldLayout.MoordellCorridor,
                    KentridgeTopDownWorldLayout.RossdamApproach,
                    RossdamLake,
                    TopDownWorldRouteRegionSolutionKind.GoAround,
                    clearanceDm: 75,
                    source: "Rossdam approach follows dry ground around the substantial lake"),
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
                    TopDownWorldSettlementRealizationKind.GenericBlockout),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.Rossdam,
                    TopDownWorldSettlementRealizationKind.GenericBlockout),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.FairyVillage,
                    TopDownWorldSettlementRealizationKind.GenericBlockout),
                new TopDownWorldSettlementPhysicalSpec(
                    KentridgeTopDownWorldLayout.OrcVillage,
                    TopDownWorldSettlementRealizationKind.GenericBlockout)
            };

            return new TopDownWorldPhysicalIntentSpec(regions, routeConstraints, settlements);
        }
    }
}
