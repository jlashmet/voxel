using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Rendering.Api;

namespace Game.Materials.Tests
{
    public sealed class GameMaterialRenderingTests
    {
        [Test]
        public void RenderingDefinitions_CoverEveryCanonicalIdExactlyOnce()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();
            Assert.That(definitions.Length, Is.EqualTo(GameMaterialCatalogue.Count));

            var seen = new bool[GameMaterialCatalogue.Count];
            for (int i = 0; i < definitions.Length; i++)
            {
                byte materialId = definitions[i].MaterialIndex;
                Assert.That(materialId, Is.LessThan(GameMaterialCatalogue.Count));
                Assert.That(seen[materialId], Is.False,
                    $"Duplicate rendering definition for {GameMaterialCatalogue.NameOf(materialId)} ({materialId}).");
                seen[materialId] = true;
            }

            for (byte materialId = 0; materialId < GameMaterialCatalogue.Count; materialId++)
                Assert.That(seen[materialId], Is.True,
                    $"Missing rendering definition for {GameMaterialCatalogue.NameOf(materialId)} ({materialId}).");
        }

        [Test]
        public void RenderingDefinitions_PreserveCurrentShowcaseRows()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();

            MaterialPresentationDefinition stone = definitions[GameMaterialIds.Stone];
            Assert.That(stone.MaterialIndex, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(stone.Sampling.x, Is.EqualTo(0f));
            Assert.That(stone.Sampling.z, Is.EqualTo((float)MaterialTextureProjection.Triplanar));
            Assert.That(stone.Albedo.x, Is.EqualTo(0.43f).Within(0.0001f));

            MaterialPresentationDefinition wood = definitions[GameMaterialIds.Wood];
            Assert.That(wood.Sampling.x, Is.EqualTo(1f));
            Assert.That(wood.Sampling.z, Is.EqualTo((float)MaterialTextureProjection.Face));

            MaterialPresentationDefinition flower = definitions[GameMaterialIds.FlowerWhite];
            Assert.That(flower.Albedo.x, Is.EqualTo(1f));
            Assert.That(flower.Albedo.y, Is.EqualTo(1f));
            Assert.That(flower.Albedo.z, Is.EqualTo(1f));
        }
    }
}
