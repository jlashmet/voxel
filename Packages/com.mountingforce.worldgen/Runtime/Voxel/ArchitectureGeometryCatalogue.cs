using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Generic voxel realisation pass for renderer-independent architecture geometry profiles.
    ///
    /// This pass is deliberately city-independent. The input catalogue still owns semantic
    /// composition (shells, openings, trim, roofs and anchors), while the profile controls how its
    /// primitives are realised and reconstructed. It is also a migration bridge for older
    /// catalogues whose shape programs do not yet tag architectural operations explicitly: once
    /// those catalogues emit semantic operations directly, they can use the same profile without
    /// this material/dimension inference.
    /// </summary>
    public static class ArchitectureGeometryCatalogue
    {
        private const int MaxLikelyOpeningWidthDm = 20;

        private enum GeometryRole : byte
        {
            None,
            Foundation,
            Shell,
            Opening,
            Detail,
        }

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
            int foundationHeight = theme.FoundationHeightDm * scale;

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
                    foundationHeight,
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
            int foundationHeight,
            byte foundationMaterial,
            byte wallMaterial,
            byte accentMaterial)
        {
            if (!profile.RequiresRealization || definition.ProgramLength == 0)
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
                    ushort existingSurface = (ushort)source.Program[operand + 7];
                    PrimitiveMode mode = (PrimitiveMode)source.Program[operand + 9];

                    GeometryRole role = ResolveRole(
                        scale,
                        foundationHeight,
                        sx,
                        sy,
                        sz,
                        material,
                        mode,
                        foundationMaterial,
                        wallMaterial,
                        accentMaterial);
                    int radiusDm = ResolveRadiusDm(profile, role);
                    ushort surface = ResolveSurfaceStyle(profile, role, existingSurface);
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
                            surface,
                            mode);
                        cursor += instructionLength;
                        continue;
                    }

                    if (surface != existingSurface)
                    {
                        CopyInstructionWithSurface(
                            code,
                            source,
                            cursor,
                            instructionLength,
                            surfaceOperandIndex: 7,
                            surface);
                        cursor += instructionLength;
                        continue;
                    }
                }

                if (op == ShapeOp.EmitPrism && modeMask == 0)
                {
                    const int surfaceOperandIndex = 8;
                    int surfaceIndex = cursor + 2 + surfaceOperandIndex;
                    ushort existingSurface = (ushort)source.Program[surfaceIndex];
                    ushort surface = ResolveSurfaceTreatment(
                        profile.RoofSurface,
                        existingSurface);
                    if (surface != existingSurface)
                    {
                        CopyInstructionWithSurface(
                            code,
                            source,
                            cursor,
                            instructionLength,
                            surfaceOperandIndex,
                            surface);
                        cursor += instructionLength;
                        continue;
                    }
                }

                CopyInstruction(code, source.Program, cursor, instructionLength);
                cursor += instructionLength;
            }

            return code.ToArray();
        }

        private static GeometryRole ResolveRole(
            int scale,
            int foundationHeight,
            int sx,
            int sy,
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
                // broad shell-interior carve is intentionally left untouched so this compatibility
                // pass cannot shrink usable room corners.
                int openingLimit = MaxLikelyOpeningWidthDm * scale;
                return sx <= openingLimit || sz <= openingLimit
                    ? GeometryRole.Opening
                    : GeometryRole.None;
            }

            if (mode != PrimitiveMode.Fill && mode != PrimitiveMode.FillIfEmpty)
                return GeometryRole.None;

            bool foundation = material == foundationMaterial;
            bool shell = material == wallMaterial || material == accentMaterial;

            // Material maps are allowed to alias semantic roles. Kentridge's showcase map, for
            // example, can map foundation stone and wall masonry to one palette slot. In that case
            // use the authored foundation height to preserve independent foundation/shell controls.
            if (foundation && shell)
                return sy <= math.max(scale, foundationHeight * 2)
                    ? GeometryRole.Foundation
                    : GeometryRole.Shell;
            if (foundation) return GeometryRole.Foundation;
            if (shell) return GeometryRole.Shell;

            return GeometryRole.Detail;
        }

        private static int ResolveRadiusDm(
            StructureGeometryProfile profile,
            GeometryRole role)
        {
            switch (role)
            {
                case GeometryRole.Foundation: return profile.FoundationCornerRadiusDm;
                case GeometryRole.Shell: return profile.ShellCornerRadiusDm;
                case GeometryRole.Opening: return profile.OpeningCornerRadiusDm;
                case GeometryRole.Detail: return profile.DetailCornerRadiusDm;
                default: return 0;
            }
        }

        private static ushort ResolveSurfaceStyle(
            StructureGeometryProfile profile,
            GeometryRole role,
            ushort existingSurface)
        {
            StructureSurfaceTreatment treatment;
            switch (role)
            {
                case GeometryRole.Foundation:
                    treatment = profile.FoundationSurface;
                    break;
                case GeometryRole.Shell:
                    treatment = profile.ShellSurface;
                    break;
                case GeometryRole.Opening:
                    treatment = profile.OpeningSurface;
                    break;
                case GeometryRole.Detail:
                    treatment = profile.DetailSurface;
                    break;
                default:
                    return existingSurface;
            }

            return ResolveSurfaceTreatment(treatment, existingSurface);
        }

        private static ushort ResolveSurfaceTreatment(
            StructureSurfaceTreatment treatment,
            ushort existingSurface)
        {
            switch (treatment)
            {
                case StructureSurfaceTreatment.Smooth: return SurfaceStyles.Smooth;
                case StructureSurfaceTreatment.Rounded: return SurfaceStyles.Rounded;
                case StructureSurfaceTreatment.Planar: return SurfaceStyles.Planar;
                case StructureSurfaceTreatment.Sharp: return SurfaceStyles.Sharp;
                case StructureSurfaceTreatment.Beveled: return SurfaceStyles.Beveled;
                case StructureSurfaceTreatment.MasonryJoint: return SurfaceStyles.MasonryJoint;
                default: return existingSurface;
            }
        }

        private static void EmitRoundedBox(
            List<int> code,
            FeatureCatalogue source,
            int operand,
            int sx,
            int sy,
            int sz,
            int radius,
            byte material,
            ushort surface,
            PrimitiveMode mode)
        {
            code.Add((int)ShapeOp.EmitRoundedBox);
            code.Add(0);
            code.Add(source.Program[operand + 0]);
            code.Add(source.Program[operand + 1]);
            code.Add(source.Program[operand + 2]);
            code.Add(sx);
            code.Add(sy);
            code.Add(sz);
            code.Add(radius);
            code.Add(material);
            code.Add(surface);
            code.Add(source.Program[operand + 8]);
            code.Add((int)mode);
        }

        private static void CopyInstructionWithSurface(
            List<int> target,
            FeatureCatalogue source,
            int cursor,
            int instructionLength,
            int surfaceOperandIndex,
            ushort surface)
        {
            int surfaceIndex = cursor + 2 + surfaceOperandIndex;
            for (int i = 0; i < instructionLength; i++)
            {
                int sourceIndex = cursor + i;
                target.Add(sourceIndex == surfaceIndex ? surface : source.Program[sourceIndex]);
            }
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
