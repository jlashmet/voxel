using Game.WorldBuilder.Api;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel.Tests.EditMode
{
    /// <summary>
    /// Independent consumer proof for the semantic mountain climate contract. The fixture deliberately
    /// reuses one physical surface with materially different climate policies so presentation cannot
    /// become a second source of mountain shape authority.
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
    }
}
