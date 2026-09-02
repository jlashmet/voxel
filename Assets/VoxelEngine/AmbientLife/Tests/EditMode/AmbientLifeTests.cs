using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.AmbientLife.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class AmbientLifeTests
    {
        [Test]
        public void Population_IsDeterministicForSameSeedAndHabitat()
        {
            var habitats = new List<AmbientLifeHabitatSample>
            {
                Habitat(new float3(1f, 2f, 3f), moisture: 0.9f, shade: 0.6f, flower: 0.2f, water: 0.8f),
                Habitat(new float3(8f, 2f, 4f), moisture: 0.4f, shade: 0.1f, flower: 1f, water: 0.1f),
                Habitat(new float3(14f, 1f, 9f), moisture: 0.8f, shade: 0.8f, fungus: 1f),
            };
            var settings = AmbientLifePopulationSettings.Default(112233u);
            settings.Density = 1f;

            var a = new List<AmbientLifeCluster>();
            var b = new List<AmbientLifeCluster>();
            AmbientLifePopulation.Generate(habitats, settings, a);
            AmbientLifePopulation.Generate(habitats, settings, b);

            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Kind, Is.EqualTo(a[i].Kind));
                Assert.That(b[i].Seed, Is.EqualTo(a[i].Seed));
                Assert.That(b[i].Count, Is.EqualTo(a[i].Count));
                Assert.That(b[i].RadiusMetres, Is.EqualTo(a[i].RadiusMetres).Within(0.0001f));
                Assert.That(math.distance(b[i].PositionMetres, a[i].PositionMetres), Is.LessThan(0.0001f));
            }
        }

        [Test]
        public void MundaneHabitat_NeverProducesMagicalAmbientLife()
        {
            var habitats = new List<AmbientLifeHabitatSample>();
            for (int i = 0; i < 192; i++)
            {
                habitats.Add(Habitat(
                    new float3(i * 1.5f, 0f, i % 13),
                    moisture: 0.65f,
                    shade: 0.45f,
                    flower: 0.75f,
                    water: 0.45f,
                    fungus: 0.35f,
                    arcane: 0f));
            }

            var settings = AmbientLifePopulationSettings.Default(998877u);
            settings.Density = 1f;
            var output = new List<AmbientLifeCluster>();
            AmbientLifePopulation.Generate(habitats, settings, output);

            Assert.That(output.Count, Is.GreaterThan(0));
            for (int i = 0; i < output.Count; i++)
            {
                Assert.That(AmbientLifeCatalogue.HasTrait(output[i].Kind, AmbientLifeTraits.Magical), Is.False,
                    $"Mundane habitat produced magical ambient life {output[i].Kind}");
            }
        }

        [Test]
        public void ArcaneHabitat_CanProduceFantasyAmbientLife()
        {
            var habitats = new List<AmbientLifeHabitatSample>();
            for (int i = 0; i < 256; i++)
            {
                habitats.Add(Habitat(
                    new float3(i * 1.25f, 0f, i % 17),
                    moisture: 0.7f,
                    shade: 0.5f,
                    flower: 0.9f,
                    water: 0.5f,
                    fungus: 0.6f,
                    arcane: 1f));
            }

            var settings = AmbientLifePopulationSettings.Default(445566u);
            settings.Density = 1f;
            var output = new List<AmbientLifeCluster>();
            AmbientLifePopulation.Generate(habitats, settings, output);

            bool foundMagical = false;
            for (int i = 0; i < output.Count; i++)
            {
                foundMagical |= AmbientLifeCatalogue.HasTrait(output[i].Kind, AmbientLifeTraits.Magical);
            }

            Assert.That(foundMagical, Is.True);
        }

        [Test]
        public void SpeciesExposeActivityAndMovementWithoutSpecialCaseAgents()
        {
            AmbientLifeProfile firefly = AmbientLifeCatalogue.Get(AmbientLifeKind.Firefly);
            AmbientLifeProfile butterfly = AmbientLifeCatalogue.Get(AmbientLifeKind.Butterfly);
            AmbientLifeProfile wisp = AmbientLifeCatalogue.Get(AmbientLifeKind.Wisp);

            Assert.That((firefly.Activity & AmbientActivity.Night) != 0, Is.True);
            Assert.That((firefly.Activity & AmbientActivity.Day) != 0, Is.False);
            Assert.That(butterfly.Activity, Is.EqualTo(AmbientActivity.Day));
            Assert.That(butterfly.Movement, Is.EqualTo(AmbientMovementForm.Flutter));
            Assert.That((wisp.Traits & AmbientLifeTraits.Magical) != 0, Is.True);
            Assert.That((wisp.Traits & AmbientLifeTraits.Luminous) != 0, Is.True);
        }

        private static AmbientLifeHabitatSample Habitat(
            float3 position,
            float moisture = 0.5f,
            float shade = 0.5f,
            float flower = 0f,
            float water = 0f,
            float fungus = 0f,
            float deadwood = 0f,
            float arcane = 0f)
        {
            return new AmbientLifeHabitatSample
            {
                PositionMetres = position,
                RadiusMetres = 5f,
                Moisture = moisture,
                Shade = shade,
                FlowerDensity = flower,
                WaterPresence = water,
                FungusDensity = fungus,
                DeadwoodDensity = deadwood,
                ArcaneSaturation = arcane,
            };
        }
    }
}
