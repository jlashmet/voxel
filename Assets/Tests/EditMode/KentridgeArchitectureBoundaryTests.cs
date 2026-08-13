using System.Linq;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeArchitectureBoundaryTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void KentridgeContentSuppliesIntentAndArchitectureOwnsDetail()
        {
            Assert.AreEqual(
                "MountingForce.WorldGen.Core",
                typeof(KentridgeDefinition).Assembly.GetName().Name,
                "Kentridge settlement planning must remain in the high-level Core/content assembly.");

            Assert.AreEqual(
                "MountingForce.WorldGen.Core",
                typeof(StructureIntent).Assembly.GetName().Name,
                "The settlement-to-architecture handoff contract belongs to Core.");

            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(ArchitectureCompiler).Assembly.GetName().Name,
                "Roof/window/facade generation must live in the lower architecture assembly.");

            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(StructureForm).Assembly.GetName().Name,
                "Detailed architectural forms must live below settlement planning.");

            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(KentridgeUrbanFabricGrammar).Assembly.GetName().Name,
                "Anonymous frontage detail generation must live in the lower architecture assembly.");

            string[] coreReferences = typeof(KentridgeDefinition).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(
                coreReferences,
                "MountingForce.WorldGen.Architecture",
                "The high-level Kentridge/Core assembly must never depend downward on architectural detail.");
        }

        [Test]
        public void StructureIntentContainsHighLevelConstraintsAndNoArchitecturalDetail()
        {
            string[] fields = typeof(StructureIntent)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "RoleId", "StyleId", "Archetype", "District",
                    "PositionDm", "Frontage", "EnvelopeDm"
                },
                fields,
                "StructureIntent should describe what/where/how-large, not roofs, windows or facade modules.");

            CollectionAssert.DoesNotContain(fields, "Roof");
            CollectionAssert.DoesNotContain(fields, "WindowStyle");
            CollectionAssert.DoesNotContain(fields, "Storeys");
            CollectionAssert.DoesNotContain(fields, "ChimneyOnRight");
        }

        [Test]
        public void ArchitectureCompilerPreservesIntentIdentityAndOwnsLocalForm()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            int generated = 0;
            int bespoke = 0;

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, Seed);

                Assert.AreEqual(plot.RoleId, intent.RoleId);
                Assert.AreEqual(KentridgeDefinition.Id, intent.StyleId);
                Assert.AreEqual(plot.Archetype, intent.Archetype);
                Assert.AreEqual(plot.District, intent.District);
                Assert.AreEqual(plot.PositionDm.X, intent.PositionDm.X);
                Assert.AreEqual(plot.PositionDm.Y, intent.PositionDm.Y);
                Assert.AreEqual(plot.Frontage, intent.Frontage);
                Assert.AreEqual(KentridgeDefinition.FootprintDm(plot.Archetype).X, intent.EnvelopeDm.X);
                Assert.AreEqual(KentridgeDefinition.FootprintDm(plot.Archetype).Z, intent.EnvelopeDm.Z);

                Assert.AreEqual(intent.RoleId, form.RoleId);
                Assert.AreEqual(intent.Archetype, form.Archetype);
                Assert.AreEqual(intent.District, form.District);

                if (form.IsGenerated)
                {
                    generated++;
                    ArchitectureCompiler.ValidateGenerated(intent, plan.Theme, form);
                    Assert.Greater(form.WidthDm, 0);
                    Assert.Greater(form.DepthDm, 0);
                    Assert.Greater(form.Storeys, 0);
                }
                else
                {
                    bespoke++;
                }
            }

            Assert.AreEqual(13, generated);
            Assert.AreEqual(4, bespoke);
        }
    }
}
