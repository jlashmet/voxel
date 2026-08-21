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

        [Test]
        public void TerrainRowsAreColourLedAndWarmWindowsReadAsLit()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();

            foreach (byte terrain in new[]
                     {
                         GameMaterialIds.Sand,
                         GameMaterialIds.Grass,
                         GameMaterialIds.Dirt,
                     })
            {
                MaterialPresentationDefinition row = definitions[terrain];
                Assert.That(row.Sampling.z,
                    Is.EqualTo((float)MaterialTextureProjection.Triplanar));
                Assert.That(row.Sampling.w, Is.LessThanOrEqualTo(0.16f));
                // Enough relief for ground to catch light, well short of a photoscan. It was
                // capped at 0.04, which is close enough to zero that terrain lit like sheet
                // plastic — the flatness read as missing textures rather than as a stylised look.
                Assert.That(row.Surface.y, Is.InRange(0.12f, 0.35f));
                Assert.That(row.Surface.w, Is.EqualTo(1f),
                    "Terrain detail should modulate luminance without importing source hue.");

                // The cap above is only meaningful while the source is resolvable. A tile stretched
                // across tens of metres leaves the luminance detail nothing to modulate, which is
                // how ground ended up looking untextured despite every row here being correct.
                Assert.That(row.Surface.x, Is.GreaterThan(1f / 16f),
                    "Ground texture is tiled too large to resolve at eye level.");
            }

            MaterialPresentationDefinition window = definitions[GameMaterialIds.LitWindow];
            Assert.That(window.Albedo.x, Is.GreaterThan(0.9f));
            Assert.That(window.Albedo.y, Is.GreaterThan(0.45f));
            Assert.That(window.Albedo.z, Is.LessThan(0.25f));
        }
    }
}
