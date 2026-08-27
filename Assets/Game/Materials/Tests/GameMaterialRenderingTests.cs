using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;

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
        public void GrassAndMossCoatingShareAuthoredTextureDensity()
        {
            MaterialPresentationDefinition grass =
                GameMaterialRenderingDefinitions.Create()[GameMaterialIds.Grass];
            UnityEngine.Vector4 mossSampling = VoxelPresentationCatalogue.CoatingSampling[1];

            Assert.That(mossSampling.x, Is.EqualTo(grass.Sampling.x),
                "The renderer-owned moss coating must reuse the authored grass texture layer.");
            Assert.That(mossSampling.y, Is.EqualTo(grass.Surface.x).Within(0.0001f),
                "Crossing a moss coating boundary must not change the apparent grass motif size.");
        }

        [Test]
        public void TerrainRowsKeepResolvableDetailWithoutDominantNormalRelief()
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
                // Terrain still needs normal relief at walking distance, but the source normal map
                // must not dominate the geometric normal. At 0.24 the near-only normal path reads
                // as bluish repeated swirls from elevated views; the old 0.035 treatment was too
                // flat. Keep the authored compromise narrow so either regression is caught.
                Assert.That(row.Surface.y, Is.InRange(0.05f, 0.08f));
                Assert.That(row.Surface.w, Is.EqualTo(1f),
                    "Terrain detail should modulate luminance without importing source hue.");

                // Keep the albedo/luminance source resolvable independently of normal strength.
                // Re-enlarging the tile would hide the texture and recreate the older flat-ground
                // regression even if the normal relief itself remained correct.
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
