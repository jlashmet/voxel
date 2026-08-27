using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Adds bounded vertical circulation to generated Kentridge houses. Stair geometry, the
    /// intermediate-floor opening, and its upper guard are derived from one shared StairConfig so
    /// the three pieces cannot drift into unrelated placement constraints.
    /// </summary>
    internal static class KentridgeHouseVerticalCirculation
    {
        private const int StairWidthDm = 10;
        private const int PreferredStepRiseDm = 2;
        private const int SideInsetDm = 5;
        private const int FrontInsetDm = 5;
        private const int OpeningSideClearanceDm = 1;
        private const int MinimumHeadroomDm = 19;
        private const int GuardHeightDm = 9;
        private const int GuardThicknessDm = 1;

        public static int[] Decorate(
            StructureForm form,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            int[] program)
        {
            if (!form.IsGenerated)
                throw new ArgumentException(
                    "Vertical circulation only decorates generated Kentridge structures.",
                    nameof(form));
            if (form.Storeys <= 1)
                return program;

            int endLength = ShapeOps.InstructionLength(ShapeOp.End);
            if (program == null
                || program.Length < endLength
                || (ShapeOp)program[program.Length - endLength] != ShapeOp.End)
            {
                throw new InvalidOperationException(
                    "Generated Kentridge structure program is missing its terminal End instruction.");
            }

            int scale = settings.VoxelsPerDecimetre;
            int width = form.WidthDm * scale;
            int depth = form.DepthDm * scale;
            int foundation = theme.FoundationHeightDm * scale;
            int floorHeight = theme.FloorHeightDm * scale;
            int wall = Math.Max(1, theme.WallThicknessDm * scale);
            int slabThickness = Math.Max(1, 3 * scale);
            Int3 envelopeDm = SettlementFootprints.For(settings.Settlement, form.Archetype);
            int x0 = (envelopeDm.X * scale - width) / 2;
            int z0 = 10 * scale;

            int left = x0 + wall;
            int right = x0 + width - wall;
            int front = z0 + wall;
            int rear = z0 + depth - wall;
            int interiorWidth = right - left;
            int interiorDepth = rear - front;

            int stepRise = PreferredStepRiseDm * scale;
            if (floorHeight % stepRise != 0)
                stepRise = scale;
            int stepCount = floorHeight / stepRise;
            int stepRun = stepRise;
            int openingSideClearance = OpeningSideClearanceDm * scale;
            int stairWidth = Math.Min(
                StairWidthDm * scale,
                interiorWidth - (SideInsetDm * 2 * scale));

            var stair = new StairConfig
            {
                Direction = StructureRunDirection.PositiveZ,
                Layout = StructureStairLayout.Straight,
                Width = stairWidth,
                StepCount = stepCount,
                StepRise = stepRise,
                StepRun = stepRun,
                StepsPerFlight = 0,
                Landing = default,
                MaterialRole = StructureMaterialRole.Trim,
            };
            if (!stair.IsWellFormed || stair.TotalRise != floorHeight)
                throw new InvalidOperationException(
                    "Generated Kentridge floor height cannot produce a bounded shared stair run.");

            int runLength = stair.TotalRun;
            int stairMinX = right - SideInsetDm * scale - stair.Width;
            int southZ = front + FrontInsetDm * scale;
            int northZ = southZ + runLength;
            if (stairMinX - openingSideClearance < left
                || northZ + openingSideClearance > rear)
            {
                throw new InvalidOperationException(
                    "Generated Kentridge interior is too small for its constrained stairwell.");
            }

            int headroom = Math.Min(MinimumHeadroomDm * scale, floorHeight - slabThickness);
            int clearanceThreshold = Math.Max(0, floorHeight - slabThickness - headroom);
            int firstOpenStep = Math.Min(
                stair.StepCount - 1,
                clearanceThreshold / stair.StepRise);
            int openingOffset = firstOpenStep * stair.StepRun;
            int openingLength = stair.TotalRun - openingOffset;
            int openingMinX = stairMinX - openingSideClearance;
            int openingWidth = stair.Width + openingSideClearance * 2;
            int guardHeight = GuardHeightDm * scale;
            int guardThickness = Math.Max(1, GuardThicknessDm * scale);
            byte timber = settings.Materials.Resolve(theme.Frame);

            var code = new List<int>(
                program.Length + (form.Storeys - 1) * (stair.StepCount + 4) * 12);
            for (int i = 0; i < program.Length - endLength; i++)
                code.Add(program[i]);

            for (int level = 0; level < form.Storeys - 1; level++)
            {
                bool ascendNorth = (level & 1) == 0;
                int lowerFloorY = foundation + level * floorHeight;
                int upperFloorY = lowerFloorY + floorHeight;
                int openingMinZ = ascendNorth
                    ? southZ + openingOffset
                    : southZ;

                EmitBox(
                    code,
                    openingMinX,
                    upperFloorY - slabThickness,
                    openingMinZ,
                    openingWidth,
                    slabThickness,
                    openingLength,
                    0,
                    PrimitiveMode.Carve);

                for (int step = 0; step < stair.StepCount; step++)
                {
                    int stepZ = ascendNorth
                        ? southZ + step * stair.StepRun
                        : northZ - (step + 1) * stair.StepRun;
                    EmitBox(
                        code,
                        stairMinX,
                        lowerFloorY,
                        stepZ,
                        stair.Width,
                        (step + 1) * stair.StepRise,
                        stair.StepRun,
                        timber,
                        PrimitiveMode.Fill);
                }

                // Guard both long sides and the edge opposite the stair's upper-floor egress.
                EmitBox(
                    code,
                    openingMinX,
                    upperFloorY,
                    openingMinZ,
                    guardThickness,
                    guardHeight,
                    openingLength,
                    timber,
                    PrimitiveMode.Fill);
                EmitBox(
                    code,
                    openingMinX + openingWidth - guardThickness,
                    upperFloorY,
                    openingMinZ,
                    guardThickness,
                    guardHeight,
                    openingLength,
                    timber,
                    PrimitiveMode.Fill);
                EmitBox(
                    code,
                    openingMinX,
                    upperFloorY,
                    ascendNorth
                        ? openingMinZ
                        : openingMinZ + openingLength - guardThickness,
                    openingWidth,
                    guardHeight,
                    guardThickness,
                    timber,
                    PrimitiveMode.Fill);
            }

            for (int i = program.Length - endLength; i < program.Length; i++)
                code.Add(program[i]);
            return code.ToArray();
        }

        private static void EmitBox(
            List<int> code,
            int x,
            int y,
            int z,
            int sx,
            int sy,
            int sz,
            byte material,
            PrimitiveMode mode)
        {
            code.Add((int)ShapeOp.EmitBox);
            code.Add(0);
            code.Add(x);
            code.Add(y);
            code.Add(z);
            code.Add(sx);
            code.Add(sy);
            code.Add(sz);
            code.Add(material);
            code.Add(0);
            code.Add(0);
            code.Add((int)mode);
        }
    }
}
