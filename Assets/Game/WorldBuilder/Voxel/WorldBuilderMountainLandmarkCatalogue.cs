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
                Footprint = new int3(spec.FootprintEdge, spec.MountainHeight + 2, spec.FootprintEdge),
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
            var program = new List<int>(256);
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

                EmitBox(
                    program,
                    pathMinX, 0, z,
                    spec.PathRun, startY + 1, spec.PathWidth,
                    mountainMaterial,
                    PrimitiveMode.FillIfEmpty);

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
                    lastHighX, 0, zMin,
                    spec.PathWidth, endY + 1, zSize,
                    mountainMaterial,
                    PrimitiveMode.FillIfEmpty);
                EmitBox(
                    program,
                    lastHighX, endY, zMin,
                    spec.PathWidth, 1, zSize,
                    pathMaterial,
                    PrimitiveMode.Fill);
            }

            int summitZ = c - spec.SummitRadius - spec.PathWidth;
            int finalRise = spec.MountainHeight - endY;
            int finalZMin = Math.Min(lastRampZ, summitZ);
            int finalZSize = Math.Abs(summitZ - lastRampZ) + spec.PathWidth;

            EmitBox(
                program,
                lastHighX, 0, finalZMin,
                spec.PathWidth, endY + 1, finalZSize,
                mountainMaterial,
                PrimitiveMode.FillIfEmpty);
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
                topMinX, 0, topZ,
                topSizeX, spec.MountainHeight + 1, spec.PathWidth,
                mountainMaterial,
                PrimitiveMode.FillIfEmpty);
            EmitBox(
                program,
                topMinX, spec.MountainHeight, topZ,
                topSizeX, 1, spec.PathWidth,
                pathMaterial,
                PrimitiveMode.Fill);

            End(program);
            return program.ToArray();
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
