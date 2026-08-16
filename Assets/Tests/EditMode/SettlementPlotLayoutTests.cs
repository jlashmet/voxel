using MountingForce.WorldGen;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SettlementPlotLayoutTests
    {
        private static readonly Int3 Footprint = new Int3(100, 90, 80);

        [Test]
        public void HorizontalStreetPlacementDerivesPlotAndAccessFromFrontage()
        {
            BuildingPlot south = SettlementPlotLayout.AlongHorizontalStreet(
                seed: 11,
                salt: 0,
                roleId: 7,
                archetype: StructureArchetype.Townhouse,
                district: DistrictKind.Residential,
                streetId: "cross-street",
                frontageXDm: 500,
                streetZDm: 100,
                frontage: FrontageDirection.South,
                roadWidthDm: 40,
                setbackDm: 10,
                jitterDm: 0,
                footprintDm: Footprint);

            Assert.AreEqual(new Int2(450, 130), south.PositionDm);
            Assert.AreEqual(FrontageDirection.South, south.Frontage);
            Assert.AreEqual(SiteAccessKind.Street, south.Access.Kind);
            Assert.AreEqual("cross-street", south.Access.TargetId);
            Assert.AreEqual(new Int2(500, 100), south.Access.NetworkPointDm);

            BuildingPlot north = SettlementPlotLayout.AlongHorizontalStreet(
                11, 0, 8, StructureArchetype.Townhouse, DistrictKind.Residential,
                "cross-street", 500, 100, FrontageDirection.North,
                40, 10, 0, Footprint);

            Assert.AreEqual(new Int2(450, -10), north.PositionDm);
            Assert.AreEqual(FrontageDirection.North, north.Frontage);
        }

        [Test]
        public void VerticalStreetPlacementDerivesPlotAndAccessFromFrontage()
        {
            BuildingPlot west = SettlementPlotLayout.AlongVerticalStreet(
                seed: 23,
                salt: 0,
                roleId: 9,
                archetype: StructureArchetype.Shop,
                district: DistrictKind.Market,
                streetId: "spine",
                streetXDm: 200,
                frontageZDm: 400,
                frontage: FrontageDirection.West,
                roadWidthDm: 50,
                setbackDm: 15,
                jitterDm: 0,
                footprintDm: Footprint);

            Assert.AreEqual(new Int2(240, 360), west.PositionDm);
            Assert.AreEqual(new Int2(200, 400), west.Access.NetworkPointDm);

            BuildingPlot east = SettlementPlotLayout.AlongVerticalStreet(
                23, 0, 10, StructureArchetype.Shop, DistrictKind.Market,
                "spine", 200, 400, FrontageDirection.East,
                50, 15, 0, Footprint);

            Assert.AreEqual(new Int2(60, 360), east.PositionDm);
            Assert.AreEqual(FrontageDirection.East, east.Frontage);
        }

        [Test]
        public void JitterIsStableBoundedAndIndependentOfCallOrder()
        {
            int first = SettlementPlotLayout.StableSignedJitter(0xCAFEu, 17u, 8);
            _ = SettlementPlotLayout.StableSignedJitter(0x1234u, 99u, 8);
            int repeated = SettlementPlotLayout.StableSignedJitter(0xCAFEu, 17u, 8);

            Assert.AreEqual(first, repeated);
            Assert.That(first, Is.InRange(-8, 8));
            Assert.AreEqual(0, SettlementPlotLayout.StableSignedJitter(0xCAFEu, 17u, 0));
        }

        [Test]
        public void PlazaPlacementCentersFootprintAndRecordsExplicitAccess()
        {
            BuildingPlot plot = SettlementPlotLayout.CentreOnPlaza(
                roleId: 12,
                archetype: StructureArchetype.Well,
                district: DistrictKind.Civic,
                plazaId: "civic-square",
                centreDm: new Int2(1000, 600),
                footprintDm: Footprint);

            Assert.AreEqual(new Int2(950, 560), plot.PositionDm);
            Assert.AreEqual(SiteAccessKind.Plaza, plot.Access.Kind);
            Assert.AreEqual("civic-square", plot.Access.TargetId);
            Assert.AreEqual(new Int2(1000, 600), plot.Access.NetworkPointDm);
        }
    }
}
