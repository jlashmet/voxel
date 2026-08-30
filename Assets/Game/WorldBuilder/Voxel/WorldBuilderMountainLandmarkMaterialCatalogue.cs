using System;
using System.Collections.Generic;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Semantic material roles for a mountain landmark. Rock owns structural mass and path-support
    /// ridges, ground cover belongs to the broad foothill shoulders, and path/destination materials
    /// remain independent. Scenes choose the palette while WorldBuilder owns the realization.
    /// </summary>
    public readonly struct MountainLandmarkMaterialSet
    {
        public byte Rock { get; }
        public byte GroundCover { get; }
        public byte Path { get; }
        public byte Placeholder { get; }

        public MountainLandmarkMaterialSet(
            byte rock,
            byte groundCover,
            byte path,
            byte placeholder)
        {
            Rock = rock;
            GroundCover = groundCover;
            Path = path;
            Placeholder = placeholder;
        }
    }

    /// <summary>
    /// Naturalized presentation for the reusable mountain catalogue. The baseline catalogue remains
    /// the authoritative traversal/support program; this layer removes the visible row-of-identical
    /// support blobs without adding primitives or scene-specific coordinates.
    /// </summary>
    public static class WorldBuilderMountainLandmarkMaterialCatalogue
    {
        private const int AsymmetricShoulderCount = 3;
        private const int MinimumPlaceholderCrestMargin = 12;

        public static FeatureCatalogue Build(
            in MountainLandmarkSpec spec,
            in MountainLandmarkMaterialSet materials,
            Allocator allocator)
        {
            FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                materials.Rock,
                materials.Path,
                materials.Placeholder,
                allocator);

            NaturalizeLandform(catalogue, in spec, in materials);
            return catalogue;
        }

        private static void NaturalizeLandform(
            FeatureCatalogue catalogue,
            in MountainLandmarkSpec spec,
            in MountainLandmarkMaterialSet materials)
        {
            FeatureDefinition landform = catalogue.Definitions[0];
            int pc = landform.ProgramOffset;
            int end = pc + landform.ProgramLength;
            int additiveFrustumIndex = 0;
            bool coreSeen = false;
            var supportFrustums = new List<int>();

            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                if (op == ShapeOp.End) break;

                int instructionLength = ShapeOps.InstructionLength(op);
                if (instructionLength <= 0 || pc + instructionLength > end)
                    break;

                if (op == ShapeOp.EmitFrustum)
                {
                    PrimitiveMode mode = (PrimitiveMode)catalogue.Program[pc + 12];
                    int material = catalogue.Program[pc + 9];

                    if (!coreSeen && mode == PrimitiveMode.Fill && material == materials.Rock)
                    {
                        // Keep enough crest under the explicitly allowed cube, but remove the broad
                        // engineered-looking summit disc. Path-support ridges own the approach.
                        int minimumCrest = spec.PlaceholderSize / 2 + MinimumPlaceholderCrestMargin;
                        catalogue.Program[pc + 7] = Math.Max(
                            minimumCrest,
                            spec.SummitRadius * 3 / 4);
                        coreSeen = true;
                    }
                    else if (mode == PrimitiveMode.FillIfEmpty && material == materials.Rock)
                    {
                        if (additiveFrustumIndex < AsymmetricShoulderCount)
                        {
                            // Only the three broad foothill masses are ground-covered. Coloring
                            // every full-height path support green made the route read as a row of
                            // giant cylinders rather than a mountain.
                            catalogue.Program[pc + 9] = materials.GroundCover;
                        }
                        else
                        {
                            supportFrustums.Add(pc);
                        }

                        additiveFrustumIndex++;
                    }
                }

                pc += instructionLength;
            }

            PairSupportRidges(catalogue, supportFrustums, in spec, materials.Rock);
        }

        private static void PairSupportRidges(
            FeatureCatalogue catalogue,
            List<int> supportFrustums,
            in MountainLandmarkSpec spec,
            byte rockMaterial)
        {
            int runStart = 0;
            int pairOrdinal = 0;
            while (runStart < supportFrustums.Count)
            {
                int runHeight = catalogue.Program[supportFrustums[runStart] + 5];
                int runEnd = runStart + 1;
                while (runEnd < supportFrustums.Count
                       && catalogue.Program[supportFrustums[runEnd] + 5] == runHeight)
                {
                    runEnd++;
                }

                for (int i = runStart; i + 1 < runEnd; i += 2)
                {
                    int first = supportFrustums[i];
                    int second = supportFrustums[i + 1];
                    int x1 = catalogue.Program[first + 2];
                    int z1 = catalogue.Program[first + 4];
                    int x2 = catalogue.Program[second + 2];
                    int z2 = catalogue.Program[second + 4];
                    int halfSpan = Math.Max(Math.Abs(x2 - x1), Math.Abs(z2 - z1)) / 2;

                    // A shared broad top covers both authored support centres and the complete path
                    // width between them. Duplicating the paired primitive preserves program shape
                    // and budget accounting while FillIfEmpty makes the second copy an inexpensive
                    // semantic no-op after the first has authored the ridge.
                    int topRadius = Math.Max(
                        Math.Max(catalogue.Program[first + 7], catalogue.Program[second + 7]),
                        halfSpan + spec.PathWidth);
                    int baseRadius = Math.Max(
                        Math.Max(catalogue.Program[first + 6], catalogue.Program[second + 6]),
                        topRadius + spec.PathWidth);
                    int centreX = (x1 + x2) / 2;
                    int centreZ = (z1 + z2) / 2;

                    // Small deterministic offsets break ruler-straight repetition while the larger
                    // top radius retains support beneath both original path spans.
                    int jitter = (pairOrdinal % 3 - 1) * 4;
                    if ((pairOrdinal & 1) == 0) centreZ += jitter;
                    else centreX += jitter;
                    pairOrdinal++;

                    ApplyRidge(catalogue, first, centreX, centreZ, baseRadius, topRadius, rockMaterial);
                    ApplyRidge(catalogue, second, centreX, centreZ, baseRadius, topRadius, rockMaterial);
                }

                runStart = runEnd;
            }
        }

        private static void ApplyRidge(
            FeatureCatalogue catalogue,
            int pc,
            int centreX,
            int centreZ,
            int baseRadius,
            int topRadius,
            byte rockMaterial)
        {
            catalogue.Program[pc + 2] = centreX;
            catalogue.Program[pc + 4] = centreZ;
            catalogue.Program[pc + 6] = baseRadius;
            catalogue.Program[pc + 7] = topRadius;
            catalogue.Program[pc + 9] = rockMaterial;
        }
    }
}
