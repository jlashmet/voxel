using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Core.Vegetation;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VegetationPlacementTests
    {
        [Test]
        public void Placement_IsDeterministicForSameSeedAndSamples()
        {
            var samples = new List<VegetationSurfaceSample>
            {
                Sample(new float3(1f, 2f, 3f), VegetationSurface.Ground, 0.4f, 0.2f),
                Sample(new float3(4f, 2f, 8f), VegetationSurface.Ground, 0.8f, 0.7f),
                Sample(new float3(6f, 3f, 1f), VegetationSurface.Rock, 0.9f, 0.9f),
            };
            var settings = VegetationPlacementSettings.Default(12345u);
            settings.Density = 1f;

            var a = new List<VegetationInstance>();
            var b = new List<VegetationInstance>();
            VegetationPlacement.Generate(samples, settings, a);
            VegetationPlacement.Generate(samples, settings, b);

            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Kind, Is.EqualTo(a[i].Kind));
                Assert.That(b[i].Seed, Is.EqualTo(a[i].Seed));
                Assert.That(math.distance(b[i].PositionMetres, a[i].PositionMetres), Is.LessThan(0.0001f));
                Assert.That(b[i].Scale, Is.EqualTo(a[i].Scale).Within(0.0001f));
            }
        }

        [Test]
        public void WetShadedMasonry_SelectsSurfaceGrowingVegetation()
        {
            var samples = new List<VegetationSurfaceSample>
            {
                new VegetationSurfaceSample
                {
                    PositionMetres = new float3(2f, 4f, 7f),
                    Normal = new float3(0f, 0f, 1f),
                    Surface = VegetationSurface.Masonry,
                    Moisture = 1f,
                    Shade = 1f,
                },
            };
            var settings = VegetationPlacementSettings.Default(7u);
            settings.Density = 1f;
            var output = new List<VegetationInstance>();

            VegetationPlacement.Generate(samples, settings, output);

            Assert.That(output.Count, Is.EqualTo(1));
            VegetationProfile profile = VegetationCatalogue.Get(output[0].Kind);
            Assert.That(profile.MasonryWeight, Is.GreaterThan(0f));
            Assert.That(profile.GrowthForm, Is.AnyOf(
                VegetationGrowthForm.Creeper,
                VegetationGrowthForm.Climber,
                VegetationGrowthForm.Hanger,
                VegetationGrowthForm.Frond,
                VegetationGrowthForm.Fungus));
        }

        [Test]
        public void GroundPlants_AreRejectedOnSteepGround()
        {
            var samples = new List<VegetationSurfaceSample>
            {
                new VegetationSurfaceSample
                {
                    PositionMetres = new float3(0f),
                    Normal = math.normalize(new float3(1f, 0.05f, 0f)),
                    Surface = VegetationSurface.Ground,
                    Moisture = 0.5f,
                    Shade = 0.5f,
                },
            };
            var settings = VegetationPlacementSettings.Default(99u);
            settings.Density = 1f;
            settings.MaxGroundSlopeDegrees = 40f;
            var output = new List<VegetationInstance>();

            VegetationPlacement.Generate(samples, settings, output);

            Assert.That(output, Is.Empty);
        }

        [Test]
        public void MundaneHabitat_NeverProducesMagicalSpecies()
        {
            var samples = new List<VegetationSurfaceSample>();
            for (int i = 0; i < 128; i++)
            {
                samples.Add(Sample(new float3(i * 0.5f, 0f, i % 9), VegetationSurface.Ground, 0.65f, 0.45f, 0f));
            }

            var settings = VegetationPlacementSettings.Default(554433u);
            settings.Density = 1f;
            var output = new List<VegetationInstance>();
            VegetationPlacement.Generate(samples, settings, output);

            Assert.That(output.Count, Is.GreaterThan(0));
            for (int i = 0; i < output.Count; i++)
            {
                Assert.That(VegetationCatalogue.HasTrait(output[i].Kind, VegetationTraits.Magical), Is.False,
                    $"Mundane habitat produced magical vegetation {output[i].Kind}");
            }
        }

        [Test]
        public void ArcaneHabitat_CanProduceMagicalSpeciesDeterministically()
        {
            var samples = new List<VegetationSurfaceSample>();
            for (int i = 0; i < 128; i++)
            {
                samples.Add(Sample(new float3(i * 0.5f, 0f, i % 11), VegetationSurface.Ground, 0.65f, 0.55f, 1f));
            }

            var settings = VegetationPlacementSettings.Default(991122u);
            settings.Density = 1f;
            settings.ArcaneBias = 1f;
            var output = new List<VegetationInstance>();
            VegetationPlacement.Generate(samples, settings, output);

            bool foundMagical = false;
            for (int i = 0; i < output.Count; i++)
            {
                foundMagical |= VegetationCatalogue.HasTrait(output[i].Kind, VegetationTraits.Magical);
            }

            Assert.That(foundMagical, Is.True);
        }

        [Test]
        public void Catalogue_MapsSpeciesToReusableGrowthForms()
        {
            Assert.That(VegetationCatalogue.GrowthForm(VegetationKind.Grass), Is.EqualTo(VegetationGrowthForm.Tuft));
            Assert.That(VegetationCatalogue.GrowthForm(VegetationKind.Ivy), Is.EqualTo(VegetationGrowthForm.Climber));
            Assert.That(VegetationCatalogue.GrowthForm(VegetationKind.HangingVine), Is.EqualTo(VegetationGrowthForm.Hanger));
            Assert.That(VegetationCatalogue.GrowthForm(VegetationKind.StarMoss), Is.EqualTo(VegetationGrowthForm.Creeper));
            Assert.That(VegetationCatalogue.GrowthForm(VegetationKind.Glowshroom), Is.EqualTo(VegetationGrowthForm.Fungus));
        }

        [Test]
        public void VineGrowth_IsDeterministicAndStaysNearSupportPlane()
        {
            var settings = VineGrowthSettings.Default(55u);
            settings.LengthMetres = 6f;
            settings.SegmentCount = 18;
            settings.SurfaceAttraction = 1f;
            var a = new List<float3>();
            var b = new List<float3>();
            float3 anchor = new float3(0f, 5f, 0f);
            float3 normal = new float3(0f, 0f, 1f);

            VineGrowth.Generate(anchor, normal, settings, a);
            VineGrowth.Generate(anchor, normal, settings, b);

            Assert.That(a.Count, Is.EqualTo(19));
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(math.distance(a[i], b[i]), Is.LessThan(0.0001f));
                Assert.That(math.abs(math.dot(a[i] - anchor, normal)), Is.LessThan(0.05f));
            }
            Assert.That(a[a.Count - 1].y, Is.LessThan(anchor.y));
        }

        private static VegetationSurfaceSample Sample(
            float3 position,
            VegetationSurface surface,
            float moisture,
            float shade,
            float arcane = 0f)
        {
            return new VegetationSurfaceSample
            {
                PositionMetres = position,
                Normal = new float3(0f, 1f, 0f),
                Surface = surface,
                Moisture = moisture,
                Shade = shade,
                ArcaneSaturation = arcane,
            };
        }
    }
}
