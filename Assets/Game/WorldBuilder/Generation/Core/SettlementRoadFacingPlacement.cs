namespace MountingForce.WorldGen
{
    /// <summary>
    /// Policy-aware frontage placement over the existing deterministic SettlementPlotLayout helpers.
    /// The settlement still owns topology and street coordinates; this layer validates lot bounds,
    /// cardinal orientation and explicit movement-network frontage without introducing another planner.
    /// </summary>
    public static class SettlementRoadFacingPlacement
    {
        public static BuildingPlot AlongHorizontalStreet(
            uint seed,
            uint salt,
            int roleId,
            StructureArchetype archetype,
            DistrictKind district,
            string streetId,
            int frontageXDm,
            int streetZDm,
            FrontageDirection frontage,
            int roadWidthDm,
            int setbackDm,
            int jitterDm,
            Int3 footprintDm,
            in SettlementLotConfig lot)
        {
            BuildingPlot plot = SettlementPlotLayout.AlongHorizontalStreet(
                seed,
                salt,
                roleId,
                archetype,
                district,
                streetId,
                frontageXDm,
                streetZDm,
                frontage,
                roadWidthDm,
                setbackDm,
                jitterDm,
                footprintDm);
            lot.ValidatePlacement(seed, roleId, frontage, plot.Access, footprintDm);
            return plot;
        }

        public static BuildingPlot AlongVerticalStreet(
            uint seed,
            uint salt,
            int roleId,
            StructureArchetype archetype,
            DistrictKind district,
            string streetId,
            int streetXDm,
            int frontageZDm,
            FrontageDirection frontage,
            int roadWidthDm,
            int setbackDm,
            int jitterDm,
            Int3 footprintDm,
            in SettlementLotConfig lot)
        {
            BuildingPlot plot = SettlementPlotLayout.AlongVerticalStreet(
                seed,
                salt,
                roleId,
                archetype,
                district,
                streetId,
                streetXDm,
                frontageZDm,
                frontage,
                roadWidthDm,
                setbackDm,
                jitterDm,
                footprintDm);
            lot.ValidatePlacement(seed, roleId, frontage, plot.Access, footprintDm);
            return plot;
        }

        public static BuildingPlot CentreOnPlaza(
            uint seed,
            int roleId,
            StructureArchetype archetype,
            DistrictKind district,
            string plazaId,
            Int2 centreDm,
            Int3 footprintDm,
            in SettlementLotConfig lot,
            FrontageDirection frontage = FrontageDirection.South)
        {
            BuildingPlot plot = SettlementPlotLayout.CentreOnPlaza(
                roleId,
                archetype,
                district,
                plazaId,
                centreDm,
                footprintDm,
                frontage);
            lot.ValidatePlacement(seed, roleId, frontage, plot.Access, footprintDm);
            return plot;
        }
    }
}
