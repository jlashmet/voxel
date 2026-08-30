using System;
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
                int transformedPairs = 0;
                int loweredButtresses = 0;
                int pathPrimitives = 0;
                int pairOrdinal = 0;
                long baselineSupportRasterProxy = 0;
                long naturalizedSupportRasterProxy = 0;
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
                                baselineSupportRasterProxy += ConservativeFrustumRasterProxy(
                                    baseline.Program[baselinePc + 5],
                                    baseline.Program[baselinePc + 6]);
                                naturalizedSupportRasterProxy += ConservativeFrustumRasterProxy(
                                    naturalized.Program[naturalizedPc + 5],
                                    naturalized.Program[naturalizedPc + 6]);

                                Assert.That(naturalized.Program[naturalizedPc + 3],
                                    Is.EqualTo(baseline.Program[baselinePc + 3]),
                                    "Support base altitude must stay authoritative.");
                                Assert.That(naturalized.Program[naturalizedPc + 8],
                                    Is.EqualTo(baseline.Program[baselinePc + 8]),
                                    "Support frustum axis must stay authoritative.");
                                Assert.That(naturalized.Program[naturalizedPc + 9], Is.EqualTo(RockMaterial));
                                Assert.That(naturalized.Program[naturalizedPc + 10],
                                    Is.EqualTo(baseline.Program[baselinePc + 10]));
                                Assert.That(naturalized.Program[naturalizedPc + 11],
                                    Is.EqualTo(baseline.Program[baselinePc + 11]));
                                Assert.That(naturalized.Program[naturalizedPc + 12],
                                    Is.EqualTo(baseline.Program[baselinePc + 12]),
                                    "Support mode must remain FillIfEmpty.");

                                // Pairing is performed over each consecutive same-height support run.
                                // The first member becomes the full-height support-covering ridge and
                                // the second becomes a lower/narrow buttress. An odd run tail is left
                                // unchanged. Derive the expectation from the generic program itself.
                                int nextBaselinePc = baselinePc + length;
                                bool hasSameHeightPartner = nextBaselinePc < end
                                    && (ShapeOp)baseline.Program[nextBaselinePc] == ShapeOp.EmitFrustum
                                    && (PrimitiveMode)baseline.Program[nextBaselinePc + 12] == PrimitiveMode.FillIfEmpty
                                    && baseline.Program[nextBaselinePc + 9] == RockMaterial
                                    && baseline.Program[nextBaselinePc + 5] == baseline.Program[baselinePc + 5];

                                if (hasSameHeightPartner)
                                {
                                    int nextNaturalizedPc = naturalizedPc + length;
                                    AssertRidgeButtressPair(
                                        baseline,
                                        naturalized,
                                        baselinePc,
                                        nextBaselinePc,
                                        naturalizedPc,
                                        nextNaturalizedPc,
                                        in spec,
                                        pairOrdinal);

                                    baselineSupportRasterProxy += ConservativeFrustumRasterProxy(
                                        baseline.Program[nextBaselinePc + 5],
                                        baseline.Program[nextBaselinePc + 6]);
                                    naturalizedSupportRasterProxy += ConservativeFrustumRasterProxy(
                                        naturalized.Program[nextNaturalizedPc + 5],
                                        naturalized.Program[nextNaturalizedPc + 6]);

                                    transformedPairs++;
                                    loweredButtresses++;
                                    rockSupportFrustums += 2;
                                    pairOrdinal++;
                                    additiveFrustumIndex += 2;
                                    baselinePc += length * 2;
                                    naturalizedPc += length * 2;
                                    continue;
                                }

                                for (int field = 0; field < length; field++)
                                {
                                    Assert.That(naturalized.Program[naturalizedPc + field],
                                        Is.EqualTo(baseline.Program[baselinePc + field]),
                                        "An unpaired support-run tail must remain unchanged.");
                                }

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
                Assert.That(transformedPairs, Is.GreaterThanOrEqualTo(1),
                    "At least one same-elevation support pair must become a ridge plus buttress.");
                Assert.That(loweredButtresses, Is.EqualTo(transformedPairs),
                    "Every transformed support pair must replace its duplicate full-height member with a buttress.");
                Assert.That(naturalizedSupportRasterProxy * 4, Is.LessThan(baselineSupportRasterProxy * 3),
                    "Naturalized support raster-volume proxy must stay below 75% of the generic baseline; "
                    + "revision 5 exceeded the 240-second bake watchdog despite unchanged primitive count.");
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

        private static void AssertRidgeButtressPair(
            FeatureCatalogue baseline,
            FeatureCatalogue naturalized,
            int firstBaselinePc,
            int secondBaselinePc,
            int ridgePc,
            int buttressPc,
            in MountainLandmarkSpec spec,
            int pairOrdinal)
        {
            int x1 = baseline.Program[firstBaselinePc + 2];
            int z1 = baseline.Program[firstBaselinePc + 4];
            int x2 = baseline.Program[secondBaselinePc + 2];
            int z2 = baseline.Program[secondBaselinePc + 4];
            int centreX = (x1 + x2) / 2;
            int centreZ = (z1 + z2) / 2;
            int coverRadius = Math.Max(
                Math.Max(Math.Abs(centreX - x1), Math.Abs(centreZ - z1)),
                Math.Max(Math.Abs(centreX - x2), Math.Abs(centreZ - z2)))
                + spec.PathWidth;
            int expectedRidgeTop = Math.Max(
                Math.Max(baseline.Program[firstBaselinePc + 7], baseline.Program[secondBaselinePc + 7]),
                coverRadius);
            int expectedRidgeBase = Math.Max(
                Math.Max(baseline.Program[firstBaselinePc + 6], baseline.Program[secondBaselinePc + 6]),
                expectedRidgeTop + spec.PathWidth / 2);

            Assert.That(naturalized.Program[ridgePc + 2], Is.EqualTo(centreX));
            Assert.That(naturalized.Program[ridgePc + 4], Is.EqualTo(centreZ));
            Assert.That(naturalized.Program[ridgePc + 5], Is.EqualTo(baseline.Program[firstBaselinePc + 5]),
                "The primary ridge must retain full authored support height.");
            Assert.That(naturalized.Program[ridgePc + 6], Is.EqualTo(expectedRidgeBase));
            Assert.That(naturalized.Program[ridgePc + 7], Is.EqualTo(expectedRidgeTop));
            Assert.That(expectedRidgeTop, Is.GreaterThanOrEqualTo(coverRadius),
                "The primary ridge top must cover both original path-support centres with path-width margin.");

            int runHeight = baseline.Program[firstBaselinePc + 5];
            int expectedButtressHeight = Math.Max(spec.PathRise / 2, runHeight / 2);
            expectedButtressHeight = Math.Max(1, Math.Min(runHeight - 1, expectedButtressHeight));
            int expectedButtressTop = Math.Max(
                spec.PathWidth,
                Math.Min(baseline.Program[firstBaselinePc + 7], baseline.Program[secondBaselinePc + 7]) * 3 / 4);
            int expectedButtressBase = Math.Min(
                Math.Max(baseline.Program[firstBaselinePc + 6], baseline.Program[secondBaselinePc + 6]),
                expectedButtressTop + spec.PathWidth / 2);
            bool anchorFirst = (pairOrdinal & 1) == 0;

            Assert.That(naturalized.Program[buttressPc + 2],
                Is.EqualTo(anchorFirst ? x1 : x2));
            Assert.That(naturalized.Program[buttressPc + 4],
                Is.EqualTo(anchorFirst ? z1 : z2));
            Assert.That(naturalized.Program[buttressPc + 5], Is.EqualTo(expectedButtressHeight));
            Assert.That(naturalized.Program[buttressPc + 5], Is.LessThan(runHeight),
                "The companion primitive must be a lower buttress, not another full-height ridge.");
            Assert.That(naturalized.Program[buttressPc + 6], Is.EqualTo(expectedButtressBase));
            Assert.That(naturalized.Program[buttressPc + 7], Is.EqualTo(expectedButtressTop));
            Assert.That(naturalized.Program[buttressPc + 7], Is.LessThan(expectedRidgeTop),
                "The buttress must be visibly narrower than its support-covering ridge.");
        }

        private static long ConservativeFrustumRasterProxy(int height, int baseRadius)
        {
            long diameter = baseRadius * 2L + 1L;
            return height * diameter * diameter;
        }
    }
}
