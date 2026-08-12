using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeBuildingGrammarTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void GrammarIsDeterministicAndVariesOrdinaryBuildings()
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

            Assert.AreEqual(11, generated,
                "Townhouses, wide houses, and shops should now be grammar-generated per role.");
            Assert.AreEqual(6, bespoke,
                "Inn, pub, church, warehouse, mansion, and well remain bespoke during migration.");
            Assert.GreaterOrEqual(signatures.Count, 9,
                "The new grammar should not collapse ordinary roles back into archetype clones.");
        }

        [Test]
        public void SignatureRolesExpressDifferentArchitecturalIntent()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            KentridgeBuildingForm weapon = Resolve(plan, KentridgeRole.WeaponShop);
            KentridgeBuildingForm armor = Resolve(plan, KentridgeRole.ArmorShop);
            KentridgeBuildingForm magic = Resolve(plan, KentridgeRole.MagicShop);
            KentridgeBuildingForm mayor = Resolve(plan, KentridgeRole.MayorHouse);
            KentridgeBuildingForm abandoned = Resolve(plan, KentridgeRole.AbandonedHouse);

            Assert.AreEqual(2, weapon.Storeys);
            Assert.AreEqual(KentridgeFootprintForm.RearWing, weapon.Footprint);
            Assert.AreEqual(KentridgeRoofForm.GableWithLeanTo, weapon.Roof);

            Assert.AreEqual(2, armor.Storeys);
            Assert.AreEqual(KentridgeFootprintForm.SideWing, armor.Footprint);
            Assert.AreEqual(KentridgeRoofForm.TwinGable, armor.Roof);

            Assert.AreEqual(3, magic.Storeys,
                "Magic shop should be the narrow vertical shop landmark.");
            Assert.AreEqual(KentridgeRoofForm.SteepGable, magic.Roof);
            Assert.AreEqual(KentridgeWindowStyle.Warm, magic.WindowStyle);
            Assert.Greater(magic.UpperOverhangDm, 0);

            Assert.AreEqual(3, mayor.Storeys,
                "Mayor house should reinforce the civic tier rather than clone a residential wide house.");
            Assert.AreEqual(KentridgeWindowStyle.Warm, mayor.WindowStyle);

            Assert.AreEqual(KentridgeWindowStyle.Open, abandoned.WindowStyle,
                "Abandoned house should read through empty/dark openings rather than normal glass.");
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
                        "Definition identity should now follow stable Kentridge role identity.");
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
