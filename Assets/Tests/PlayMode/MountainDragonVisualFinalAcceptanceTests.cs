using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Final visual-quality wrapper for Mountain Dragon. It proves that the reusable WorldBuilder
    /// realization gains material separation without changing any authored geometry/traversal
    /// instruction, then executes the established structural/bake acceptance before player replay.
    /// </summary>
    public sealed class MountainDragonVisualFinalAcceptanceTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte RockMaterial = 6;
        private const byte GroundCoverMaterial = 14;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;

        [Test]
        public void ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay()
        {
            MountainMaterialRolesSeparateRockGroundCoverPathAndPlaceholderWithoutChangingGeometry();
            new MountainDragonFinalAcceptanceTests()
                .NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay();
        }

        [Test]
        public void MountainMaterialRolesSeparateRockGroundCoverPathAndPlaceholderWithoutChangingGeometry()
        {
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            var materials = new MountainLandmarkMaterialSet(
                RockMaterial,
                GroundCoverMaterial,
                PathMaterial,
                DragonMaterial);

            FeatureCatalogue baseline = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                RockMaterial,
                PathMaterial,
                DragonMaterial,
                Allocator.Temp);
            FeatureCatalogue naturalized = WorldBuilderMountainLandmarkMaterialCatalogue.Build(
                in spec,
                in materials,
                Allocator.Temp);

            try
            {
                FeatureDefinition baselineLandform = baseline.Definitions[0];
                FeatureDefinition naturalizedLandform = naturalized.Definitions[0];
                Assert.That(naturalizedLandform.ProgramLength, Is.EqualTo(baselineLandform.ProgramLength));
                Assert.That(naturalizedLandform.MaxPrimitives, Is.EqualTo(baselineLandform.MaxPrimitives),
                    "Material-role naturalization must not add geometry or bake-cost primitives.");

                int baselinePc = baselineLandform.ProgramOffset;
                int naturalizedPc = naturalizedLandform.ProgramOffset;
                int end = baselinePc + baselineLandform.ProgramLength;
                int groundCoverFrustums = 0;
                int rockCoreFrustums = 0;
                int pathPrimitives = 0;

                while (baselinePc < end)
                {
                    ShapeOp baselineOp = (ShapeOp)baseline.Program[baselinePc];
                    ShapeOp naturalizedOp = (ShapeOp)naturalized.Program[naturalizedPc];
                    Assert.That(naturalizedOp, Is.EqualTo(baselineOp),
                        "Material-role composition must preserve the authored primitive sequence.");
                    if (baselineOp == ShapeOp.End) break;

                    int length = ShapeOps.InstructionLength(baselineOp);
                    Assert.That(length, Is.GreaterThan(0));

                    bool recoloredGroundCover = baselineOp == ShapeOp.EmitFrustum
                        && (PrimitiveMode)baseline.Program[baselinePc + 12] == PrimitiveMode.FillIfEmpty
                        && baseline.Program[baselinePc + 9] == RockMaterial;

                    for (int field = 0; field < length; field++)
                    {
                        int expected = baseline.Program[baselinePc + field];
                        if (recoloredGroundCover && field == 9)
                            expected = GroundCoverMaterial;

                        Assert.That(naturalized.Program[naturalizedPc + field], Is.EqualTo(expected),
                            $"Only the additive shoulder/support frustum material may change; "
                            + $"opcode {baselineOp}, field {field} diverged.");
                    }

                    if (baselineOp == ShapeOp.EmitFrustum)
                    {
                        PrimitiveMode mode = (PrimitiveMode)naturalized.Program[naturalizedPc + 12];
                        int material = naturalized.Program[naturalizedPc + 9];
                        if (mode == PrimitiveMode.Fill && material == RockMaterial)
                            rockCoreFrustums++;
                        if (mode == PrimitiveMode.FillIfEmpty && material == GroundCoverMaterial)
                            groundCoverFrustums++;
                    }
                    else if (baselineOp == ShapeOp.EmitRamp
                             && naturalized.Program[naturalizedPc + 9] == PathMaterial)
                    {
                        pathPrimitives++;
                    }
                    else if (baselineOp == ShapeOp.EmitBox
                             && naturalized.Program[naturalizedPc + 8] == PathMaterial)
                    {
                        pathPrimitives++;
                    }

                    baselinePc += length;
                    naturalizedPc += length;
                }

                Assert.That(rockCoreFrustums, Is.EqualTo(1),
                    "The structural mountain core must remain the distinct rock material.");
                Assert.That(groundCoverFrustums, Is.GreaterThanOrEqualTo(3),
                    "Asymmetric shoulders and tapered support banks must visibly separate from rock.");
                Assert.That(pathPrimitives, Is.GreaterThan(spec.SwitchbackCount),
                    "The winding road must retain its independent path material across ramps and landings.");

                FeatureDefinition placeholder = naturalized.Definitions[1];
                int placeholderPc = placeholder.ProgramOffset;
                Assert.That((ShapeOp)naturalized.Program[placeholderPc], Is.EqualTo(ShapeOp.EmitBox));
                Assert.That(naturalized.Program[placeholderPc + 8], Is.EqualTo(DragonMaterial),
                    "The issue explicitly permits the existing red cube dragon placeholder; "
                    + "mountain naturalization must not recolor it.");
            }
            finally
            {
                naturalized.Dispose();
                baseline.Dispose();
            }
        }
    }
}
