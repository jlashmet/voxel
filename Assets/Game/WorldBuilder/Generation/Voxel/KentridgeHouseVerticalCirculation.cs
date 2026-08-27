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
        private const int StairWidthDm = 9;
        private const int StairGapDm = 2;
        private const int LandingLengthDm = 3;
        private const int PreferredStepRiseDm = 2;
        private const int SideInsetDm = 4;
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

            int stepRise = PreferredStepRiseDm * scale;
            if (floorHeight % stepRise != 0)
                stepRise = scale;
            int stepCount = floorHeight / stepRise;
            int stepRun = stepRise;
            int firstFlightSteps = stepCount / 2;
            int secondFlightSteps = stepCount - firstFlightSteps;
            int stairWidth = StairWidthDm * scale;
            int stairGap = StairGapDm * scale;
            int landingLength = LandingLengthDm * scale;
            int openingSideClearance = OpeningSideClearanceDm * scale;
            int layoutWidth = stairWidth * 2 + stairGap;

            var stair = new StairConfig
            {
                Direction = StructureRunDirection.PositiveZ,
                Layout = StructureStairLayout.Switchback,
                Width = stairWidth,
                StepCount = stepCount,
                StepRise = stepRise,
                StepRun = stepRun,
                StepsPerFlight = firstFlightSteps,
                Landing = new LandingConfig
                {
                    Width = layoutWidth,
                    Length = landingLength,
                    Thickness = stepRise,
                    MaterialRole = StructureMaterialRole.Trim,
                },
                MaterialRole = StructureMaterialRole.Trim,
            };
            if (!stair.IsWellFormed
                || stair.TotalRise != floorHeight
                || firstFlightSteps <= 0
                || secondFlightSteps <= 0)
            {
                throw new InvalidOperationException(
                    "Generated Kentridge floor height cannot produce a bounded shared stair run.");
            }

            int firstRun = firstFlightSteps * stair.StepRun;
            int secondRun = secondFlightSteps * stair.StepRun;
            int flightRun = Math.Max(firstRun, secondRun);
            int southZ = front + FrontInsetDm * scale;
            int landingMinZ = southZ + flightRun;
            int northZ = landingMinZ + stair.Landing.Length;

            // Keep the public entrance approach open by putting the stair against the side opposite
            // the authored door bias. The return flight sits toward the room centre.
            bool firstFlightOnRight = form.DoorOffsetDm <= 0;
            int firstFlightX;
            int secondFlightX;
            if (firstFlightOnRight)
            {
                firstFlightX = right - SideInsetDm * scale - stair.Width;
                secondFlightX = firstFlightX - stairGap - stair.Width;
            }
            else
            {
                firstFlightX = left + SideInsetDm * scale;
                secondFlightX = firstFlightX + stair.Width + stairGap;
            }

            int layoutMinX = Math.Min(firstFlightX, secondFlightX);
            int layoutMaxX = Math.Max(firstFlightX, secondFlightX) + stair.Width;
            if (layoutWidth + SideInsetDm * scale > interiorWidth
                || layoutMinX - openingSideClearance < left
                || layoutMaxX + openingSideClearance > right
                || northZ + openingSideClearance > rear)
            {
                throw new InvalidOperationException(
                    "Generated Kentridge interior is too small for its constrained switchback stairwell.");
            }

            int headroom = Math.Min(MinimumHeadroomDm * scale, floorHeight - slabThickness);
            int clearanceThreshold = Math.Max(0, floorHeight - slabThickness - headroom);
            int firstOpenStep = Math.Min(
                firstFlightSteps - 1,
                clearanceThreshold / stair.StepRise);

            int firstStartZ = landingMinZ - firstRun;
            int firstOpeningMinZ = firstStartZ + firstOpenStep * stair.StepRun;
            int firstOpeningLength = northZ - firstOpeningMinZ;
            int secondSouthZ = landingMinZ - secondRun;
            int secondOpeningLength = northZ - secondSouthZ;
            int guardHeight = GuardHeightDm * scale;
            int guardThickness = Math.Max(1, GuardThicknessDm * scale);
            byte timber = settings.Materials.Resolve(theme.Frame);

            var code = new List<int>(
                program.Length + (form.Storeys - 1) * (stair.StepCount + 8) * 12);
            for (int i = 0; i < program.Length - endLength; i++)
                code.Add(program[i]);

            for (int level = 0; level < form.Storeys - 1; level++)
            {
                int lowerFloorY = foundation + level * floorHeight;
                int upperFloorY = lowerFloorY + floorHeight;

                // The first-flight carve begins exactly where remaining ceiling clearance drops
                // below required headroom. The return flight and half-storey landing are already
                // above that threshold, so their bounded shaft remains open through the slab.
                EmitBox(
                    code,
                    firstFlightX - openingSideClearance,
                    upperFloorY - slabThickness,
                    firstOpeningMinZ,
                    stair.Width + openingSideClearance * 2,
                    slabThickness,
                    firstOpeningLength,
                    0,
                    PrimitiveMode.Carve);
                EmitBox(
                    code,
                    secondFlightX - openingSideClearance,
                    upperFloorY - slabThickness,
                    secondSouthZ,
                    stair.Width + openingSideClearance * 2,
                    slabThickness,
                    secondOpeningLength,
                    0,
                    PrimitiveMode.Carve);

                for (int step = 0; step < firstFlightSteps; step++)
                {
                    EmitBox(
                        code,
                        firstFlightX,
                        lowerFloorY,
                        firstStartZ + step * stair.StepRun,
                        stair.Width,
                        (step + 1) * stair.StepRise,
                        stair.StepRun,
                        timber,
                        PrimitiveMode.Fill);
                }

                EmitBox(
                    code,
                    layoutMinX,
                    lowerFloorY,
                    landingMinZ,
                    layoutMaxX - layoutMinX,
                    firstFlightSteps * stair.StepRise,
                    stair.Landing.Length,
                    timber,
                    PrimitiveMode.Fill);

                for (int step = 0; step < secondFlightSteps; step++)
                {
                    EmitBox(
                        code,
                        secondFlightX,
                        lowerFloorY,
                        landingMinZ - (step + 1) * stair.StepRun,
                        stair.Width,
                        (firstFlightSteps + step + 1) * stair.StepRise,
                        stair.StepRun,
                        timber,
                        PrimitiveMode.Fill);
                }

                int guardMinX = layoutMinX - openingSideClearance;
                int guardWidth = layoutMaxX - layoutMinX + openingSideClearance * 2;
                int guardMinZ = Math.Min(firstOpeningMinZ, secondSouthZ);
                int guardLength = northZ - guardMinZ;

                // Perimeter guards leave only the return flight's south-end upper-floor egress open.
                EmitBox(code, guardMinX, upperFloorY, guardMinZ,
                    guardThickness, guardHeight, guardLength, timber, PrimitiveMode.Fill);
                EmitBox(code, guardMinX + guardWidth - guardThickness, upperFloorY, guardMinZ,
                    guardThickness, guardHeight, guardLength, timber, PrimitiveMode.Fill);
                EmitBox(code, guardMinX, upperFloorY, northZ - guardThickness,
                    guardWidth, guardHeight, guardThickness, timber, PrimitiveMode.Fill);
                EmitBox(
                    code,
                    firstFlightX - openingSideClearance,
                    upperFloorY,
                    firstOpeningMinZ,
                    stair.Width + openingSideClearance * 2,
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
