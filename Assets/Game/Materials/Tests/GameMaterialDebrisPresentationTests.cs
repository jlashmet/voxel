using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;

namespace Game.Materials.Tests
{
    public sealed class GameMaterialDebrisPresentationTests
    {
        [Test]
        public void ImpulseScale_PreservesExistingOrganicMaterialBehavior()
        {
            Assert.That(GameMaterialDebrisPresentation.ImpulseScale(GameMaterialIds.Wood), Is.EqualTo(0.58f));
            Assert.That(GameMaterialDebrisPresentation.ImpulseScale(GameMaterialIds.Cloth), Is.EqualTo(0.50f));
            Assert.That(GameMaterialDebrisPresentation.ImpulseScale(GameMaterialIds.Grass), Is.EqualTo(0.38f));
            Assert.That(GameMaterialDebrisPresentation.ImpulseScale(GameMaterialIds.Moss), Is.EqualTo(0.45f));
            Assert.That(GameMaterialDebrisPresentation.ImpulseScale(GameMaterialIds.Stone), Is.EqualTo(1f));
        }

        [Test]
        public void Colour_PreservesRepresentativeExistingDebrisRows()
        {
            const float alpha = 2.5f;

            var wood = GameMaterialDebrisPresentation.Colour(GameMaterialIds.Wood, alpha);
            Assert.That(wood.x, Is.EqualTo(0.43f));
            Assert.That(wood.y, Is.EqualTo(0.25f));
            Assert.That(wood.z, Is.EqualTo(0.12f));
            Assert.That(wood.w, Is.EqualTo(alpha));

            var crystal = GameMaterialDebrisPresentation.Colour(GameMaterialIds.Crystal, alpha);
            Assert.That(crystal.x, Is.EqualTo(0.08f));
            Assert.That(crystal.y, Is.EqualTo(0.56f));
            Assert.That(crystal.z, Is.EqualTo(0.82f));
            Assert.That(crystal.w, Is.EqualTo(alpha));

            var masonry = GameMaterialDebrisPresentation.Colour(GameMaterialIds.MasonryMedium, alpha);
            Assert.That(masonry.x, Is.EqualTo(0.68f));
            Assert.That(masonry.y, Is.EqualTo(0.58f));
            Assert.That(masonry.z, Is.EqualTo(0.42f));
        }
    }
}
