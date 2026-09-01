using Game.WorldBuilder.Api;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel.Tests.EditMode
{
    /// <summary>
    /// Independent consumer proof for the semantic mountain climate contract. The fixtures keep
    /// shape and presentation independently configurable and use no Showcase policy.
    /// </summary>
    public sealed class MountainClimateReuseTests
    {
        [Test]
        public void OneLandformSupportsDifferentClimatesWithoutChangingOccupancyInstructions()
        {
            var spec = new MountainLandformSpec(
                originXdm: -140,
                originYdm: 28,
                originZdm: 220,
                radiusXdm: 360,
                radiusZdm: 470,
                heightDm: 310,
                summitRadiusDm: 58,
                macroShape: MountainMacroShape.Ridged,
                summitCharacter: MountainSummitCharacter.Craggy,
                seed: 1907u,
                ridgeCount: 6,
                ridgeStrengthPermille: 820,
                asymmetryXPermille: 240,
                asymmetryZPermille: -180,
                roughnessAmplitudeDm: 44,
                roughnessScaleDm: 52,
                erosionStrengthPermille: 520);
            var surface = new MountainLandformSurface(in spec);
            var palette = new MountainLandformPalette(
                groundCoverMaterial: 4,
                rockMaterial: 7,
                snowMaterial: 9);
            var dryClimate = new MountainClimateProfile(
                groundCoverCeilingPermille: 420,
                snowLinePermille: 910,
                steepRockSlopePermille: 1500);
            var alpineClimate = new MountainClimateProfile(
                groundCoverCeilingPermille: 220,
                snowLinePermille: 560,
                steepRockSlopePermille: 850);

            FeatureCatalogue dry = WorldBuilderMountainLandformCatalogue.Build(
                surface, dryClimate, in palette, Allocator.Temp);
            FeatureCatalogue alpine = WorldBuilderMountainLandformCatalogue.Build(
                surface, alpineClimate, in palette, Allocator.Temp);
            try
            {
                int occupancyLength = surface.MassCount * ShapeOps.InstructionLength(ShapeOp.EmitFrustum);
                Assert.That(occupancyLength, Is.GreaterThan(0));
                Assert.That(dry.Program.Length, Is.GreaterThan(occupancyLength));
                Assert.That(alpine.Program.Length, Is.GreaterThan(occupancyLength));

                for (int i = 0; i < occupancyLength; i++)
                    Assert.That(alpine.Program[i], Is.EqualTo(dry.Program[i]),
                        $"climate changed physical mountain occupancy instruction {i}");

                bool presentationDiffers = dry.Program.Length != alpine.Program.Length;
                int commonLength = System.Math.Min(dry.Program.Length, alpine.Program.Length);
                for (int i = occupancyLength; i < commonLength && !presentationDiffers; i++)
                    presentationDiffers = dry.Program[i] != alpine.Program[i];

                Assert.That(presentationDiffers, Is.True,
                    "independent climate profiles should produce materially different surface presentation");
                Assert.That(dryClimate.RoleAt(700, 300), Is.EqualTo(MountainSurfaceRole.Rock));
                Assert.That(alpineClimate.RoleAt(700, 300), Is.EqualTo(MountainSurfaceRole.Snow));
            }
            finally
            {
                alpine.Dispose();
                dry.Dispose();
            }
        }

        [Test]
        public void SameBuilderSupportsMateriallyDifferentShapeAndClimateCombinations()
        {
            var broadSpec = new MountainLandformSpec(
                originXdm: 80,
                originYdm: 16,
                originZdm: -40,
                radiusXdm: 620,
                radiusZdm: 540,
                heightDm: 250,
                summitRadiusDm: 125,
                macroShape: MountainMacroShape.Massif,
                summitCharacter: MountainSummitCharacter.Broad,
                seed: 1223u,
                ridgeCount: 2,
                ridgeStrengthPermille: 240,
                asymmetryXPermille: 0,
                asymmetryZPermille: 0,
                roughnessAmplitudeDm: 14,
                roughnessScaleDm: 90,
                erosionStrengthPermille: 760);
            var narrowSpec = new MountainLandformSpec(
                originXdm: -310,
                originYdm: 34,
                originZdm: 190,
                radiusXdm: 300,
                radiusZdm: 520,
                heightDm: 360,
                summitRadiusDm: 48,
                macroShape: MountainMacroShape.Ridged,
                summitCharacter: MountainSummitCharacter.Craggy,
                seed: 9929u,
                ridgeCount: 8,
                ridgeStrengthPermille: 900,
                asymmetryXPermille: 310,
                asymmetryZPermille: -250,
                roughnessAmplitudeDm: 54,
                roughnessScaleDm: 46,
                erosionStrengthPermille: 430);
            var broadSurface = new MountainLandformSurface(in broadSpec);
            var narrowSurface = new MountainLandformSurface(in narrowSpec);
            var broadClimate = new MountainClimateProfile(
                groundCoverCeilingPermille: 500,
                snowLinePermille: 940,
                steepRockSlopePermille: 1600);
            var narrowClimate = new MountainClimateProfile(
                groundCoverCeilingPermille: 190,
                snowLinePermille: 540,
                steepRockSlopePermille: 820);
            var palette = new MountainLandformPalette(
                groundCoverMaterial: 3,
                rockMaterial: 6,
                snowMaterial: 10);

            FeatureCatalogue broad = WorldBuilderMountainLandformCatalogue.Build(
                broadSurface, broadClimate, in palette, Allocator.Temp);
            FeatureCatalogue narrow = WorldBuilderMountainLandformCatalogue.Build(
                narrowSurface, narrowClimate, in palette, Allocator.Temp);
            try
            {
                Assert.That(narrowSurface.MassCount, Is.GreaterThan(broadSurface.MassCount),
                    "ridged/craggy fixture should realize a materially more articulated mass family");
                Assert.That(narrowSurface.GetMass(0).BaseRadiusDm,
                    Is.LessThan(broadSurface.GetMass(0).BaseRadiusDm));
                Assert.That(broadClimate.RoleAt(700, 300), Is.EqualTo(MountainSurfaceRole.Rock));
                Assert.That(narrowClimate.RoleAt(700, 300), Is.EqualTo(MountainSurfaceRole.Snow));
                Assert.That(broad.Definitions[0].MaxPrimitives,
                    Is.LessThanOrEqualTo(FeatureBudget.MaxPrimitivesPerInstance));
                Assert.That(narrow.Definitions[0].MaxPrimitives,
                    Is.LessThanOrEqualTo(FeatureBudget.MaxPrimitivesPerInstance));
                Assert.That(narrow.Program.Length, Is.Not.EqualTo(broad.Program.Length),
                    "materially different shape/climate combinations should not collapse to one catalogue");
            }
            finally
            {
                narrow.Dispose();
                broad.Dispose();
            }
        }
    }
}
