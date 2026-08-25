using System;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeCirculationCoherenceTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SecondaryParallelStairStreetsKeepTownSpacingFromMainSpine()
        {
            KentridgeUrbanCirculationPlan plan = KentridgeUrbanCirculation.Build(Seed);
            int roadMinX = KentridgeTownPlanner.MainSpineXDm
                           - KentridgeTownPlanner.MainRoadWidthDm / 2;
            int roadMaxX = KentridgeTownPlanner.MainSpineXDm
                           + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int requiredGap = KentridgeTownPlanner.CompositionPolicy.Density.MinSpacingDm;

            for (int i = 0; i < plan.Connectors.Count; i++)
            {
                KentridgeUrbanConnector connector = plan.Connectors[i];
                if (connector.Kind != KentridgeUrbanConnectorKind.StairStreet
                    || !connector.IsVertical)
                    continue;

                int connectorMinX = connector.StartDm.X - connector.WidthDm / 2;
                int connectorMaxX = connector.StartDm.X + connector.WidthDm / 2;
                int gap = connectorMaxX <= roadMinX
                    ? roadMinX - connectorMaxX
                    : connectorMinX >= roadMaxX
                        ? connectorMinX - roadMaxX
                        : 0;

                Assert.GreaterOrEqual(
                    gap,
                    requiredGap,
                    connector.Id + " runs parallel to the already-walkable main spine with only "
                    + gap + " dm of separation; independent circulation corridors must keep the "
                    + requiredGap + " dm town spacing instead of forming a duplicate stair bundle.");
            }
        }

        [Test]
        public void VerticalInfrastructureDoesNotOverlayDedicatedStairsOnContinuousMainRoad()
        {
            FeatureCatalogue catalogue = KentridgeVerticalConnectorCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                bool retainedWall = false;
                bool retainedCampanile = false;
                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    string name = catalogue.Definitions[i].Name.ToString();
                    Assert.IsFalse(
                        name.StartsWith("kentridge-stair-", StringComparison.Ordinal),
                        name + " duplicates the continuous supported main-road climb already emitted "
                        + "by KentridgeVerticalTownSurfaceCatalogue.");
                    retainedWall |= name.StartsWith(
                        "kentridge-infrastructure-", StringComparison.Ordinal)
                        && name.Contains("retaining");
                    retainedCampanile |= name == "kentridge-infrastructure-civic-campanile";
                }

                Assert.IsTrue(retainedWall,
                    "Removing duplicate road stairs must not remove hillside retaining architecture.");
                Assert.IsTrue(retainedCampanile,
                    "Removing duplicate road stairs must not remove the civic campanile.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
