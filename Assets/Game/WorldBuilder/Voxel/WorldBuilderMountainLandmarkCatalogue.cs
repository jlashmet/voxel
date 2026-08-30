using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// One authored switchback tier derived from the landmark's tapered core.
    /// Coordinates are local to the landmark footprint.
    /// </summary>
    public readonly struct MountainPathTierGeometry
    {
        public int Level { get; }
        public int StartY { get; }
        public int EndY { get; }
        public int MinX { get; }
        public int Run { get; }
        public int LocalZ { get; }
        public int PathWidth { get; }
        public bool Reverse { get; }

        public MountainPathTierGeometry(
            int level,
            int startY,
            int endY,
            int minX,
            int run,
            int localZ,
            int pathWidth,
            bool reverse)
        {
            Level = level;
            StartY = startY;
            EndY = endY;
            MinX = minX;
            Run = run;
            LocalZ = localZ;
            PathWidth = pathWidth;
            Reverse = reverse;
        }

        public int LowLandingMinX => Reverse ? MinX + Run - PathWidth : MinX;
        public int HighLandingMinX => Reverse ? MinX : MinX + Run - PathWidth;
        public int LowCentreX => LowLandingMinX + PathWidth / 2;
        public int HighCentreX => HighLandingMinX + PathWidth / 2;
        public int CentreZ => LocalZ + PathWidth / 2;
    }

    /// <summary>
    /// Integer-only authored intent for a substantial mountain landmark. The scene chooses scale,
    /// placement and materials; this adapter owns the reusable voxel realization, including the
    /// switchback ascent, summit and placeholder footprint.
    /// </summary>
    public readonly struct MountainLandmarkSpec
    {
        public int3 Origin { get; }
        public int FootprintEdge { get; }
        public int MountainRadius { get; }
        public int MountainHeight { get; }
        public int SummitRadius { get; }
        public int PathWidth { get; }
        public int PathRun { get; }
        public int PathRise { get; }
        public int SwitchbackCount { get; }
        public int PlaceholderSize { get; }

        public MountainLandmarkSpec(
            int3 origin,
            int footprintEdge,
            int mountainRadius,
            int mountainHeight,
            int summitRadius,
            int pathWidth,
            int pathRun,
            int pathRise,
            int switchbackCount,
            int placeholderSize)
        {
            if (footprintEdge <= 0 || footprintEdge > FeatureBudget.MaxFootprintVoxels)
                throw new ArgumentOutOfRangeException(nameof(footprintEdge));
            if (mountainRadius <= 0 || mountainRadius * 2 >= footprintEdge)
                throw new ArgumentOutOfRangeException(nameof(mountainRadius));
            if (mountainHeight <= 0 || mountainHeight >= footprintEdge)
                throw new ArgumentOutOfRangeException(nameof(mountainHeight));
            if (summitRadius <= 0 || summitRadius >= mountainRadius)
                throw new ArgumentOutOfRangeException(nameof(summitRadius));
            if (pathWidth < 8 || pathWidth >= summitRadius)
                throw new ArgumentOutOfRangeException(nameof(pathWidth));
            if (pathRun <= pathWidth || pathRun >= mountainRadius * 2)
                throw new ArgumentOutOfRangeException(nameof(pathRun));
            if (pathRise <= 0 || switchbackCount < 2 || pathRise * switchbackCount > mountainHeight)
                throw new ArgumentOutOfRangeException(nameof(pathRise));
            if (placeholderSize <= 0 || placeholderSize >= summitRadius * 2)
                throw new ArgumentOutOfRangeException(nameof(placeholderSize));

            Origin = origin;
            FootprintEdge = footprintEdge;
            MountainRadius = mountainRadius;
            MountainHeight = mountainHeight;
            SummitRadius = summitRadius;
            PathWidth = pathWidth;
            PathRun = pathRun;
            PathRise = pathRise;
            SwitchbackCount = switchbackCount;
            PlaceholderSize = placeholderSize;
        }

        public int CentreLocal => FootprintEdge / 2;

        // Legacy aliases now represent the first tier. Consumers that need any later tier must use
        // PathTier so traversal, carving and support geometry share one tapered route truth.
        public int PathMinLocalX => PathTier(0).MinX;
        public int FirstRampLocalZ => PathTier(0).LocalZ;

        public int SummitApproachLocalX => CentreLocal - PlaceholderSize / 2 - PathWidth;
        public int SummitApproachLocalZ => CentreLocal - SummitRadius - PathWidth / 2;

        public int SummitApproachWorldX => Origin.x + SummitApproachLocalX;
        public int SummitApproachWorldZ => Origin.z + SummitApproachLocalZ;

        public int CoreRadiusAtHeight(int localY)
        {
            int y = math.clamp(localY, 0, MountainHeight);
            return MountainRadius
                 - (MountainRadius - SummitRadius) * y / MountainHeight;
        }

        public int CoreMinLocalZAtHeight(int localY) =>
            CentreLocal - CoreRadiusAtHeight(localY);

        /// <summary>
        /// Near-face path edge for the tier beginning at startY. One third of the walking surface
        /// remains exposed outside the core at the low end; the rest cuts into the shell rather than
        /// floating in front of it. Natural support masses are biased inward to merge the high end
        /// back into the shrinking core.
        /// </summary>
        public int RampLocalZ(int startY) =>
            CoreMinLocalZAtHeight(startY) - math.max(2, PathWidth / 3);

        /// <summary>
        /// Returns the deterministic route geometry for one switchback. Lower tiers retain the
        /// nominal run. As the core narrows, later tiers shorten from the side opposite the previous
        /// turn, preserving exact landing connectivity without introducing broad connector slabs.
        /// </summary>
        public MountainPathTierGeometry PathTier(int level)
        {
            if ((uint)level >= (uint)SwitchbackCount)
                throw new ArgumentOutOfRangeException(nameof(level));

            int previousHigh = CentreLocal - PathRun / 2;
            int run = PathRun;
            int minX = previousHigh;

            for (int i = 0; i <= level; i++)
            {
                int startY = i * PathRise;
                int endY = startY + PathRise;
                int radiusAtEnd = CoreRadiusAtHeight(endY);
                int minimumRun = math.min(PathRun, PathWidth * 2 + PathRise * 3);
                int desiredRun = math.clamp(
                    radiusAtEnd * 2 - PathWidth,
                    minimumRun,
                    PathRun);

                // The previous turn is authoritative. Alternate direction while shortening only
                // the opposite end, so consecutive landing footprints remain exactly coincident.
                run = desiredRun;
                bool reverse = (i & 1) != 0;
                if (i == 0)
                {
                    minX = CentreLocal - run / 2;
                }
                else if (reverse)
                {
                    minX = previousHigh - (run - PathWidth);
                }
                else
                {
                    minX = previousHigh;
                }

                int high = reverse
                    ? minX
                    : minX + run - PathWidth;
                previousHigh = high;

                if (i == level)
                {
                    return new MountainPathTierGeometry(
                        i,
                        startY,
                        endY,
                        minX,
                        run,
                        RampLocalZ(startY),
                        PathWidth,
                        reverse);
                }
            }

            throw new InvalidOperationException("Unreachable mountain path tier state.");
        }
    }

    /// <summary>
    /// Shared WorldBuilder voxel realization for an authored mountain landmark. It emits a bounded
    /// landform definition plus a separate hard-surface placeholder cube. The ascent is a sequence
    /// of shallow alternating ramps with supported landings, so normal movement can traverse it.
    /// </summary>
    public static class WorldBuilderMountainLandmarkCatalogue
    {
        public const string LandformDefinitionName = "worldbuilder-mountain-landmark";
        public const string PlaceholderDefinitionName = "worldbuilder-mountain-placeholder";

        public const int PathHeadroomVoxels = 24;
        public const int PathClearanceWidthVoxels = 16;

        private const int SupportSegmentSpan = 64;
        private const int MinimumSupportTopRadius = 40;
        private const int MaximumSupportFlare = 112;

        public static FeatureCatalogue Build(
            in MountainLandmarkSpec spec,
            byte mountainMaterial,
            byte pathMaterial,
            byte placeholderMaterial,
            Allocator allocator)
        {
            int[] landformProgram = BuildLandformProgram(in spec, mountainMaterial, pathMaterial);
            int[] placeholderProgram = BuildPlaceholderProgram(spec.PlaceholderSize, placeholderMaterial);
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
                Name = LandformDefinitionName,
                Kind = FeatureKind.Landform,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = spec.Origin.y,
                Footprint = new int3(
                    spec.FootprintEdge,
                    spec.MountainHeight + PathHeadroomVoxels + 2,
                    spec.FootprintEdge),
                MaxSlope = 8,
                Precedence = 100,
                ProgramOffset = 0,
                ProgramLength = landformProgram.Length,
                MaxPrimitives = CountEmitInstructions(landformProgram),
            };

            catalogue.Definitions[1] = new FeatureDefinition
            {
                Name = PlaceholderDefinitionName,
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
                "Mountain landmark catalogue failed validation: " + result);
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
            byte mountainMaterial,
            byte pathMaterial)
        {
            var program = new List<int>(1200);
            int c = spec.CentreLocal;

            EmitFrustum(
                program,
                c, 0, c,
                spec.MountainHeight + 1,
                spec.MountainRadius,
                spec.SummitRadius,
                1,
                mountainMaterial,
                PrimitiveMode.Fill);

            AddAsymmetricMountainShoulders(program, in spec, mountainMaterial);
            AddNaturalPathSupports(program, in spec, mountainMaterial);
            CarvePathHeadroom(program, in spec);
            EmitPathSurface(program, in spec, pathMaterial);

            End(program);
            return program.ToArray();
        }

        private static void AddNaturalPathSupports(
            List<int> program,
            in MountainLandmarkSpec spec,
            byte mountainMaterial)
        {
            MountainPathTierGeometry lastTier = default;
            int endY = 0;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                endY = tier.EndY;
                lastTier = tier;

                AddNaturalSupportMasses(
                    program,
                    tier.MinX, tier.LocalZ,
                    tier.Run, spec.PathWidth,
                    tier.StartY,
                    spec.PathWidth,
                    mountainMaterial,
                    spec.PathWidth / 2);

                if (level + 1 >= spec.SwitchbackCount) continue;

                MountainPathTierGeometry next = spec.PathTier(level + 1);
                int zMin = Math.Min(tier.LocalZ, next.LocalZ);
                int zSize = Math.Abs(next.LocalZ - tier.LocalZ) + spec.PathWidth;
                AddNaturalSupportMasses(
                    program,
                    tier.HighLandingMinX, zMin,
                    spec.PathWidth, zSize,
                    endY,
                    spec.PathWidth,
                    mountainMaterial,
                    0);
            }

            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalZMin = Math.Min(lastTier.LocalZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastTier.LocalZ) + spec.PathWidth;
            AddNaturalSupportMasses(
                program,
                lastTier.HighLandingMinX, finalZMin,
                spec.PathWidth, finalZSize,
                endY,
                spec.PathWidth,
                mountainMaterial,
                0);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastTier.HighLandingMinX, approachX);
            int topSizeX = Math.Abs(approachX - lastTier.HighLandingMinX) + spec.PathWidth;
            int topZ = spec.SummitApproachLocalZ - spec.PathWidth / 2;
            AddNaturalSupportMasses(
                program,
                topMinX, topZ,
                topSizeX, spec.PathWidth,
                spec.MountainHeight,
                spec.PathWidth,
                mountainMaterial,
                spec.PathWidth / 2);
        }

        private static void CarvePathHeadroom(
            List<int> program,
            in MountainLandmarkSpec spec)
        {
            int clearanceWidth = math.min(spec.PathWidth, PathClearanceWidthVoxels);
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
                    tier.Run, spec.PathRise + PathHeadroomVoxels, clearanceWidth,
                    0,
                    PrimitiveMode.Carve);

                if (level + 1 >= spec.SwitchbackCount) continue;

                MountainPathTierGeometry next = spec.PathTier(level + 1);
                int zMin = Math.Min(tier.LocalZ, next.LocalZ);
                int zSize = Math.Abs(next.LocalZ - tier.LocalZ) + spec.PathWidth;
                EmitBox(
                    program,
                    tier.HighLandingMinX + clearanceInset, endY + 1, zMin,
                    clearanceWidth, PathHeadroomVoxels, zSize,
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
                clearanceWidth, finalRise + PathHeadroomVoxels, finalZSize,
                0,
                PrimitiveMode.Carve);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastTier.HighLandingMinX, approachX);
            int topSizeX = Math.Abs(approachX - lastTier.HighLandingMinX) + spec.PathWidth;
            int topZ = spec.SummitApproachLocalZ - spec.PathWidth / 2;
            EmitBox(
                program,
                topMinX, spec.MountainHeight + 1, topZ + clearanceInset,
                topSizeX, PathHeadroomVoxels, clearanceWidth,
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
            byte mountainMaterial)
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
                mountainMaterial,
                PrimitiveMode.FillIfEmpty);

            EmitFrustum(
                program,
                c + r * 42 / 100, 0, c - r * 34 / 100,
                math.max(2, spec.MountainHeight * 55 / 100),
                math.max(2, r * 56 / 100),
                math.max(1, r * 14 / 100),
                1,
                mountainMaterial,
                PrimitiveMode.FillIfEmpty);

            EmitFrustum(
                program,
                c - r * 30 / 100, 0, c - r * 46 / 100,
                math.max(2, spec.MountainHeight * 43 / 100),
                math.max(2, r * 48 / 100),
                math.max(1, r * 12 / 100),
                1,
                mountainMaterial,
                PrimitiveMode.FillIfEmpty);
        }

        private static void AddNaturalSupportMasses(
            List<int> program,
            int minX,
            int minZ,
            int sizeX,
            int sizeZ,
            int supportTopY,
            int pathWidth,
            byte mountainMaterial,
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

                int centreX = alongX ? longCentre : shortCentre + lateralJitter;
                int centreZ = alongX ? shortCentre + lateralJitter : longCentre;
                EmitFrustum(
                    program,
                    centreX, 0, centreZ,
                    supportTopY + 1,
                    baseRadius,
                    topRadius,
                    1,
                    mountainMaterial,
                    PrimitiveMode.FillIfEmpty);
            }
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
