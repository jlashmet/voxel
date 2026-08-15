using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeArchitectureSiteProjectionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void GeneratedGeometryUsesFixedEnvelopeExactFrontageAndGuaranteedInterior()
        {
            var intent = new StructureIntent(
                77,
                KentridgeDefinition.Id,
                StructureArchetype.Shop,
                DistrictKind.Market,
                new Int2(100, 200),
                FrontageDirection.West,
                new Int3(124, 120, 124));
            var form = new StructureForm(
                77,
                StructureArchetype.Shop,
                DistrictKind.Market,
                StructureGenerationMode.Generated,
                FootprintForm.RearWing,
                RoofForm.GableWithLeanTo,
                FrontageRhythm.ThreeBay,
                WindowTreatment.Glass,
                94, 70, 2, -10,
                0, 20, 30, 26,
                false, true);

            StructureSiteGeometry geometry;
            Assert.IsTrue(StructureSiteGeometryResolver.TryResolve(
                intent, KentridgeDefinition.Theme, form, out geometry));

            Assert.AreEqual(100, geometry.FootprintMinDm.X);
            Assert.AreEqual(200, geometry.FootprintMinDm.Y);
            Assert.AreEqual(224, geometry.FootprintMaxDm.X);
            Assert.AreEqual(324, geometry.FootprintMaxDm.Y);

            // Local shop door centre is (52,10). West is quarter-turn 1 inside a 124x124
            // envelope: (123-10,52), then translated by the world origin.
            Assert.AreEqual(213, geometry.PublicEntranceDm.X);
            Assert.AreEqual(252, geometry.PublicEntranceDm.Y);
            Assert.AreEqual(FrontageDirection.West, geometry.PublicEntranceFacing);

            // Main body spans x=15..109 with four-decimetre walls. The offset door centre x=52
            // therefore guarantees 33dm laterally to the nearer interior wall. From the door plane
            // to the inner face of the rear wall there are 70-4 = 66dm of usable depth.
            Assert.AreEqual(33, geometry.InteriorHalfWidthDm);
            Assert.AreEqual(66, geometry.InteriorDepthDm);
        }

        [Test]
        public void BespokeArchitectureFailsClosedUntilItPublishesGeometry()
        {
            var intent = new StructureIntent(
                2,
                KentridgeDefinition.Id,
                StructureArchetype.Church,
                DistrictKind.Civic,
                new Int2(0, 0),
                FrontageDirection.South,
                new Int3(164, 180, 164));
            var form = new StructureForm(
                2,
                StructureArchetype.Church,
                DistrictKind.Civic,
                StructureGenerationMode.Bespoke,
                FootprintForm.Rectangle,
                RoofForm.Gable,
                FrontageRhythm.TwoBay,
                WindowTreatment.Glass,
                0, 0, 0, 0, 0, 0, 0, 0,
                false, false);

            StructureSiteGeometry geometry;
            Assert.IsFalse(StructureSiteGeometryResolver.TryResolve(
                intent, KentridgeDefinition.Theme, form, out geometry));
        }

        [Test]
        public void KentridgeProjectionExposesGeneratedInteriorAndStageEnvelopeOnly()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var provider = new KentridgeArchitectureSiteProjectionProvider(plan);
            int projected = 0;
            int excluded = 0;

            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                SettlementSiteProjection projection;
                if (!provider.TryProject(site, out projection))
                {
                    excluded++;
                    CutsceneStageEnvelope excludedEnvelope;
                    Assert.IsFalse(provider.TryGetCutsceneStageEnvelope(site, out excludedEnvelope));
                    continue;
                }

                projected++;
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.Interior));
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.PublicExit));
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.ConversationSpace));
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.CutsceneStage));

                CutsceneStageEnvelope envelope;
                Assert.IsTrue(provider.TryGetCutsceneStageEnvelope(site, out envelope));
                Assert.Greater(envelope.InteriorHalfWidthDecimetres, 0);
                Assert.Greater(envelope.InteriorDepthDecimetres, 0);

                SiteArchetype expectedArchetype = site.RoleId == (int)KentridgeRole.Pub
                    ? SiteArchetype.Pub
                    : SiteArchetype.Unspecified;
                Assert.AreEqual(expectedArchetype, projection.Archetype);
            }

            Assert.AreEqual(13, projected);
            Assert.AreEqual(4, excluded);
        }

        private static bool HasCapability(
            SettlementSiteProjection projection,
            SiteCapabilityKind kind)
        {
            for (var i = 0; i < projection.Capabilities.Count; i++)
                if (projection.Capabilities[i].Kind == kind)
                    return true;
            return false;
        }
    }
}
