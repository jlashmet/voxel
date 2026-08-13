using System.Linq;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeArchitectureBoundaryTests
    {
        [Test]
        public void KentridgeContentSuppliesIntentAndArchitectureOwnsDetail()
        {
            Assert.AreEqual(
                "MountingForce.WorldGen.Core",
                typeof(KentridgeDefinition).Assembly.GetName().Name,
                "Kentridge settlement planning must remain in the high-level Core/content assembly.");

            Assert.AreEqual(
                "MountingForce.WorldGen.Architecture",
                typeof(KentridgeBuildingGrammar).Assembly.GetName().Name,
                "Per-building roof/window/facade generation must live in the lower architecture assembly.");

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
        public void BuildingPlotsContainStructureIntentNotArchitecturalDetail()
        {
            var fields = typeof(BuildingPlot)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[] { "RoleId", "Archetype", "District", "PositionDm", "Frontage" },
                fields,
                "A BuildingPlot is the settlement-to-architecture contract: identity, use, placement and frontage only.");
        }
    }
}
