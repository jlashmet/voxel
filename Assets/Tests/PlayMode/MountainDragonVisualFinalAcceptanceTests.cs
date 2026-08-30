using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Final visual-quality wrapper for Mountain Dragon. It proves the reusable presentation layer
    /// can reshape repetitive support masses without adding primitives or changing traversal/path
    /// instructions, then executes the established structural/bake acceptance before player replay.
    /// </summary>
    public sealed class MountainDragonVisualFinalAcceptanceTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte RockMaterial = 6;
        private const byte GroundCoverMaterial = 14;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;
        private const int AsymmetricShoulderCount = 3;
        private const int MinimumPlaceholderCrestMargin = 12;

        [Test]
        public void ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay()
        {
            MountainPresentationNaturalizesSupportRidgesWithoutChangingTraversalOrBudget();
            new MountainDragonFinalAcceptanceTests()
                .NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay();
        }

        [Test]
        public void MountainPresentationNaturalizesSupportRidgesWithoutChangingTraversalOrBudget()
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
                    "Mountain presentation must not add geometry or bake-cost primitives.");

                int baselinePc = baselineLandform.ProgramOffset;
                int naturalizedPc = naturalizedLandform.ProgramOffset;
                int end = baselinePc + baselineLandform.ProgramLength;
                int additiveFrustumIndex = 0;
                int groundCoverFrustums = 0;
                int rockSupportFrustums = 0;
                int changedSupportFrustums = 0;
                int pairedSupportDuplicates = 0;
                int pathPrimitives = 0;
                int previousSupportHeight = -1;
                int previousSupportX = int.MinValue;
                int previousSupportZ = int.MinValue;
                int previousSupportBaseRadius = -1;
                int previousSupportTopRadius = -1;
                bool coreSeen = false;

                while (baselinePc < end)
                {
                    ShapeOp baselineOp = (ShapeOp)baseline.Program[baselinePc];
                    ShapeOp naturalizedOp = (ShapeOp)naturalized.Program[naturalizedPc];
                    Assert.That(naturalizedOp, Is.EqualTo(baselineOp),
                        "Naturalization must preserve the authored primitive sequence.");
                    if (baselineOp == ShapeOp.End) break;

                    int length = ShapeOps.InstructionLength(baselineOp);
                    Assert.That(length, Is.GreaterThan(0));

                    if (baselineOp == ShapeOp.EmitFrustum)
                    {
                        PrimitiveMode baselineMode = (PrimitiveMode)baseline.Program[baselinePc + 12];
                        int baselineMaterial = baseline.Program[baselinePc + 9];

                        if (!coreSeen
                            && baselineMode == PrimitiveMode.Fill
                            && baselineMaterial == RockMaterial)
                        {
                            coreSeen = true;
                            for (int field = 0; field < length; field++)
                            {
                                if (field == 7) continue;
                                Assert.That(naturalized.Program[naturalizedPc + field],
                                    Is.EqualTo(baseline.Program[baselinePc + field]),
                                    "Only the summit radius may change on the structural core.");
                            }

                            Assert.That(naturalized.Program[naturalizedPc + 7],
                                Is.LessThan(baseline.Program[baselinePc + 7]),
                                "The broad engineered summit disc must be reduced.");
                            Assert.That(naturalized.Program[naturalizedPc + 7],
                                Is.GreaterThanOrEqualTo(
                                    spec.PlaceholderSize / 2 + MinimumPlaceholderCrestMargin),
                                "The narrowed crest must retain stable support beneath the cube placeholder.");
                        }
                        else if (baselineMode == PrimitiveMode.FillIfEmpty
                                 && baselineMaterial == RockMaterial)
                        {
                            if (additiveFrustumIndex < AsymmetricShoulderCount)
                            {
                                for (int field = 0; field < length; field++)
                                {
                                    int expected = baseline.Program[baselinePc + field];
                                    if (field == 9) expected = GroundCoverMaterial;
                                    Assert.That(naturalized.Program[naturalizedPc + field], Is.EqualTo(expected),
                                        "Foothill shoulders may change material only.");
                                }

                                groundCoverFrustums++;
                            }
                            else
                            {
                                // Support masses may move/flare into overlapping ridges, but their
                                // elevation, height, axis, mode and rock role remain authoritative.
                                int[] unchangedFields = { 0, 1, 3, 5, 8, 9, 10, 11, 12 };
                                for (int i = 0; i < unchangedFields.Length; i++)
                                {
                                    int field = unchangedFields[i];
                                    Assert.That(naturalized.Program[naturalizedPc + field],
                                        Is.EqualTo(baseline.Program[baselinePc + field]),
                                        $"Support ridge field {field} must preserve traversal/support semantics.");
                                }

                                if (naturalized.Program[naturalizedPc + 2] != baseline.Program[baselinePc + 2]
                                    || naturalized.Program[naturalizedPc + 4] != baseline.Program[baselinePc + 4]
                                    || naturalized.Program[naturalizedPc + 6] != baseline.Program[baselinePc + 6]
                                    || naturalized.Program[naturalizedPc + 7] != baseline.Program[baselinePc + 7])
                                {
                                    changedSupportFrustums++;
                                }

                                int supportHeight = naturalized.Program[naturalizedPc + 5];
                                int supportX = naturalized.Program[naturalizedPc + 2];
                                int supportZ = naturalized.Program[naturalizedPc + 4];
                                int supportBaseRadius = naturalized.Program[naturalizedPc + 6];
                                int supportTopRadius = naturalized.Program[naturalizedPc + 7];
                                if (supportHeight == previousSupportHeight
                                    && supportX == previousSupportX
                                    && supportZ == previousSupportZ
                                    && supportBaseRadius == previousSupportBaseRadius
                                    && supportTopRadius == previousSupportTopRadius)
                                {
                                    pairedSupportDuplicates++;
                                }

                                previousSupportHeight = supportHeight;
                                previousSupportX = supportX;
                                previousSupportZ = supportZ;
                                previousSupportBaseRadius = supportBaseRadius;
                                previousSupportTopRadius = supportTopRadius;
                                rockSupportFrustums++;
                            }

                            additiveFrustumIndex++;
                        }
                    }
                    else
                    {
                        // Carves, ramps, walking surfaces and landings are traversal truth. The
                        // visual pass is not allowed to move or resize any of them.
                        for (int field = 0; field < length; field++)
                        {
                            Assert.That(naturalized.Program[naturalizedPc + field],
                                Is.EqualTo(baseline.Program[baselinePc + field]),
                                $"Traversal/path instruction {baselineOp} field {field} changed.");
                        }

                        if (baselineOp == ShapeOp.EmitRamp
                            && naturalized.Program[naturalizedPc + 9] == PathMaterial)
                        {
                            pathPrimitives++;
                        }
                        else if (baselineOp == ShapeOp.EmitBox
                                 && naturalized.Program[naturalizedPc + 8] == PathMaterial)
                        {
                            pathPrimitives++;
                        }
                    }

                    baselinePc += length;
                    naturalizedPc += length;
                }

                Assert.That(coreSeen, Is.True);
                Assert.That(groundCoverFrustums, Is.EqualTo(AsymmetricShoulderCount),
                    "Only the three broad asymmetric foothill masses should carry moss ground cover.");
                Assert.That(rockSupportFrustums, Is.GreaterThan(AsymmetricShoulderCount),
                    "Elevated path support must remain visibly structural rock rather than green cylinders.");
                Assert.That(changedSupportFrustums, Is.GreaterThanOrEqualTo(2),
                    "At least one authored support pair must be reshaped into a broader ridge.");
                Assert.That(pairedSupportDuplicates, Is.GreaterThanOrEqualTo(1),
                    "Adjacent same-elevation support masses must consolidate into overlapping ridge geometry.");
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
