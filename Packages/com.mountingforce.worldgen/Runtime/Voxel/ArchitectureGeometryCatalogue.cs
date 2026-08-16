using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Generic voxel realisation pass for renderer-independent architecture geometry profiles.
    ///
    /// This pass is deliberately city-independent. The input catalogue still owns semantic
    /// composition (shells, openings, trim, roofs and anchors), while the profile controls how its
    /// box primitives are realised. It is also a migration bridge for older catalogues whose shape
    /// programs do not yet tag architectural operations explicitly: once those catalogues emit
    /// semantic operations directly, they can use the same profile without this material inference.
    /// </summary>
    public static class ArchitectureGeometryCatalogue
    {
        private const int MaxLikelyOpeningWidthDm = 20;

        public static FeatureCatalogue Apply(
            in FeatureCatalogue source,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            IReadOnlyList<StructureGeometryProfile> profiles,
            Allocator allocator)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (profiles.Count != source.Definitions.Length)
                throw new ArgumentException(
                    "A geometry profile is required for every structure definition.",
                    nameof(profiles));

            int scale = settings.VoxelsPerDecimetre;
            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte accent = settings.Materials.Resolve(theme.AccentStone);

            var rewritten = new int[source.Definitions.Length][];
            int programLength = 0;
            for (int i = 0; i < source.Definitions.Length; i++)
            {
                FeatureDefinition definition = source.Definitions[i];
                rewritten[i] = RewriteProgram(
                    in source,
                    in definition,
                    profiles[i],
                    scale,
                    foundation,
                    wall,
                    accent);
                programLength += rewritten[i].Length;
            }

            FeatureCatalogue result = FeatureCatalogueBuilder.Allocate(
                definitions: source.Definitions.Length,
                rules: source.Rules.Length,
                parameters: source.Parameters.Length,
                anchors: source.Anchors.Length,
                slots: source.Slots.Length,
                programLength: programLength,
                materials: source.Materials.Length,
                explicitPlacements: source.ExplicitPlacements.Length,
                overrides: source.ParameterOverrides.Length,
                allocator);

            try
            {
                Copy(source.Parameters, result.Parameters);
                Copy(source.Anchors, result.Anchors);
                Copy(source.Slots, result.Slots);
                Copy(source.Materials, result.Materials);
                Copy(source.Rules, result.Rules);
                Copy(source.ExplicitPlacements, result.ExplicitPlacements);
                Copy(source.ParameterOverrides, result.ParameterOverrides);

                int offset = 0;
                for (int i = 0; i < source.Definitions.Length; i++)
                {
                    FeatureDefinition definition = source.Definitions[i];
                    int[] code = rewritten[i];
                    definition.ProgramOffset = offset;
                    definition.ProgramLength = code.Length;
                    result.Definitions[i] = definition;
                    for (int c = 0; c < code.Length; c++)
                        result.Program[offset + c] = code[c];
                    offset += code.Length;
                }

                CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref result);
                if (load != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException(
                        "Architecture geometry catalogue failed validation: " + load);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private static int[] RewriteProgram(
            in FeatureCatalogue source,
            in FeatureDefinition definition,
            StructureGeometryProfile profile,
            int scale,
            byte foundationMaterial,
            byte wallMaterial,
            byte accentMaterial)
        {
            if (!profile.HasRoundedGeometry || definition.ProgramLength == 0)
                return CopyProgram(in source, in definition);

            var code = new List<int>(definition.ProgramLength + 32);
            int cursor = definition.ProgramOffset;
            int end = cursor + definition.ProgramLength;
            while (cursor < end)
            {
                ShapeOp op = (ShapeOp)source.Program[cursor];
                int operands = ShapeOps.OperandCount(op);
                if (operands < 0)
                    throw new InvalidOperationException("Unknown shape opcode: " + op);
                int instructionLength = 2 + operands;
                if (cursor + instructionLength > end)
                    throw new InvalidOperationException(
                        "Shape instruction overruns its feature program.");

                int modeMask = source.Program[cursor + 1];
                if (op == ShapeOp.EmitBox && modeMask == 0)
                {
                    int operand = cursor + 2;
                    int sx = source.Program[operand + 3];
                    int sy = source.Program[operand + 4];
                    int sz = source.Program[operand + 5];
                    byte material = (byte)source.Program[operand + 6];
                    PrimitiveMode mode = (PrimitiveMode)source.Program[operand + 9];

                    int radiusDm = ResolveRadiusDm(
                        profile,
                        scale,
                        sx,
                        sz,
                        material,
                        mode,
                        foundationMaterial,
                        wallMaterial,
                        accentMaterial);
                    int radius = ClampRadius(radiusDm * scale, sx, sy, sz);
                    if (radius > 0)
                    {
                        EmitRoundedBox(
                            code,
                            source,
                            operand,
                            sx,
                            sy,
                            sz,
                            radius,
                            material,
                            mode);
                        cursor += instructionLength;
                        continue;
                    }
                }

                CopyInstruction(code, source, cursor, instructionLength);
                cursor += instructionLength;
            }

            return code.ToArray();
        }

        private static int ResolveRadiusDm(
            StructureGeometryProfile profile,
            int scale,
            int sx,
            int sz,
            byte material,
            PrimitiveMode mode,
            byte foundationMaterial,
            byte wallMaterial,
            byte accentMaterial)
        {
            if (mode == PrimitiveMode.Carve)
            {
                // Generated door/window cuts are thin in at least one horizontal dimension. The
                // broad shell-interior carve is intentionally left sharp here so this compatibility
                // pass cannot accidentally shrink usable room corners. A semantic shape builder can
                // remove this inference entirely once every catalogue emits explicit Opening ops.
                int openingLimit = MaxLikelyOpeningWidthDm * scale;
                return sx <= openingLimit || sz <= openingLimit
                    ? profile.OpeningCornerRadiusDm
                    : 0;
            }

            if (mode != PrimitiveMode.Fill && mode != PrimitiveMode.FillIfEmpty)
                return 0;

            if (material == foundationMaterial)
                return profile.FoundationCornerRadiusDm;
            if (material == wallMaterial || material == accentMaterial)
                return profile.ShellCornerRadiusDm;

            // Timber frames, glass infill, awnings and other small solids are architectural detail.
            // Radius clamping below naturally prevents over-rounding thin members.
            return profile.DetailCornerRadiusDm;
        }

        private static void EmitRoundedBox(
            List<int> code,
            NativeArray<int> source,
            int operand,
            int sx,
            int sy,
            int sz,
            int radius,
            byte material,
            PrimitiveMode mode)
        {
            code.Add((int)ShapeOp.EmitRoundedBox);
            code.Add(0);
            code.Add(source[operand + 0]);
            code.Add(source[operand + 1]);
            code.Add(source[operand + 2]);
            code.Add(sx);
            code.Add(sy);
            code.Add(sz);
            code.Add(radius);
            code.Add(material);
            code.Add(source[operand + 7]);
            code.Add(source[operand + 8]);
            code.Add((int)mode);
        }

        private static int[] CopyProgram(
            in FeatureCatalogue source,
            in FeatureDefinition definition)
        {
            var unchanged = new int[definition.ProgramLength];
            for (int i = 0; i < unchanged.Length; i++)
                unchanged[i] = source.Program[definition.ProgramOffset + i];
            return unchanged;
        }

        private static void CopyInstruction(
            List<int> target,
            NativeArray<int> source,
            int cursor,
            int instructionLength)
        {
            for (int i = 0; i < instructionLength; i++)
                target.Add(source[cursor + i]);
        }

        private static int ClampRadius(int requested, int sx, int sy, int sz)
        {
            if (requested <= 0 || sx <= 2 || sy <= 2 || sz <= 2) return 0;
            int minExtent = math.min(sx, math.min(sy, sz));
            return math.clamp(requested, 1, math.max(1, (minExtent - 1) / 2));
        }

        private static void Copy<T>(NativeArray<T> source, NativeArray<T> target)
            where T : struct
        {
            for (int i = 0; i < source.Length; i++)
                target[i] = source[i];
        }
    }
}
