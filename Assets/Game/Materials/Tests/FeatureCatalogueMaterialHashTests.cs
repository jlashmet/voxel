using Game.Materials.Api;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.Materials.Tests
{
    public sealed class FeatureCatalogueMaterialHashTests
    {
        [Test]
        public void ComputeHash_ChangesWhenOnlyMaterialMappingChanges()
        {
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 0,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: 0,
                materials: 1,
                explicitPlacements: 0,
                overrides: 0,
                allocator: Allocator.Temp);

            try
            {
                catalogue.Materials[0] = GameMaterialIds.Stone;
                ulong stoneHash = FeatureCatalogueBuilder.ComputeHash(in catalogue);
                ulong repeatedStoneHash = FeatureCatalogueBuilder.ComputeHash(in catalogue);

                catalogue.Materials[0] = GameMaterialIds.Wood;
                ulong woodHash = FeatureCatalogueBuilder.ComputeHash(in catalogue);

                Assert.That(repeatedStoneHash, Is.EqualTo(stoneHash),
                    "Catalogue hashing must remain deterministic for identical material mappings.");
                Assert.That(woodHash, Is.Not.EqualTo(stoneHash),
                    "Changing only the semantic material mapping must change world identity.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }
    }
}
