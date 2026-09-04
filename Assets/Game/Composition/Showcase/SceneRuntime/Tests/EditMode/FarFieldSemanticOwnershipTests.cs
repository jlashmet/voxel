using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFieldSemanticOwnershipTests
    {
        [Test]
        public void SemanticFeatureFootprintIsExcludedFromLegacyPositiveSilhouetteOwnership()
        {
            var semantic = new FeaturePresentationBake(
                sourceId: 17,
                revision: 3,
                kind: (FeatureKind)0,
                position: new int3(20, 0, 30),
                orientation: 0,
                boundsMin: new int3(10, -4, 20),
                boundsMax: new int3(30, 90, 40),
                primitives: new Primitive[1]);
            IReadOnlyList<FeaturePresentationBake> features =
                new List<FeaturePresentationBake> { semantic };

            Assert.That(FarFieldStructureStore.IsSemanticColumn(10, 20, features), Is.True,
                "The inclusive semantic footprint must own its own far presentation.");
            Assert.That(FarFieldStructureStore.IsSemanticColumn(30, 40, features), Is.True,
                "The inclusive semantic footprint must own its own far presentation.");
            Assert.That(FarFieldStructureStore.IsSemanticColumn(9, 20, features), Is.False);
            Assert.That(FarFieldStructureStore.IsSemanticColumn(31, 40, features), Is.False);
            Assert.That(FarFieldStructureStore.IsSemanticColumn(20, 41, features), Is.False);
            Assert.That(FarFieldStructureStore.IsSemanticColumn(20, 30, null), Is.False,
                "Anonymous authored surfaces still use the legacy fallback when no semantic owner exists.");
        }
    }
}
