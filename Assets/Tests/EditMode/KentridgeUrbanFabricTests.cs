using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanFabricTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void AnonymousFabricGrammarVariesFormsWhileRespectingBlockStoreyLimits()
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(Seed);
            var signatures = new HashSet<string>();
            int civicSamples = 0;
            int workingSamples = 0;

            for (int runIndex = 0; runIndex < plan.FrontageRuns.Count; runIndex++)
            {
                KentridgeFrontageRun run = plan.FrontageRuns[runIndex];
                for (int siteIndex = 0; siteIndex < 3; siteIndex++)
                {
                    KentridgeUrbanFabricForm form = KentridgeUrbanFabricGrammar.Resolve(
                        run, Seed, runIndex, siteIndex);
                    KentridgeUrbanFabricGrammar.Validate(run, form);
                    Assert.That(form.Storeys, Is.InRange(run.MinStoreys, run.MaxStoreys));
                    Assert.LessOrEqual(
                        form.WidthDm + 2 * form.UpperOverhangDm
                        + 2 * KentridgeUrbanFabricGrammar.RoofOverhangDm,
                        KentridgeUrbanFabricGrammar.EnvelopeDm);
                    signatures.Add(Signature(form));

                    if (run.Band == KentridgeUrbanBand.CivicCrown)
                    {
                        civicSamples++;
                        Assert.AreEqual(3, form.Storeys,
                            "Civic anonymous fabric should preserve the authored three-storey crown.");
                    }

                    if (run.District == DistrictKind.Working)
                    {
                        workingSamples++;
                        Assert.That(form.Storeys, Is.InRange(1, 2),
                            "Working-yard fabric should stay lower than market/civic frontage.");
                        Assert.IsFalse(form.HasAwning,
                            "Working-yard frontage should not accidentally inherit market-shop awnings.");
                    }
                }
            }

            Assert.Greater(civicSamples, 0);
            Assert.Greater(workingSamples, 0);
            Assert.GreaterOrEqual(signatures.Count, 18,
                "Anonymous block frontage should no longer collapse into a tiny reusable proxy set.");
        }

        [Test]
        public void UrbanFabricCatalogueRealizesAllEightBlocksAsIndividualBuildings()
        {
            FeatureCatalogue catalogue = KentridgeUrbanFabricCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(42, catalogue.Definitions.Length);
                Assert.AreEqual(42, catalogue.Rules.Length);
                Assert.AreEqual(42, catalogue.ExplicitPlacements.Length);
                Assert.AreEqual(0, catalogue.Anchors.Length);

                for (int i = 0; i < catalogue.Definitions.Length; i++)
                {
                    FeatureDefinition definition = catalogue.Definitions[i];
                    PlacementRule rule = catalogue.Rules[i];
                    Assert.AreEqual(FeatureKind.Infrastructure, definition.Kind);
                    Assert.AreEqual(86, definition.Precedence);
                    Assert.AreEqual(
                        KentridgeUrbanFabricGrammar.EnvelopeDm,
                        definition.Footprint.x,
                        "Test settings use one voxel per decimetre.");
                    Assert.AreEqual(definition.Footprint.x, definition.Footprint.z);
                    Assert.AreEqual(i, rule.DefinitionId);
                    Assert.AreEqual(i, rule.ExplicitOffset);
                    Assert.AreEqual(1, rule.ExplicitCount);
                    Assert.Greater(definition.ProgramLength, 20,
                        "Urban fabric should be real architectural bytecode, not a three-primitive box proxy.");
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static string Signature(KentridgeUrbanFabricForm form)
        {
            return form.WidthDm + "x" + form.DepthDm + ":" + form.Storeys + ":"
                 + form.UpperOverhangDm + ":" + form.RoofHeightDm + ":"
                 + form.Roof + ":" + form.FrontageRhythm + ":" + form.WindowStyle + ":"
                 + form.HasAwning + ":" + form.ChimneyOnRight + ":" + form.AnnexOnRight;
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
