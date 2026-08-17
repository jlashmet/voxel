using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationExpansion260AuthoringEmitterTests
    {
        [Test]
        public void EnchanterRecipeProducesVisibleBoxBackendDescriptor()
        {
            var context = new DecorationContext
            {
                WorldSeed = 1,
                StructureId = 2,
                SpaceId = 3,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, 1),
                StructureKind = DecorationStructureKind.House,
                SpaceKind = DecorationSpaceKind.Study,
                Wealth = DecorationWealthTier.Wealthy,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };

            DecorationPropDescriptor descriptor = DecorationExpansion260Catalog.Describe(
                in context, 0xE2600004u, 1u, DecorationExpansion260Kind.EnchantersWorkbench);

            Assert.That(descriptor.IsWellFormed, Is.True);
            Assert.That(descriptor.Backend, Is.EqualTo(DecorationRenderBackend.BoxAssembly));
            Assert.That(DecorationExpansion260Variants.IsExpansion260(descriptor.Variant), Is.True);
            Assert.That(DecorationExpansion260Variants.KindOf(descriptor.Variant), Is.EqualTo(DecorationExpansion260Kind.EnchantersWorkbench));
            Assert.That(math.all(descriptor.Size > 0), Is.True);
        }
    }
}
