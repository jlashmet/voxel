using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace Game.Materials.Tests
{
    public sealed class GameStructureMaterialTests
    {
        [Test]
        public void DefaultStructureRoles_ResolveOnlyCanonicalGameMaterials()
        {
            StructureMaterialSet roles = GameStructureMaterials.Default;
            byte[] materialIds =
            {
                roles.Void,
                roles.PrimaryMasonry,
                roles.Timber,
                roles.LooseAggregate,
                roles.TransparentInfill,
                roles.IndestructibleBase,
                roles.DarkMasonry,
                roles.SlateRoof,
                roles.TileRoof,
                roles.TextileAccent,
                roles.GroundCover,
                roles.Water,
                roles.MetalAccent,
                roles.Earth,
                roles.Overgrowth,
                roles.WarmWindow,
                roles.AeratedWater,
                roles.CoolEmissiveAccent,
                roles.FineMasonry,
                roles.MediumMasonry,
                roles.LargeMasonry,
                roles.PaleFlora,
            };

            Assert.That(materialIds.Length, Is.EqualTo(GameMaterialCatalogue.Count));
            for (int i = 0; i < materialIds.Length; i++)
            {
                byte materialId = materialIds[i];
                Assert.That(GameMaterialCatalogue.IsCanonicalId(materialId), Is.True,
                    $"Structure role {i} points outside the canonical game material catalogue.");
            }
        }

        [Test]
        public void EveryNonVoidStructureRole_HasAnExplicitSimulationDefinition()
        {
            StructureMaterialSet roles = GameStructureMaterials.Default;
            byte[] physicalRoles =
            {
                roles.PrimaryMasonry,
                roles.Timber,
                roles.LooseAggregate,
                roles.TransparentInfill,
                roles.IndestructibleBase,
                roles.DarkMasonry,
                roles.SlateRoof,
                roles.TileRoof,
                roles.TextileAccent,
                roles.GroundCover,
                roles.Water,
                roles.MetalAccent,
                roles.Earth,
                roles.Overgrowth,
                roles.WarmWindow,
                roles.AeratedWater,
                roles.CoolEmissiveAccent,
                roles.FineMasonry,
                roles.MediumMasonry,
                roles.LargeMasonry,
                roles.PaleFlora,
            };

            for (int i = 0; i < physicalRoles.Length; i++)
            {
                ref readonly GameMaterialRuntimeDefinition definition =
                    ref GameMaterialRuntimeCatalogue.Get(physicalRoles[i]);
                Assert.That(definition.HasSimulation, Is.True,
                    $"Structure role {i} has no explicit simulation projection.");
            }
        }

        [Test]
        public void DefaultStructureRoles_PreserveCurrentStableBindings()
        {
            StructureMaterialSet roles = GameStructureMaterials.Default;
            Assert.That(roles.Void, Is.EqualTo(GameMaterialIds.Empty));
            Assert.That(roles.PrimaryMasonry, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(roles.Timber, Is.EqualTo(GameMaterialIds.Wood));
            Assert.That(roles.LooseAggregate, Is.EqualTo(GameMaterialIds.Sand));
            Assert.That(roles.TransparentInfill, Is.EqualTo(GameMaterialIds.Glass));
            Assert.That(roles.IndestructibleBase, Is.EqualTo(GameMaterialIds.Bedrock));
            Assert.That(roles.DarkMasonry, Is.EqualTo(GameMaterialIds.DarkStone));
            Assert.That(roles.SlateRoof, Is.EqualTo(GameMaterialIds.Slate));
            Assert.That(roles.TileRoof, Is.EqualTo(GameMaterialIds.Tile));
            Assert.That(roles.TextileAccent, Is.EqualTo(GameMaterialIds.Cloth));
            Assert.That(roles.GroundCover, Is.EqualTo(GameMaterialIds.Grass));
            Assert.That(roles.Water, Is.EqualTo(GameMaterialIds.Water));
            Assert.That(roles.MetalAccent, Is.EqualTo(GameMaterialIds.Gold));
            Assert.That(roles.Earth, Is.EqualTo(GameMaterialIds.Dirt));
            Assert.That(roles.Overgrowth, Is.EqualTo(GameMaterialIds.Moss));
            Assert.That(roles.WarmWindow, Is.EqualTo(GameMaterialIds.LitWindow));
            Assert.That(roles.AeratedWater, Is.EqualTo(GameMaterialIds.Cascade));
            Assert.That(roles.CoolEmissiveAccent, Is.EqualTo(GameMaterialIds.Crystal));
            Assert.That(roles.FineMasonry, Is.EqualTo(GameMaterialIds.MasonrySmall));
            Assert.That(roles.MediumMasonry, Is.EqualTo(GameMaterialIds.MasonryMedium));
            Assert.That(roles.LargeMasonry, Is.EqualTo(GameMaterialIds.MasonryLarge));
            Assert.That(roles.PaleFlora, Is.EqualTo(GameMaterialIds.FlowerWhite));
        }
    }
}
