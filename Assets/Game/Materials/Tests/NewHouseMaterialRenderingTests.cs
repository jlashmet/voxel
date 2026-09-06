using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Rendering.Api;

namespace Game.Materials.Tests
{
    public sealed class NewHouseMaterialRenderingTests
    {
        [Test]
        public void HouseRows_AreCanonicalConsecutiveOpaqueTextureLayers()
        {
            MaterialPresentationDefinition[] rows = GameMaterialRenderingDefinitions.Create();
            byte[] ids =
            {
                GameMaterialIds.HousePlaster,
                GameMaterialIds.HouseTimber,
                GameMaterialIds.HouseRoof,
                GameMaterialIds.HouseStone,
                GameMaterialIds.HouseDoor,
                GameMaterialIds.HouseFoliage,
            };

            for (int i = 0; i < ids.Length; i++)
            {
                byte id = ids[i];
                MaterialPresentationDefinition row = rows[id];
                Assert.That(GameMaterialCatalogue.IsCanonicalId(id), Is.True);
                Assert.That(row.MaterialIndex, Is.EqualTo(id));
                Assert.That(row.Sampling.x, Is.EqualTo(8 + i),
                    "Game composition owns the house role to opaque renderer-layer mapping.");
                Assert.That(row.Sampling.w, Is.EqualTo(1f),
                    "Supplied authored house textures must be fully represented, not weak tint detail.");
                Assert.That(row.Surface.y, Is.EqualTo(0f),
                    "The supplied house set has no paired normal maps; normal response must remain neutral.");
            }
        }

        [Test]
        public void HouseRows_PreserveReferenceAppropriateProjectionAndScale()
        {
            MaterialPresentationDefinition[] rows = GameMaterialRenderingDefinitions.Create();

            Assert.That(rows[GameMaterialIds.HousePlaster].Sampling.z,
                Is.EqualTo((float)MaterialTextureProjection.Triplanar));
            Assert.That(rows[GameMaterialIds.HouseStone].Sampling.z,
                Is.EqualTo((float)MaterialTextureProjection.Triplanar));
            Assert.That(rows[GameMaterialIds.HouseFoliage].Sampling.z,
                Is.EqualTo((float)MaterialTextureProjection.Triplanar));

            Assert.That(rows[GameMaterialIds.HouseTimber].Sampling.z,
                Is.EqualTo((float)MaterialTextureProjection.Face));
            Assert.That(rows[GameMaterialIds.HouseRoof].Sampling.z,
                Is.EqualTo((float)MaterialTextureProjection.Face));
            Assert.That(rows[GameMaterialIds.HouseDoor].Sampling.z,
                Is.EqualTo((float)MaterialTextureProjection.Face));

            foreach (byte id in new[]
                     {
                         GameMaterialIds.HousePlaster,
                         GameMaterialIds.HouseTimber,
                         GameMaterialIds.HouseRoof,
                         GameMaterialIds.HouseStone,
                         GameMaterialIds.HouseDoor,
                         GameMaterialIds.HouseFoliage,
                     })
            {
                Assert.That(rows[id].Surface.x, Is.InRange(1f / 16f, 1f / 7f),
                    "House motifs must resolve at the 10 cm voxel scale rather than stretching across the facade.");
            }
        }

        [Test]
        public void PaintedHouseAccent_UsesBlueBaseWithSuppliedLuminanceDetail()
        {
            MaterialPresentationDefinition row =
                GameMaterialRenderingDefinitions.Create()[GameMaterialIds.HouseDoor];

            Assert.That(row.Sampling.x, Is.EqualTo(12f),
                "The painted architectural role must continue to sample the supplied ornamental plate.");
            Assert.That(row.Sampling.w, Is.EqualTo(1f));
            Assert.That(row.Albedo.x, Is.EqualTo(0.12f));
            Assert.That(row.Albedo.y, Is.EqualTo(0.42f));
            Assert.That(row.Albedo.z, Is.EqualTo(0.82f),
                "The reference shutters require a saturated blue game-owned base colour.");
            Assert.That(row.Surface.w, Is.EqualTo(1f),
                "The dark/gold supplied plate must contribute luminance/detail rather than replace the blue paint chroma.");
            Assert.That(row.Variation.y, Is.EqualTo(1f));
            Assert.That(row.Variation.z, Is.EqualTo(0f),
                "Texture chroma must not leak the supplied plate's gold/black colour back into the painted accent.");
        }
    }
}
