using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeBuildingGrammarTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void GrammarIsDeterministicAndVariesRoleBuildings()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var signatures = new HashSet<string>();
            int generated = 0;
            int bespoke = 0;

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                KentridgeBuildingForm a = KentridgeBuildingGrammar.Resolve(plot, Seed);
                KentridgeBuildingForm b = KentridgeBuildingGrammar.Resolve(plot, Seed);

                Assert.AreEqual(Signature(a), Signature(b),
                    "Same stable role and seed must resolve the same architectural form.");
                Assert.AreEqual(plot.RoleId, a.RoleId);
                Assert.AreEqual(plot.Archetype, a.Archetype);
                Assert.AreEqual(plot.District, a.District);

                if (a.IsGenerated)
                {
                    generated++;
                    signatures.Add(Signature(a));
                    KentridgeBuildingGrammar.ValidateGenerated(a);
                }
                else bespoke++;
            }

            Assert.AreEqual(13, generated,
                "Houses, shops, inn, and pub should now be grammar-generated per stable role.");
            Assert.AreEqual(4, bespoke,
                "Only the remaining landmark and utility forms stay bespoke in this migration slice.");
            Assert.GreaterOrEqual(signatures.Count, 11,
                "The grammar should not collapse role buildings back into archetype clones.");
        }

        [Test]
        public void HospitalityRolesNoLongerShareOneTemplate()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            KentridgeBuildingForm inn = Resolve(plan, KentridgeRole.Inn);
            KentridgeBuildingForm pub = Resolve(plan, KentridgeRole.Pub);

            Assert.IsTrue(inn.IsHospitality);
            Assert.AreEqual(3, inn.Storeys);
            Assert.AreEqual(KentridgeFootprintForm.RearWing, inn.Footprint);
            Assert.AreEqual(KentridgeRoofForm.TwinGable, inn.Roof);
            Assert.AreEqual(KentridgeWindowStyle.Warm, inn.WindowStyle);

            Assert.IsTrue(pub.IsHospitality);
            Assert.AreEqual(2, pub.Storeys);
            Assert.AreEqual(KentridgeFootprintForm.SideWing, pub.Footprint);
            Assert.AreEqual(KentridgeRoofForm.GableWithLeanTo, pub.Roof);
            Assert.AreEqual(KentridgeFrontageRhythm.Asymmetric, pub.FrontageRhythm);
            Assert.AreNotEqual(Signature(inn), Signature(pub),
                "Inn and pub must no longer share one prefab-like geometry.");
        }

        [Test]
        public void GrammarCatalogueUsesStableRoleIdentityAndExactlySeventeenStructures()
        {
            FeatureCatalogue catalogue = KentridgeGrammarVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(17, catalogue.Definitions.Length);
                Assert.AreEqual(17, catalogue.Rules.Length);
                Assert.AreEqual(17, catalogue.Anchors.Length);
                Assert.AreEqual(17, catalogue.ExplicitPlacements.Length);

                for (int roleId = 0; roleId < 17; roleId++)
                {
                    KentridgeRole role = (KentridgeRole)roleId;
                    FeatureDefinition definition = catalogue.Definitions[roleId];
                    PlacementRule rule = catalogue.Rules[roleId];

                    Assert.AreEqual(FeatureKind.Structure, definition.Kind);
                    StringAssert.AreEqualIgnoringCase(
                        "kentridge-role-" + role.ToString(),
                        definition.Name.ToString());
                    Assert.AreEqual(roleId, rule.DefinitionId,
                        "Definition identity should follow stable Kentridge role identity.");
                    Assert.AreEqual(roleId, rule.ExplicitOffset);
                    Assert.AreEqual(1, rule.ExplicitCount);
                    Assert.Greater(definition.ProgramLength, 0);
                    Assert.AreEqual(1, definition.AnchorCount);
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static KentridgeBuildingForm Resolve(SettlementPlan plan, KentridgeRole role)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.RoleId == (int)role)
                    return KentridgeBuildingGrammar.Resolve(plot, Seed);
            }

            Assert.Fail("Missing Kentridge role " + role);
            return default;
        }

        private static string Signature(KentridgeBuildingForm form)
        {
            return form.Mode + ":" + form.Footprint + ":" + form.Roof + ":"
                 + form.FrontageRhythm + ":" + form.WindowStyle + ":"
                 + form.WidthDm + "x" + form.DepthDm + ":" + form.Storeys + ":"
                 + form.DoorOffsetDm + ":" + form.UpperOverhangDm + ":"
                 + form.RoofHeightDm + ":" + form.WingWidthDm + "x" + form.WingDepthDm
                 + ":" + form.WingOnRight + ":" + form.ChimneyOnRight;
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
