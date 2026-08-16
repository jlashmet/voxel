using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Generic voxel realisation pass for renderer-independent architecture geometry profiles.
    ///
    /// The input catalogue remains responsible for semantic composition (shell, openings, trim,
    /// roofs, anchors). This pass changes only the primitive used to realise recognised massing
    /// materials. That separation lets other cities reuse the same low-level geometry controls
    /// without copying Kentridge's layout or role catalogue.
    /// </summary>
    public static class ArchitectureGeometryCatalogue
    {
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
            {
                var unchanged = new int[definition.ProgramLength];
                for (int i = 0; i < unchanged.Length; i++)
                    unchanged[i] = source.Program[definition.ProgramOffset + i];
                return unchanged;
            }

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

                    int radiusDm = 0;
                    if (mode == PrimitiveMode.Fill)
                    {
                        if (material == foundationMaterial)
                            radiusDm = profile.FoundationCornerRadiusDm;
                        else if (material == wallMaterial || material == accentMaterial)
                            radiusDm = profile.ShellCornerRadiusDm;
                    }

                    int radius = ClampRadius(radiusDm * scale, sx, sy, sz);
                    if (radius > 0)
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
                        code.Add(source.Program[operand + 7]);
                        code.Add(source.Program[operand + 8]);
                        code.Add((int)mode);
                        cursor += instructionLength;
                        continue;
                    }
                }

                for (int i = 0; i < instructionLength; i++)
                    code.Add(source.Program[cursor + i]);
                cursor += instructionLength;
            }

            return code.ToArray();
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

    /// <summary>
    /// Kentridge's thin composition adapter: resolve the same semantic structure forms already used
    /// by gameplay, choose a generic geometry profile for each role, then feed those profiles through
    /// the reusable voxel realiser.
    /// </summary>
    internal static class KentridgeSmoothedGrammarVoxelCatalogue
    {
        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            FeatureCatalogue source = KentridgeGrammarVoxelCatalogue.Build(
                seed, settings, Allocator.Temp);
            try
            {
                SettlementPlan plan = KentridgeDefinition.Build(seed);
                var profiles = new StructureGeometryProfile[source.Definitions.Length];
                IStructureGeometryProfileResolver resolver =
                    HumanSettlementGeometryProfileResolver.Instance;

                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if ((uint)plot.RoleId >= (uint)profiles.Length)
                        throw new InvalidOperationException(
                            "Kentridge role is outside the grammar catalogue: " + plot.RoleId);
                    StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                    StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, seed);
                    profiles[plot.RoleId] = resolver.Resolve(intent, form);
                }

                return ArchitectureGeometryCatalogue.Apply(
                    in source,
                    plan.Theme,
                    settings,
                    profiles,
                    allocator);
            }
            finally
            {
                source.Dispose();
            }
        }
    }
}
