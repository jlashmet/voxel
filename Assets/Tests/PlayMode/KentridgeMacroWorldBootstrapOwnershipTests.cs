using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldBootstrapOwnershipTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void ShowcaseBootstrapLeavesMacroGeometryForPlayableCatalogue()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            VoxelWorldGenSettings settings = Settings();

            // Clear any stale one-shot selection left by another fixture, then publish exactly the
            // selection that the playable Kentridge compatibility composition publishes.
            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);
            Assert.That(TopDownWorldLayoutSelection.TryConsume(Seed, out _), Is.True);
            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);

            FeatureCatalogue bootstrap = default;
            FeatureCatalogue playable = default;
            try
            {
#pragma warning disable CS0618
                bootstrap = ShowcaseCatalogue.Build(Seed, Allocator.Temp);
#pragma warning restore CS0618
                Assert.That(
                    ContainsDefinitionStarting(bootstrap, "macro-town-"),
                    Is.False,
                    "The Showcase bootstrap catalogue is temporary and must realize only its authored town, not steal the gameplay macro-world handoff.");

                playable = KentridgeCombinedVoxelCatalogue.Build(
                    Seed,
                    settings,
                    Allocator.Temp);

                TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                    layout,
                    KentridgeTopDownWorldPhysicalIntent.Build(),
                    KentridgeDefinition.TownCentreDm,
                    MountingForceTopDownWorldDefinition.CellSizeDm,
                    settings);

                var expectedGenericTowns = 0;
                for (var i = 0; i < physical.Settlements.Count; i++)
                {
                    TopDownWorldSettlementPlan settlement = physical.Settlements[i];
                    string id = settlement.Node.Id;
                    Assert.That(
                        layout.CanReach(layout.RootId, id, verifiedOnly: false),
                        Is.True,
                        $"Macro settlement '{id}' is not reachable from root '{layout.RootId}' through the planned world routes.");

                    if (settlement.RealizationKind == TopDownWorldSettlementRealizationKind.ExistingRichGeneration)
                        continue;

                    expectedGenericTowns++;
                    Assert.That(
                        ContainsDefinitionStarting(playable, "macro-town-streets-" + id),
                        Is.True,
                        $"Playable catalogue is missing reachable street geometry for macro settlement '{id}'.");
                    if (settlement.Buildings.Count > 0)
                    {
                        Assert.That(
                            ContainsDefinitionStarting(playable, "macro-town-building-" + id + "-"),
                            Is.True,
                            $"Playable catalogue is missing generated building geometry for macro settlement '{id}'.");
                    }
                }

                Assert.That(expectedGenericTowns, Is.GreaterThanOrEqualTo(2));
                for (var i = 0; i < physical.Routes.Count; i++)
                {
                    Assert.That(
                        ContainsDefinitionStarting(playable, "macro-road-" + i),
                        Is.True,
                        $"Playable catalogue is missing macro route geometry for route {i}.");
                }

                TestContext.WriteLine(
                    "MACRO_BOOTSTRAP_OWNERSHIP " +
                    $"bootstrapDefinitions={bootstrap.Definitions.Length} " +
                    $"playableDefinitions={playable.Definitions.Length} " +
                    $"genericTowns={expectedGenericTowns} routes={physical.Routes.Count}");
            }
            finally
            {
                if (playable.IsCreated) playable.Dispose();
                if (bootstrap.IsCreated) bootstrap.Dispose();
                TopDownWorldLayoutSelection.TryConsume(Seed, out _);
            }
        }

        private static bool ContainsDefinitionStarting(FeatureCatalogue catalogue, string prefix)
        {
            for (var i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString().StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static VoxelWorldGenSettings Settings()
        {
            return new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
                    foundationStone: 20,
                    masonry: 18,
                    darkMasonry: 6,
                    timber: 2,
                    glass: 4,
                    warmWindow: 15,
                    roofTile: 8,
                    slate: 7,
                    cloth: 9,
                    moss: 14,
                    water: 11,
                    roadSurface: 13));
        }
    }
}
