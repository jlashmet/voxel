using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Semantic material roles for a mountain landmark. Scenes choose the palette while WorldBuilder
    /// owns the geometry that receives each role.
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
            if (rock == 0) throw new ArgumentOutOfRangeException(nameof(rock));
            if (groundCover == 0) throw new ArgumentOutOfRangeException(nameof(groundCover));
            if (path == 0) throw new ArgumentOutOfRangeException(nameof(path));
            if (placeholder == 0) throw new ArgumentOutOfRangeException(nameof(placeholder));

            Rock = rock;
            GroundCover = groundCover;
            Path = path;
            Placeholder = placeholder;
        }
    }

    public enum MountainLandmarkSupportForm : byte
    {
        SegmentedMasses = 0,
        RidgeAndButtress = 1,
    }

    /// <summary>
    /// Semantic presentation choices for a reusable mountain landmark. No scene coordinates or
    /// material ids live here; callers select the crest proportion and support form explicitly.
    /// </summary>
    public readonly struct MountainLandmarkPresentationProfile
    {
        public int CrestRadiusPercent { get; }
        public int MinimumPlaceholderCrestMargin { get; }
        public MountainLandmarkSupportForm SupportForm { get; }

        public MountainLandmarkPresentationProfile(
            int crestRadiusPercent,
            int minimumPlaceholderCrestMargin,
            MountainLandmarkSupportForm supportForm)
        {
            if (crestRadiusPercent <= 0 || crestRadiusPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(crestRadiusPercent));
            if (minimumPlaceholderCrestMargin < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumPlaceholderCrestMargin));
            if (!Enum.IsDefined(typeof(MountainLandmarkSupportForm), supportForm))
                throw new ArgumentOutOfRangeException(nameof(supportForm));

            CrestRadiusPercent = crestRadiusPercent;
            MinimumPlaceholderCrestMargin = minimumPlaceholderCrestMargin;
            SupportForm = supportForm;
        }
    }

    /// <summary>
    /// Semantic material/presentation authoring for the reusable mountain landmark. Geometry is
    /// emitted directly from MountainLandmarkSpec and its tapered PathTier contract; this builder
    /// never rewrites a compiled instruction by definition index or instruction ordinal.
    /// </summary>
    public static class WorldBuilderMountainLandmarkMaterialCatalogue
    {
        private const int SupportSegmentSpan = 64;
        private const int MinimumSupportTopRadius = 40;
        private const int MaximumSupportFlare = 112;

        private readonly struct SupportFrustumDraft
        {
            public int CentreX { get; }
            public int CentreZ { get; }
            public int Height { get; }
            public int BaseRadius { get; }
            public int TopRadius { get; }

            public SupportFrustumDraft(
                int centreX,
                int centreZ,
                int height,
                int baseRadius,
                int topRadius)
            {
                CentreX = centreX;
                CentreZ = centreZ;
                Height = height;
                BaseRadius = baseRadius;
                TopRadius = topRadius;
            }
        }

        public static FeatureCatalogue Build(
            in MountainLandmarkSpec spec,
            in MountainLandmarkMaterialSet materials,
            in MountainLandmarkPresentationProfile presentation,
            Allocator allocator)
        {
            int[] landformProgram = BuildLandformProgram(in spec, in materials, in presentation);
            int[] placeholderProgram = BuildPlaceholderProgram(spec.PlaceholderSize, materials.Placeholder);
            int programLength = landformProgram.Length + placeholderProgram.Length;

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 2,
                rules: 2,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: 2,
                overrides: 0,
                allocator);

            int p = 0;
            for (int i = 0; i < landformProgram.Length; i++) catalogue.Program[p++] = landformProgram[i];
            for (int i = 0; i < placeholderProgram.Length; i++) catalogue.Program[p++] = placeholderProgram[i];

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = WorldBuilderMountainLandmarkCatalogue.LandformDefinitionName,
                Kind = FeatureKind.Landform,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = spec.Origin.y,
                Footprint = new int3(
                    spec.FootprintEdge,
                    spec.MountainHeight + WorldBuilderMountainLandmarkCatalogue.PathHeadroomVoxels + 2,
                    spec.FootprintEdge),
                MaxSlope = 8,
                Precedence = 100,
                ProgramOffset = 0,
                ProgramLength = landformProgram.Length,
                MaxPrimitives = CountEmitInstructions(landformProgram),
            };

            catalogue.Definitions[1] = new FeatureDefinition
            {
                Name = WorldBuilderMountainLandmarkCatalogue.PlaceholderDefinitionName,
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = spec.Origin.y + spec.MountainHeight + 1,
                Footprint = new int3(spec.PlaceholderSize, spec.PlaceholderSize, spec.PlaceholderSize),
                MaxSlope = 0,
                Precedence = 120,
                ProgramOffset = landformProgram.Length,
                ProgramLength = placeholderProgram.Length,
                MaxPrimitives = 1,
            };

            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = spec.Origin,
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            int cubeHalf = spec.PlaceholderSize / 2;
            catalogue.ExplicitPlacements[1] = new ExplicitPlacement
            {
                Position = new int3(
                    spec.Origin.x + spec.CentreLocal - cubeHalf,
                    spec.Origin.y + spec.MountainHeight + 1,
                    spec.Origin.z + spec.CentreLocal - cubeHalf),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            catalogue.Rules[0] = ExplicitRule(0, 0);
            catalogue.Rules[1] = ExplicitRule(1, 1);

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result == CatalogueLoadResult.Ok) return catalogue;

            catalogue.Dispose();
            throw new InvalidOperationException(
                "Semantic mountain landmark catalogue failed validation: " + result);
        }

        [Obsolete("Select an explicit MountainLandmarkPresentationProfile at the composition boundary.")]
        public static FeatureCatalogue Build(
            in MountainLandmarkSpec spec,
            in MountainLandmarkMaterialSet materials,
            Allocator allocator)
        {
            var compatibility = new MountainLandmarkPresentationProfile(
                crestRadiusPercent: 75,
                minimumPlaceholderCrestMargin: 12,
                supportForm: MountainLandmarkSupportForm.RidgeAndButtress);
            return Build(in spec, in materials, in compatibility, allocator);
        }

        private static PlacementRule ExplicitRule(int definitionId, int placementIndex) =>
            new PlacementRule
            {
                DefinitionId = definitionId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 4096,
                MaxSlope = 8,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = placementIndex,
                ExplicitCount = 1,
            };

        private static int[] BuildLandformProgram(
            in MountainLandmarkSpec spec,
            in MountainLandmarkMaterialSet materials,
            in MountainLandmarkPresentationProfile presentation)
        {
            var program = new List<int>(1200);
            int c = spec.CentreLocal;
            int minimumCrest = spec.PlaceholderSize / 2 + presentation.MinimumPlaceholderCrestMargin;
            int proportionalCrest = spec.SummitRadius * presentation.CrestRadiusPercent / 100;
            int crestRadius = math.clamp(
                Math.Max(minimumCrest, proportionalCrest),
                1,
                spec.MountainRadius - 1);

            EmitFrustum(
                program,
                c, 0, c,
                spec.MountainHeight + 1,
                spec.MountainRadius,
                crestRadius,
                1,
                materials.Rock,
                PrimitiveMode.Fill);

            AddAsymmetricMountainShoulders(program, in spec, materials.GroundCover);
            AddPathSupports(program, in spec, materials.Rock, presentation.SupportForm);
            CarvePathHeadroom(program, in spec);
            EmitPathSurface(program, in spec, materials.Path);

            End(program);
            return program.ToArray();
        }

        private static void AddPathSupports(
            List<int> program,
            in MountainLandmarkSpec spec,
            byte rockMaterial,
            MountainLandmarkSupportForm supportForm)
        {
            var drafts = new List<SupportFrustumDraft>(64);
            MountainPathTierGeometry lastTier = default;
            int endY = 0;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                endY = tier.EndY;
                lastTier = tier;

                AddSupportMassDrafts(
                    drafts,
                    tier.MinX, tier.LocalZ,
                    tier.Run, spec.PathWidth,
                    tier.StartY,
                    spec.PathWidth,
                    spec.PathWidth / 2);

                if (level + 1 >= spec.SwitchbackCount) continue;

                MountainPathTierGeometry next = spec.PathTier(level + 1);
                int zMin = Math.Min(tier.LocalZ, next.LocalZ);
                int zSize = Math.Abs(next.LocalZ - tier.LocalZ) + spec.PathWidth;
                AddSupportMassDrafts(
                    drafts,
                    tier.HighLandingMinX, zMin,
                    spec.PathWidth, zSize,
                    endY,
                    spec.PathWidth,
                    0);
            }

            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalZMin = Math.Min(lastTier.LocalZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastTier.LocalZ) + spec.PathWidth;
            AddSupportMassDrafts(
                drafts,
                lastTier.HighLandingMinX, finalZMin,
                spec.PathWidth, finalZSize,
                endY,
                spec.PathWidth,
                0);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastTier.HighLandingMinX, approachX);
            int topSizeX = Math.Abs(approachX - lastTier.HighLandingMinX) + spec.PathWidth;
            int topZ = spec.SummitApproachLocalZ - spec.PathWidth / 2;
            AddSupportMassDrafts(
                drafts,
                topMinX, topZ,
                topSizeX, spec.PathWidth,
                spec.MountainHeight,
                spec.PathWidth,
                spec.PathWidth / 2);

            if (supportForm == MountainLandmarkSupportForm.RidgeAndButtress)
                EmitRidgeAndButtressSupports(program, drafts, in spec, rockMaterial);
            else
                EmitSegmentedSupports(program, drafts, rockMaterial);
        }

        private static void AddSupportMassDrafts(
            List<SupportFrustumDraft> drafts,
            int minX,
            int minZ,
            int sizeX,
            int sizeZ,
            int supportTopY,
            int pathWidth,
            int inwardShortAxisBias)
        {
            if (supportTopY <= 0) return;

            bool alongX = sizeX >= sizeZ;
            int longMin = alongX ? minX : minZ;
            int longSize = alongX ? sizeX : sizeZ;
            int shortMin = alongX ? minZ : minX;
            int shortSize = alongX ? sizeZ : sizeX;
            int shortCentre = shortMin + shortSize / 2;
            if (alongX) shortCentre += inwardShortAxisBias;

            int segmentCount = math.max(1, (longSize + SupportSegmentSpan - 1) / SupportSegmentSpan);
            int topRadius = math.max(MinimumSupportTopRadius, pathWidth + 18);
            int flare = math.min(MaximumSupportFlare, math.max(16, supportTopY * 2 / 5));
            int baseRadius = topRadius + flare;

            for (int segment = 0; segment < segmentCount; segment++)
            {
                int segmentStart = segment * longSize / segmentCount;
                int segmentEndExclusive = (segment + 1) * longSize / segmentCount;
                int longCentre = longMin + (segmentStart + segmentEndExclusive - 1) / 2;
                int lateralJitter = ((segment * 37 + supportTopY * 11) % 9) - 4;

                drafts.Add(new SupportFrustumDraft(
                    alongX ? longCentre : shortCentre + lateralJitter,
                    alongX ? shortCentre + lateralJitter : longCentre,
                    supportTopY + 1,
                    baseRadius,
                    topRadius));
            }
        }

        private static void EmitSegmentedSupports(
            List<int> program,
            List<SupportFrustumDraft> drafts,
            byte rockMaterial)
        {
            for (int i = 0; i < drafts.Count; i++)
                EmitSupport(program, drafts[i], rockMaterial);
        }

        private static void EmitRidgeAndButtressSupports(
            List<int> program,
            List<SupportFrustumDraft> drafts,
            in MountainLandmarkSpec spec,
            byte rockMaterial)
        {
            int runStart = 0;
            int pairOrdinal = 0;
            while (runStart < drafts.Count)
            {
                int runHeight = drafts[runStart].Height;
                int runEnd = runStart + 1;
                while (runEnd < drafts.Count && drafts[runEnd].Height == runHeight)
                    runEnd++;

                int i = runStart;
                for (; i + 1 < runEnd; i += 2)
                {
                    SupportFrustumDraft first = drafts[i];
                    SupportFrustumDraft second = drafts[i + 1];
                    int centreX = (first.CentreX + second.CentreX) / 2;
                    int centreZ = (first.CentreZ + second.CentreZ) / 2;
                    int coverRadius = Math.Max(
                        Math.Max(Math.Abs(centreX - first.CentreX), Math.Abs(centreZ - first.CentreZ)),
                        Math.Max(Math.Abs(centreX - second.CentreX), Math.Abs(centreZ - second.CentreZ)))
                        + spec.PathWidth;
                    int ridgeTopRadius = Math.Max(
                        Math.Max(first.TopRadius, second.TopRadius),
                        coverRadius);
                    int ridgeBaseRadius = Math.Max(
                        Math.Max(first.BaseRadius, second.BaseRadius),
                        ridgeTopRadius + spec.PathWidth / 2);

                    EmitFrustum(
                        program,
                        centreX, 0, centreZ,
                        runHeight,
                        ridgeBaseRadius,
                        ridgeTopRadius,
                        1,
                        rockMaterial,
                        PrimitiveMode.FillIfEmpty);

                    int buttressHeight = Math.Max(spec.PathRise / 2, runHeight / 2);
                    buttressHeight = Math.Max(1, Math.Min(runHeight - 1, buttressHeight));
                    int buttressTopRadius = Math.Max(
                        spec.PathWidth,
                        Math.Min(first.TopRadius, second.TopRadius) * 3 / 4);
                    int buttressBaseRadius = Math.Min(
                        Math.Max(first.BaseRadius, second.BaseRadius),
                        buttressTopRadius + spec.PathWidth / 2);
                    bool anchorFirst = (pairOrdinal & 1) == 0;
                    pairOrdinal++;
                    SupportFrustumDraft anchor = anchorFirst ? first : second;

                    EmitFrustum(
                        program,
                        anchor.CentreX, 0, anchor.CentreZ,
                        buttressHeight,
                        buttressBaseRadius,
                        buttressTopRadius,
                        1,
                        rockMaterial,
                        PrimitiveMode.FillIfEmpty);
                }

                if (i < runEnd)
                    EmitSupport(program, drafts[i], rockMaterial);

                runStart = runEnd;
            }
        }

        private static void EmitSupport(
            List<int> program,
            SupportFrustumDraft draft,
            byte rockMaterial) =>
            EmitFrustum(
                program,
                draft.CentreX, 0, draft.CentreZ,
                draft.Height,
                draft.BaseRadius,
                draft.TopRadius,
                1,
                rockMaterial,
                PrimitiveMode.FillIfEmpty);

        private static void CarvePathHeadroom(
            List<int> program,
            in MountainLandmarkSpec spec)
        {
            int clearanceWidth = math.min(
                spec.PathWidth,
                WorldBuilderMountainLandmarkCatalogue.PathClearanceWidthVoxels);
            int clearanceInset = (spec.PathWidth - clearanceWidth) / 2;
            MountainPathTierGeometry lastTier = default;
            int endY = 0;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                endY = tier.EndY;
                lastTier = tier;

                EmitBox(
                    program,
                    tier.MinX, tier.StartY + 1, tier.LocalZ + clearanceInset,
                    tier.Run,
                    spec.PathRise + WorldBuilderMountainLandmarkCatalogue.PathHeadroomVoxels,
                    clearanceWidth,
                    0,
                    PrimitiveMode.Carve);

                if (level + 1 >= spec.SwitchbackCount) continue;

                MountainPathTierGeometry next = spec.PathTier(level + 1);
                int zMin = Math.Min(tier.LocalZ, next.LocalZ);
                int zSize = Math.Abs(next.LocalZ - tier.LocalZ) + spec.PathWidth;
                EmitBox(
                    program,
                    tier.HighLandingMinX + clearanceInset, endY + 1, zMin,
                    clearanceWidth,
                    WorldBuilderMountainLandmarkCatalogue.PathHeadroomVoxels,
                    zSize,
                    0,
                    PrimitiveMode.Carve);
            }

            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalRise = spec.MountainHeight - endY;
            int finalZMin = Math.Min(lastTier.LocalZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastTier.LocalZ) + spec.PathWidth;
            EmitBox(
                program,
                lastTier.HighLandingMinX + clearanceInset, endY + 1, finalZMin,
                clearanceWidth,
                finalRise + WorldBuilderMountainLandmarkCatalogue.PathHeadroomVoxels,
                finalZSize,
                0,
                PrimitiveMode.Carve);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastTier.HighLandingMinX, approachX);
            int topSizeX = Math.Abs(approachX - lastTier.HighLandingMinX) + spec.PathWidth;
            int topZ = spec.SummitApproachLocalZ - spec.PathWidth / 2;
            EmitBox(
                program,
                topMinX, spec.MountainHeight + 1, topZ + clearanceInset,
                topSizeX,
                WorldBuilderMountainLandmarkCatalogue.PathHeadroomVoxels,
                clearanceWidth,
                0,
                PrimitiveMode.Carve);
        }

        private static void EmitPathSurface(
            List<int> program,
            in MountainLandmarkSpec spec,
            byte pathMaterial)
        {
            MountainPathTierGeometry lastTier = default;
            int endY = 0;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                endY = tier.EndY;
                lastTier = tier;

                int interiorRun = math.max(1, tier.Run - spec.PathWidth * 2);
                int rampHeight = spec.PathRise + 1;
                int overlapNumerator = math.max(0, interiorRun - rampHeight);
                int lowLandingOverlap = overlapNumerator == 0
                    ? 0
                    : (overlapNumerator + rampHeight - 2) / (rampHeight - 1);
                lowLandingOverlap = math.min(spec.PathWidth, lowLandingOverlap);
                int rampRun = interiorRun + lowLandingOverlap;
                int axis = tier.Reverse ? ShapeOps.ReverseRampBit : 0;
                int rampX = tier.Reverse
                    ? tier.MinX + spec.PathWidth
                    : tier.MinX + spec.PathWidth - lowLandingOverlap;

                if (level == 0)
                {
                    EmitBox(
                        program,
                        tier.LowLandingMinX, tier.StartY, tier.LocalZ,
                        spec.PathWidth, 1, spec.PathWidth,
                        pathMaterial,
                        PrimitiveMode.Fill);
                }

                EmitRamp(
                    program,
                    rampX, tier.StartY, tier.LocalZ,
                    rampRun, rampHeight, spec.PathWidth,
                    axis,
                    pathMaterial,
                    PrimitiveMode.Fill);

                if (level + 1 >= spec.SwitchbackCount) continue;

                MountainPathTierGeometry next = spec.PathTier(level + 1);
                int zMin = Math.Min(tier.LocalZ, next.LocalZ);
                int zSize = Math.Abs(next.LocalZ - tier.LocalZ) + spec.PathWidth;
                EmitBox(
                    program,
                    tier.HighLandingMinX, endY, zMin,
                    spec.PathWidth, 1, zSize,
                    pathMaterial,
                    PrimitiveMode.Fill);
            }

            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalRise = spec.MountainHeight - endY;
            int finalZMin = Math.Min(lastTier.LocalZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastTier.LocalZ) + spec.PathWidth;

            EmitBox(
                program,
                lastTier.HighLandingMinX, endY, lastTier.LocalZ,
                spec.PathWidth, 1, spec.PathWidth,
                pathMaterial,
                PrimitiveMode.Fill);
            EmitRamp(
                program,
                lastTier.HighLandingMinX, endY, finalZMin,
                spec.PathWidth, finalRise + 1, finalZSize,
                2,
                pathMaterial,
                PrimitiveMode.Fill);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastTier.HighLandingMinX, approachX);
            int topSizeX = Math.Abs(approachX - lastTier.HighLandingMinX) + spec.PathWidth;
            int topZ = spec.SummitApproachLocalZ - spec.PathWidth / 2;
            EmitBox(
                program,
                topMinX, spec.MountainHeight, topZ,
                topSizeX, 1, spec.PathWidth,
                pathMaterial,
                PrimitiveMode.Fill);
        }

        private static void AddAsymmetricMountainShoulders(
            List<int> program,
            in MountainLandmarkSpec spec,
            byte groundCoverMaterial)
        {
            int c = spec.CentreLocal;
            int r = spec.MountainRadius;

            EmitFrustum(
                program,
                c - r * 36 / 100, 0, c + r * 28 / 100,
                math.max(2, spec.MountainHeight * 68 / 100),
                math.max(2, r * 60 / 100),
                math.max(1, r * 18 / 100),
                1,
                groundCoverMaterial,
                PrimitiveMode.FillIfEmpty);

            EmitFrustum(
                program,
                c + r * 42 / 100, 0, c - r * 34 / 100,
                math.max(2, spec.MountainHeight * 55 / 100),
                math.max(2, r * 56 / 100),
                math.max(1, r * 14 / 100),
                1,
                groundCoverMaterial,
                PrimitiveMode.FillIfEmpty);

            EmitFrustum(
                program,
                c - r * 30 / 100, 0, c - r * 46 / 100,
                math.max(2, spec.MountainHeight * 43 / 100),
                math.max(2, r * 48 / 100),
                math.max(1, r * 12 / 100),
                1,
                groundCoverMaterial,
                PrimitiveMode.FillIfEmpty);
        }

        private static int[] BuildPlaceholderProgram(int size, byte material)
        {
            var program = new List<int>(16);
            EmitBox(program, 0, 0, 0, size, size, size, material, PrimitiveMode.Fill);
            End(program);
            return program.ToArray();
        }

        private static int CountEmitInstructions(int[] program)
        {
            int count = 0;
            for (int pc = 0; pc < program.Length;)
            {
                ShapeOp op = (ShapeOp)program[pc];
                if (ShapeOps.IsEmit(op)) count++;
                int length = ShapeOps.InstructionLength(op);
                if (length <= 0) break;
                pc += length;
                if (op == ShapeOp.End) break;
            }
            return count;
        }

        private static void EmitBox(
            List<int> p,
            int x, int y, int z,
            int sizeX, int sizeY, int sizeZ,
            byte material,
            PrimitiveMode mode)
        {
            p.Add((int)ShapeOp.EmitBox); p.Add(0);
            p.Add(x); p.Add(y); p.Add(z);
            p.Add(sizeX); p.Add(sizeY); p.Add(sizeZ);
            p.Add(material); p.Add(0); p.Add(0); p.Add((int)mode);
        }

        private static void EmitRamp(
            List<int> p,
            int x, int y, int z,
            int sizeX, int sizeY, int sizeZ,
            int axis,
            byte material,
            PrimitiveMode mode)
        {
            p.Add((int)ShapeOp.EmitRamp); p.Add(0);
            p.Add(x); p.Add(y); p.Add(z);
            p.Add(sizeX); p.Add(sizeY); p.Add(sizeZ);
            p.Add(axis); p.Add(material); p.Add(0); p.Add(0); p.Add((int)mode);
        }

        private static void EmitFrustum(
            List<int> p,
            int x, int y, int z,
            int height,
            int baseRadius,
            int topRadius,
            int axis,
            byte material,
            PrimitiveMode mode)
        {
            p.Add((int)ShapeOp.EmitFrustum); p.Add(0);
            p.Add(x); p.Add(y); p.Add(z);
            p.Add(height); p.Add(baseRadius); p.Add(topRadius); p.Add(axis);
            p.Add(material); p.Add(0); p.Add(0); p.Add((int)mode);
        }

        private static void End(List<int> p)
        {
            p.Add((int)ShapeOp.End);
            p.Add(0);
        }
    }
}
