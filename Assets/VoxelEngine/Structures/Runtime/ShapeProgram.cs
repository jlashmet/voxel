using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime.Emitters;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Why an evaluation stopped short. Anything but Ok is a catalogue defect.</summary>
    public enum EvaluationResult
    {
        Ok = 0,
        PrimitiveLimitExceeded = 1,
        MalformedProgram = 2,
        TransformStackOverflow = 3,
        OutsideFootprint = 4,
    }

    /// <summary>
    /// Evaluates a shape program into primitives.
    ///
    /// Pure, total, and bounded: the same inputs give the same primitives on every platform, every
    /// parameter combination inside the declared ranges produces a valid list, and the list length
    /// has a ceiling validation proved before the world loaded.
    ///
    /// The evaluator never touches voxels and cannot read the brickmap. It has exactly one window
    /// onto the world — <see cref="ShapeOp.SampleGround"/>, which reads
    /// <see cref="TerrainQuery"/>, a pure function of position. That restriction is what keeps
    /// generation region-local: a program that could inspect what was already there would produce
    /// different results depending on which region generated first.
    ///
    /// Integer throughout (Constitution I).
    /// </summary>
    public static class ShapeProgram
    {
        /// <summary>State carried through one instance's evaluation.</summary>
        private struct Frame
        {
            public int3 Offset;
        }

        /// <summary>
        /// Evaluates <paramref name="definition"/>'s program.
        ///
        /// <paramref name="origin"/> is where the instance's local origin lands in the world, and
        /// <paramref name="orientation"/> is one of four cardinal rotations about Y. Rotation is
        /// applied to finished primitives rather than to coordinates as the program runs, so a
        /// program author never has to think about it.
        /// </summary>
        public static EvaluationResult Evaluate(
            in FeatureCatalogue catalogue,
            int definitionId,
            in ParameterSet parameters,
            int3 origin,
            byte orientation,
            uint terrainSeed,
            ulong instanceSeed,
            NativeList<Primitive> primitives,
            NativeList<ResolvedAnchor> anchors)
        {
            if ((uint)definitionId >= (uint)catalogue.DefinitionCount)
                return EvaluationResult.MalformedProgram;

            var definition = catalogue.Definitions[definitionId];

            var registers = new NativeArray<int>(ShapeOps.RegisterCount, Allocator.Temp);
            var stack = new NativeArray<Frame>(ShapeOps.MaxTransformDepth, Allocator.Temp);

            for (var i = 0; i < definition.ParameterCount && i < ShapeOps.RegisterCount; i++)
                registers[ShapeOps.FirstParameterRegister + i] = parameters[i];

            registers[ShapeOps.RegisterBase] = BasePlane(in definition, origin, terrainSeed);

            int stackDepth = 0;
            stack[0] = new Frame { Offset = int3.zero };

            ulong drawState = instanceSeed;
            int emitted = 0;

            var result = Run(
                in catalogue, in definition, definition.ProgramOffset, definition.ProgramLength,
                registers, stack, ref stackDepth, ref drawState, ref emitted,
                origin, orientation, terrainSeed, primitives, anchors);

            registers.Dispose();
            stack.Dispose();

            return result;
        }

        /// <summary>
        /// Executes a run of instructions. Recurses for structured control flow only — there is no
        /// jump, so a program cannot loop forever and the recursion depth is the nesting depth the
        /// author wrote.
        /// </summary>
        private static EvaluationResult Run(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition,
            int start, int length,
            NativeArray<int> registers,
            NativeArray<Frame> stack,
            ref int stackDepth,
            ref ulong drawState,
            ref int emitted,
            int3 origin, byte orientation, uint terrainSeed,
            NativeList<Primitive> primitives,
            NativeList<ResolvedAnchor> anchors)
        {
            var program = catalogue.Program;
            int pc = start;
            int end = start + length;

            while (pc < end)
            {
                if (pc + 1 >= program.Length) return EvaluationResult.MalformedProgram;

                var op = (ShapeOp)program[pc];
                int mask = program[pc + 1];

                int operandCount = ShapeOps.OperandCount(op);
                if (operandCount < 0) return EvaluationResult.MalformedProgram;

                int operandBase = pc + 2;
                if (operandBase + operandCount > end) return EvaluationResult.MalformedProgram;

                if (op == ShapeOp.End) return EvaluationResult.Ok;

                int o0 = Resolve(program, registers, operandBase, mask, 0, operandCount);
                int o1 = Resolve(program, registers, operandBase, mask, 1, operandCount);
                int o2 = Resolve(program, registers, operandBase, mask, 2, operandCount);
                int o3 = Resolve(program, registers, operandBase, mask, 3, operandCount);
                int o4 = Resolve(program, registers, operandBase, mask, 4, operandCount);
                int o5 = Resolve(program, registers, operandBase, mask, 5, operandCount);
                int o6 = Resolve(program, registers, operandBase, mask, 6, operandCount);
                int o7 = Resolve(program, registers, operandBase, mask, 7, operandCount);
                int o8 = Resolve(program, registers, operandBase, mask, 8, operandCount);
                int o9 = Resolve(program, registers, operandBase, mask, 9, operandCount);
                int o10 = Resolve(program, registers, operandBase, mask, 10, operandCount);
                int o11 = Resolve(program, registers, operandBase, mask, 11, operandCount);
                int o12 = Resolve(program, registers, operandBase, mask, 12, operandCount);
                int o13 = Resolve(program, registers, operandBase, mask, 13, operandCount);
                int o14 = Resolve(program, registers, operandBase, mask, 14, operandCount);

                int advance = ShapeOps.InstructionLength(op);

                switch (op)
                {
                    case ShapeOp.EmitBox:
                    case ShapeOp.EmitCylinder:
                    case ShapeOp.EmitPrism:
                    case ShapeOp.EmitCapsule:
                    case ShapeOp.EmitRamp:
                    case ShapeOp.EmitRoundedBox:
                    case ShapeOp.EmitEllipsoid:
                    case ShapeOp.EmitFrustum:
                    case ShapeOp.EmitAnnulus:
                    case ShapeOp.EmitArcWedge:
                    {
                        if (emitted >= definition.MaxPrimitives ||
                            emitted >= FeatureBudget.MaxPrimitivesPerInstance)
                            return EvaluationResult.PrimitiveLimitExceeded;

                        var primitive = BuildPrimitive(op, o0, o1, o2, o3, o4, o5, o6, o7, o8,
                                                       o9, o10,
                                                       o11, o12, o13, o14,
                                                       stack[stackDepth].Offset, emitted);

                        primitive = Orient(primitive, definition.Footprint, orientation);
                        primitive.A += origin;
                        primitive.B += origin;
                        if (primitive.Shape >= PrimitiveShape.Ellipsoid)
                            primitive.C += origin;

                        primitives.Add(primitive);
                        emitted++;
                        break;
                    }

                    case ShapeOp.PushTransform:
                    {
                        if (stackDepth + 1 >= ShapeOps.MaxTransformDepth)
                            return EvaluationResult.TransformStackOverflow;

                        var offset = new int3(o0, o1, o2);
                        stack[stackDepth + 1] = new Frame { Offset = stack[stackDepth].Offset + offset };
                        stackDepth++;
                        break;
                    }

                    case ShapeOp.PopTransform:
                        if (stackDepth > 0) stackDepth--;
                        break;

                    case ShapeOp.Repeat:
                    {
                        int count = o0;
                        var stride = new int3(o1, o2, o3);
                        int bodyInstructions = o4;

                        int bodyStart = pc + advance;
                        int bodyLength = MeasureInstructions(catalogue, bodyStart, end, bodyInstructions);
                        if (bodyLength < 0) return EvaluationResult.MalformedProgram;

                        if (count < 0) count = 0;
                        if (count > FeatureBudget.MaxPrimitivesPerInstance)
                            return EvaluationResult.PrimitiveLimitExceeded;

                        for (var i = 0; i < count; i++)
                        {
                            if (stackDepth + 1 >= ShapeOps.MaxTransformDepth)
                                return EvaluationResult.TransformStackOverflow;

                            stack[stackDepth + 1] = new Frame
                            {
                                Offset = stack[stackDepth].Offset + stride * i,
                            };
                            stackDepth++;

                            var inner = Run(in catalogue, in definition, bodyStart, bodyLength,
                                            registers, stack, ref stackDepth, ref drawState,
                                            ref emitted, origin, orientation, terrainSeed,
                                            primitives, anchors);

                            stackDepth--;
                            if (inner != EvaluationResult.Ok) return inner;
                        }

                        advance += bodyLength;
                        break;
                    }

                    case ShapeOp.IfRange:
                    {
                        int value = o0;
                        int min = o1;
                        int max = o2;
                        int bodyInstructions = o3;

                        int bodyStart = pc + advance;
                        int bodyLength = MeasureInstructions(catalogue, bodyStart, end, bodyInstructions);
                        if (bodyLength < 0) return EvaluationResult.MalformedProgram;

                        if (value >= min && value <= max)
                        {
                            var inner = Run(in catalogue, in definition, bodyStart, bodyLength,
                                            registers, stack, ref stackDepth, ref drawState,
                                            ref emitted, origin, orientation, terrainSeed,
                                            primitives, anchors);

                            if (inner != EvaluationResult.Ok) return inner;
                        }

                        advance += bodyLength;
                        break;
                    }

                    case ShapeOp.SampleGround:
                    {
                        int destination = program[operandBase];
                        int worldX = origin.x + stack[stackDepth].Offset.x + o1;
                        int worldZ = origin.z + stack[stackDepth].Offset.z + o2;

                        if ((uint)destination < (uint)ShapeOps.RegisterCount)
                            registers[destination] = TerrainQuery.HeightAt(worldX, worldZ, terrainSeed);

                        break;
                    }

                    case ShapeOp.DrawRange:
                    {
                        int destination = program[operandBase];
                        int min = o1;
                        int max = o2;
                        int quantum = o3;

                        int value = FeatureHash.Range(ref drawState, min, max);
                        if (quantum > 1) value = min + ((value - min) / quantum) * quantum;

                        if ((uint)destination < (uint)ShapeOps.RegisterCount)
                            registers[destination] = value;

                        break;
                    }

                    case ShapeOp.SetAnchor:
                    {
                        int anchorIndex = o0;
                        var local = new int3(o1, o2, o3) + stack[stackDepth].Offset;
                        var facing = (Facing)o4;

                        if ((uint)anchorIndex < (uint)definition.AnchorCount)
                        {
                            var spec = catalogue.Anchors[definition.AnchorOffset + anchorIndex];
                            var rotated = RotatePoint(local, definition.Footprint, orientation);

                            anchors.Add(new ResolvedAnchor
                            {
                                Name = spec.Name,
                                Position = origin + rotated,
                                Facing = RotateFacing(facing, orientation),
                            });
                        }

                        break;
                    }

                    case ShapeOp.Arithmetic:
                    {
                        int destination = program[operandBase];
                        int a = o1;
                        int b = o2;
                        var operation = (ArithmeticOp)o3;

                        int value = operation switch
                        {
                            ArithmeticOp.Add => a + b,
                            ArithmeticOp.Subtract => a - b,
                            ArithmeticOp.Multiply => a * b,
                            ArithmeticOp.Divide => b == 0 ? 0 : a / b,
                            ArithmeticOp.Min => a < b ? a : b,
                            ArithmeticOp.Max => a > b ? a : b,
                            _ => 0,
                        };

                        if ((uint)destination < (uint)ShapeOps.RegisterCount)
                            registers[destination] = value;

                        break;
                    }

                    case ShapeOp.CallSlot:
                        break;
                }

                pc += advance;
            }

            return EvaluationResult.Ok;
        }

        private static int MeasureInstructions(in FeatureCatalogue catalogue, int start, int end, int count)
        {
            var program = catalogue.Program;
            int pc = start;

            for (var i = 0; i < count; i++)
            {
                if (pc >= end || pc >= program.Length) return -1;

                var op = (ShapeOp)program[pc];
                int length = ShapeOps.InstructionLength(op);
                if (length < 0) return -1;

                pc += length;

                if (op == ShapeOp.Repeat || op == ShapeOp.IfRange)
                {
                    int nested = program[pc - 1];
                    int nestedLength = MeasureInstructions(catalogue, pc, end, nested);
                    if (nestedLength < 0) return -1;
                    pc += nestedLength;
                }
            }

            return pc - start;
        }

        private static int Resolve(NativeArray<int> program, NativeArray<int> registers,
                                   int operandBase, int mask, int index, int operandCount)
        {
            if (index >= operandCount) return 0;

            int raw = program[operandBase + index];
            if ((mask & (1 << index)) == 0) return raw;

            return (uint)raw < (uint)ShapeOps.RegisterCount ? registers[raw] : 0;
        }

        private static Primitive BuildPrimitive(ShapeOp op,
                                                int o0, int o1, int o2, int o3, int o4,
                                                int o5, int o6, int o7, int o8,
                                                int o9, int o10,
                                                int o11, int o12, int o13, int o14,
                                                int3 offset, int order)
        {
            switch (op)
            {
                case ShapeOp.EmitBox:
                    return BoxEmitter.Box(
                        new int3(o0, o1, o2) + offset,
                        new int3(o3, o4, o5),
                        (byte)o6, (PrimitiveMode)o9, order, (ushort)o7, (byte)o8);

                case ShapeOp.EmitCylinder:
                    return CylinderEmitter.Cylinder(
                        new int3(o0, o1, o2) + offset,
                        o3, o4, (byte)o5,
                        (byte)o6, (PrimitiveMode)o9, order, (ushort)o7, (byte)o8);

                case ShapeOp.EmitPrism:
                    return PrismEmitter.Prism(
                        new int3(o0, o1, o2) + offset,
                        new int3(o3, o4, o5),
                        (PrismProfile)o6, (byte)o7,
                        (PrimitiveMode)o10, order, (ushort)o8, (byte)o9);

                case ShapeOp.EmitCapsule:
                    return CapsuleChainEmitter.Capsule(
                        new int3(o0, o1, o2) + offset,
                        new int3(o3, o4, o5) + offset,
                        o6, (byte)o7, (PrimitiveMode)o10, order, (ushort)o8, (byte)o9);

                case ShapeOp.EmitRoundedBox:
                    return CurvedPrimitiveEmitter.RoundedBox(
                        new int3(o0, o1, o2) + offset, new int3(o3, o4, o5), o6,
                        (byte)o7, (ushort)o8, (PrimitiveMode)o10, order, (byte)o9);

                case ShapeOp.EmitEllipsoid:
                    return CurvedPrimitiveEmitter.Ellipsoid(
                        new int3(o0, o1, o2) + offset, new int3(o3, o4, o5),
                        (byte)o6, (ushort)o7, (PrimitiveMode)o9, order, (byte)o8);

                case ShapeOp.EmitFrustum:
                    return CurvedPrimitiveEmitter.Frustum(
                        new int3(o0, o1, o2) + offset, o3, o4, o5, (byte)o6,
                        (byte)o7, (ushort)o8, (PrimitiveMode)o10, order, (byte)o9);

                case ShapeOp.EmitAnnulus:
                    return CurvedPrimitiveEmitter.Annulus(
                        new int3(o0, o1, o2) + offset, o3, o4, o5, (byte)o6, o7 != 0,
                        (byte)o8, (ushort)o9, (PrimitiveMode)o11, order, (byte)o10);

                case ShapeOp.EmitArcWedge:
                    return CurvedPrimitiveEmitter.ArcWedge(
                        new int3(o0, o1, o2) + offset, o3, o4, o5, (byte)o6,
                        new int2(o7, o8), new int2(o9, o10), (byte)o11, (ushort)o12,
                        (PrimitiveMode)o14, order, (byte)o13);

                case ShapeOp.EmitRamp:
                    return BoxEmitter.Ramp(
                        new int3(o0, o1, o2) + offset,
                        new int3(o3, o4, o5),
                        (byte)o6, (byte)o7,
                        (PrimitiveMode)o10, order, (ushort)o8, (byte)o9);

                default:
                    return default;
            }
        }

        private static Primitive Orient(Primitive p, int3 footprint, byte orientation)
        {
            if ((orientation & 3) == 0) return p;

            byte originalAxis = p.Axis;

            int3 a = RotatePoint(p.A, footprint, orientation);
            int3 b = RotatePoint(p.B, footprint, orientation);

            p.A = math.min(a, b);
            p.B = math.max(a, b);

            if (p.Shape >= PrimitiveShape.Ellipsoid)
                p.C = RotatePoint(p.C, footprint, orientation);

            if ((orientation & 1) != 0)
            {
                if (p.Axis == 0) p.Axis = 2;
                else if (p.Axis == 2) p.Axis = 0;

                if (p.Shape == PrimitiveShape.Ellipsoid)
                {
                    int radius = p.D.x;
                    p.D.x = p.D.z;
                    p.D.z = radius;
                }
            }

            if (p.Shape == PrimitiveShape.Ramp || p.Shape == PrimitiveShape.Frustum)
            {
                int initialDirection = p.Direction < 0 ? -1 : 1;
                p.Direction = (sbyte)(initialDirection
                    * RotateAxisSign(originalAxis, p.Axis, orientation));
            }
            else if (p.Shape == PrimitiveShape.Prism)
            {
                int3 profileVector = RotateVector(new int3(1, 0, 0), orientation);
                int profileAxis = p.Axis == 0 ? 2 : 0;
                p.Direction = (sbyte)(profileVector[profileAxis] < 0 ? -1 : 1);
            }

            if (p.Shape == PrimitiveShape.ArcWedge)
            {
                int2 start = RotateRadialDirection(p.StartDirection, originalAxis,
                                                   p.Axis, orientation, out int axisSign);
                int2 end = RotateRadialDirection(p.EndDirection, originalAxis,
                                                 p.Axis, orientation, out _);
                if (axisSign < 0)
                {
                    p.StartDirection = end;
                    p.EndDirection = start;
                }
                else
                {
                    p.StartDirection = start;
                    p.EndDirection = end;
                }
            }

            return p;
        }

        private static int2 RotateRadialDirection(int2 direction, byte originalAxis,
                                                  byte rotatedAxis, byte orientation,
                                                  out int axisSign)
        {
            int originalA = (originalAxis + 1) % 3;
            int originalB = (originalAxis + 2) % 3;
            int3 vector = default;
            vector[originalA] = direction.x;
            vector[originalB] = direction.y;

            int3 axisVector = default;
            axisVector[originalAxis] = 1;
            vector = RotateVector(vector, orientation);
            axisVector = RotateVector(axisVector, orientation);
            axisSign = axisVector[rotatedAxis] < 0 ? -1 : 1;

            int rotatedA = (rotatedAxis + 1) % 3;
            int rotatedB = (rotatedAxis + 2) % 3;
            return new int2(vector[rotatedA], vector[rotatedB]);
        }

        private static int RotateAxisSign(byte originalAxis, byte rotatedAxis, byte orientation)
        {
            int3 axisVector = default;
            axisVector[originalAxis] = 1;
            axisVector = RotateVector(axisVector, orientation);
            return axisVector[rotatedAxis] < 0 ? -1 : 1;
        }

        private static int3 RotateVector(int3 vector, byte orientation) =>
            (orientation & 3) switch
            {
                1 => new int3(-vector.z, vector.y, vector.x),
                2 => new int3(-vector.x, vector.y, -vector.z),
                3 => new int3(vector.z, vector.y, -vector.x),
                _ => vector,
            };

        private static int3 RotatePoint(int3 p, int3 footprint, byte orientation)
        {
            int maxX = footprint.x - 1;
            int maxZ = footprint.z - 1;

            return (orientation & 3) switch
            {
                1 => new int3(maxZ - p.z, p.y, p.x),
                2 => new int3(maxX - p.x, p.y, maxZ - p.z),
                3 => new int3(p.z, p.y, maxX - p.x),
                _ => p,
            };
        }

        private static Facing RotateFacing(Facing facing, byte orientation)
        {
            if (facing == Facing.Up || facing == Facing.Down) return facing;
            return (Facing)(((int)facing + orientation) & 3);
        }

        private static int BasePlane(in FeatureDefinition definition, int3 origin, uint terrainSeed)
        {
            if (definition.BasePlane == BasePlaneRule.FixedAltitude)
                return definition.FixedAltitude;

            const int samplesPerAxis = 5;

            int lowest = int.MaxValue;
            int highest = int.MinValue;
            long total = 0;

            for (var iz = 0; iz < samplesPerAxis; iz++)
            for (var ix = 0; ix < samplesPerAxis; ix++)
            {
                int x = origin.x + (definition.Footprint.x - 1) * ix / (samplesPerAxis - 1);
                int z = origin.z + (definition.Footprint.z - 1) * iz / (samplesPerAxis - 1);

                int h = TerrainQuery.HeightAt(x, z, terrainSeed);

                if (h < lowest) lowest = h;
                if (h > highest) highest = h;
                total += h;
            }

            return definition.BasePlane switch
            {
                BasePlaneRule.LowestGround => lowest,
                BasePlaneRule.HighestGround => highest,
                _ => (int)(total / (samplesPerAxis * samplesPerAxis)),
            };
        }
    }
}
