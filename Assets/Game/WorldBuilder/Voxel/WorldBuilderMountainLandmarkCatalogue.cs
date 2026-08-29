using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
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
        public int PathMinLocalX => CentreLocal - PathRun / 2;

        public int FirstRampLocalZ => RampLocalZ(0);

        public int SummitApproachLocalX => CentreLocal - PlaceholderSize / 2 - PathWidth;
        public int SummitApproachLocalZ => CentreLocal - SummitRadius - PathWidth / 2;

        public int SummitApproachWorldX => Origin.x + SummitApproachLocalX;
        public int SummitApproachWorldZ => Origin.z + SummitApproachLocalZ;

        public int RampLocalZ(int startY)
        {
            int radiusAtHeight = MountainRadius
                               - (MountainRadius - SummitRadius) * startY / MountainHeight;
            return CentreLocal - radiusAtHeight - PathWidth - 10;
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

        // VoxelShowcase uses 10 cm voxels and a 2 m navigation-agent height. Keep an extra 40 cm
        // above the authored walking surface so rasterization and collision skin never turn a
        // visually present ramp into a blocked corridor.
        public const int PathHeadroomVoxels = 24;

        // The walking surface may be wider for visual/readability reasons, but empty-space authoring
        // only needs a centered traversal lane. At 10 cm voxels this is 1.6 m wide: a 0.6 m motor
        // retains 0.5 m of lateral clearance on each side without rasterizing the full 3 m path.
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

            // Primitive order is part of the physical contract: establish every scenic/support
            // mass first, carve player-clear air through all of it second, then restore the exact
            // authored walking wedges and landings last. Later primitives win, so no support fill
            // can silently bury a path after its headroom has been opened.
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
            int pathMinX = spec.PathMinLocalX;
            int lastRampZ = 0;
            int lastHighX = pathMinX;
            int endY = 0;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                int startY = level * spec.PathRise;
                endY = startY + spec.PathRise;
                int z = spec.RampLocalZ(startY);
                lastRampZ = z;
                bool reverse = (level & 1) != 0;

                // Only support the authored elevation. Tapered overlapping masses blend into the
                // landform and avoid the tall rectangular retaining walls produced by old boxes.
                AddNaturalSupportMasses(
                    program,
                    pathMinX, z,
                    spec.PathRun, spec.PathWidth,
                    startY,
                    spec.PathWidth,
                    mountainMaterial);

                lastHighX = reverse
                    ? pathMinX
                    : pathMinX + spec.PathRun - spec.PathWidth;

                if (level + 1 >= spec.SwitchbackCount) continue;

                int nextZ = spec.RampLocalZ(endY);
                int zMin = Math.Min(z, nextZ);
                int zSize = Math.Abs(nextZ - z) + spec.PathWidth;
                AddNaturalSupportMasses(
                    program,
                    lastHighX, zMin,
                    spec.PathWidth, zSize,
                    endY,
                    spec.PathWidth,
                    mountainMaterial);
            }

            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalZMin = Math.Min(lastRampZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastRampZ) + spec.PathWidth;
            AddNaturalSupportMasses(
                program,
                lastHighX, finalZMin,
                spec.PathWidth, finalZSize,
                endY,
                spec.PathWidth,
                mountainMaterial);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastHighX, approachX);
            int topSizeX = Math.Abs(approachX - lastHighX) + spec.PathWidth;
            int topZ = spec.SummitApproachLocalZ - spec.PathWidth / 2;
            AddNaturalSupportMasses(
                program,
                topMinX, topZ,
                topSizeX, spec.PathWidth,
                spec.MountainHeight,
                spec.PathWidth,
                mountainMaterial);
        }

        private static void CarvePathHeadroom(
            List<int> program,
            in MountainLandmarkSpec spec)
        {
            int pathMinX = spec.PathMinLocalX;
            int clearanceWidth = math.min(spec.PathWidth, PathClearanceWidthVoxels);
            int clearanceInset = (spec.PathWidth - clearanceWidth) / 2;
            int lastRampZ = 0;
            int lastHighX = pathMinX;
            int endY = 0;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                int startY = level * spec.PathRise;
                endY = startY + spec.PathRise;
                int z = spec.RampLocalZ(startY);
                lastRampZ = z;
                bool reverse = (level & 1) != 0;

                // Clear the complete vertical collision envelope over a centered traversal lane.
                // The full walking surface remains authored for visual width and route readability;
                // the ramp wedge is restored after all carving is complete.
                EmitBox(
                    program,
                    pathMinX, startY + 1, z + clearanceInset,
                    spec.PathRun, spec.PathRise + PathHeadroomVoxels, clearanceWidth,
                    0,
                    PrimitiveMode.Carve);

                lastHighX = reverse
                    ? pathMinX
                    : pathMinX + spec.PathRun - spec.PathWidth;

                if (level + 1 >= spec.SwitchbackCount) continue;

                int nextZ = spec.RampLocalZ(endY);
                int zMin = Math.Min(z, nextZ);
                int zSize = Math.Abs(nextZ - z) + spec.PathWidth;
                EmitBox(
                    program,
                    lastHighX + clearanceInset, endY + 1, zMin,
                    clearanceWidth, PathHeadroomVoxels, zSize,
                    0,
                    PrimitiveMode.Carve);
            }

            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalRise = spec.MountainHeight - endY;
            int finalZMin = Math.Min(lastRampZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastRampZ) + spec.PathWidth;
            EmitBox(
                program,
                lastHighX + clearanceInset, endY + 1, finalZMin,
                clearanceWidth, finalRise + PathHeadroomVoxels, finalZSize,
                0,
                PrimitiveMode.Carve);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastHighX, approachX);
            int topSizeX = Math.Abs(approachX - lastHighX) + spec.PathWidth;
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
            int pathMinX = spec.PathMinLocalX;
            int lastRampZ = 0;
            int lastHighX = pathMinX;
            int endY = 0;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                int startY = level * spec.PathRise;
                endY = startY + spec.PathRise;
                int z = spec.RampLocalZ(startY);
                lastRampZ = z;
                bool reverse = (level & 1) != 0;
                int axis = reverse ? ShapeOps.ReverseRampBit : 0;

                EmitRamp(
                    program,
                    pathMinX, startY, z,
                    spec.PathRun, spec.PathRise + 1, spec.PathWidth,
                    axis,
                    pathMaterial,
                    PrimitiveMode.Fill);

                lastHighX = reverse
                    ? pathMinX
                    : pathMinX + spec.PathRun - spec.PathWidth;

                if (level + 1 >= spec.SwitchbackCount) continue;

                int nextZ = spec.RampLocalZ(endY);
                int zMin = Math.Min(z, nextZ);
                int zSize = Math.Abs(nextZ - z) + spec.PathWidth;
                EmitBox(
                    program,
                    lastHighX, endY, zMin,
                    spec.PathWidth, 1, zSize,
                    pathMaterial,
                    PrimitiveMode.Fill);
            }

            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalRise = spec.MountainHeight - endY;
            int finalZMin = Math.Min(lastRampZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastRampZ) + spec.PathWidth;

            // The final ascent changes from alternating X ramps to a Z ramp. Keep an explicit flat
            // direction-change landing so integer ramp rasterization cannot leave an edge-only join.
            EmitBox(
                program,
                lastHighX, endY, lastRampZ,
                spec.PathWidth, 1, spec.PathWidth,
                pathMaterial,
                PrimitiveMode.Fill);
            EmitRamp(
                program,
                lastHighX, endY, finalZMin,
                spec.PathWidth, finalRise + 1, finalZSize,
                2,
                pathMaterial,
                PrimitiveMode.Fill);

            int approachX = spec.SummitApproachLocalX;
            int topMinX = Math.Min(lastHighX, approachX);
            int topSizeX = Math.Abs(approachX - lastHighX) + spec.PathWidth;
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

            // These deterministic overlapping masses stay inside the original ground footprint,
            // but protrude from the shrinking core at different elevations. That breaks the
            // single perfect-pyramid silhouette without changing placement, path, or summit bounds.
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
            byte mountainMaterial)
        {
            if (supportTopY <= 0) return;

            bool alongX = sizeX >= sizeZ;
            int longMin = alongX ? minX : minZ;
            int longSize = alongX ? sizeX : sizeZ;
            int shortMin = alongX ? minZ : minX;
            int shortSize = alongX ? sizeZ : sizeX;
            int shortCentre = shortMin + shortSize / 2;
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
