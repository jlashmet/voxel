using System;
using MountingForce.WorldGen.Architecture;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Features.Emitters;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Concrete material/presentation inputs for generic generated-building realization.
    /// Architecture owns dimensions and opening semantics; this lower layer owns voxel materials.
    /// </summary>
    public readonly struct GeneratedBuildingVoxelStyle
    {
        public readonly byte FoundationMaterial;
        public readonly byte WallMaterial;
        public readonly byte WindowMaterial;
        public readonly ushort WallSurfaceStyle;
        public readonly byte WallCoating;
        public readonly int FoundationHeightVoxels;
        public readonly ArchFeatureStyle ArchStyle;
        public readonly byte RoofMaterial;

        public GeneratedBuildingVoxelStyle(
            byte foundationMaterial,
            byte wallMaterial,
            byte windowMaterial,
            ushort wallSurfaceStyle,
            byte wallCoating,
            int foundationHeightVoxels,
            ArchFeatureStyle archStyle,
            byte roofMaterial = 0)
        {
            FoundationMaterial = foundationMaterial;
            WallMaterial = wallMaterial;
            WindowMaterial = windowMaterial;
            WallSurfaceStyle = wallSurfaceStyle;
            WallCoating = wallCoating;
            FoundationHeightVoxels = foundationHeightVoxels;
            ArchStyle = archStyle;
            RoofMaterial = roofMaterial;
        }
    }

    /// <summary>
    /// Town-agnostic local-space backend for generated building compositions.
    ///
    /// StructureForm owns massing: storeys, overhangs, wings and roof form. BuildingCompositionForm
    /// owns facade openings. Arch sockets are lowered through <see cref="BuildingArchIntegration"/>
    /// and delegated to <see cref="ArchBayFeatureDefinition.Emit(int3, NativeList{Primitive}, ProfileBlockStore)"/>,
    /// preserving Core as the single source of arch geometry. Frontage is local -Z/positive-depth;
    /// world placement/orientation remains a caller concern.
    /// </summary>
    public static class GeneratedBuildingVoxelRealizer
    {
        public static bool EmitLocal(
            in BuildingCompositionForm composition,
            int3 origin,
            int voxelsPerDecimetre,
            int wallDepthVoxels,
            in GeneratedBuildingVoxelStyle style,
            uint seed,
            NativeList<Primitive> output,
            ProfileBlockStore profileBlocks = null)
        {
            if (!composition.Massing.IsGenerated)
                return false;
            if (voxelsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));
            if (wallDepthVoxels <= 0)
                throw new ArgumentOutOfRangeException(nameof(wallDepthVoxels));
            if (style.WallMaterial == 0)
                throw new ArgumentException("Generated building style requires a non-air wall material.", nameof(style));
            if (style.FoundationHeightVoxels < 0)
                throw new ArgumentOutOfRangeException(nameof(style), "Foundation height cannot be negative.");

            BuildingCompositionCompiler.Validate(composition);

            StructureForm massing = composition.Massing;
            int width = checked(massing.WidthDm * voxelsPerDecimetre);
            int depth = checked(massing.DepthDm * voxelsPerDecimetre);
            int storeyHeight = checked(composition.StoreyHeightDm * voxelsPerDecimetre);
            int overhang = checked(massing.UpperOverhangDm * voxelsPerDecimetre);
            if (width <= wallDepthVoxels * 2 || depth <= wallDepthVoxels * 2 || storeyHeight <= 0)
                throw new ArgumentException("Voxel scale is too coarse for the generated building shell.", nameof(voxelsPerDecimetre));

            int foundationHeight = style.FoundationHeightVoxels;
            int wallY = origin.y + foundationHeight;
            int order = output.Length;

            if (foundationHeight > 0 && style.FoundationMaterial != 0)
            {
                output.Add(BoxEmitter.Box(
                    origin,
                    new int3(width, foundationHeight, depth),
                    style.FoundationMaterial,
                    PrimitiveMode.Fill,
                    order++,
                    style.WallSurfaceStyle,
                    style.WallCoating));
            }

            // Ground storey stays on the authored frontage. Upper storeys may project left, right
            // and toward the street; this matches StructureForm's overhang semantic without the
            // settlement layer knowing anything about voxel shell construction.
            EmitShell(output,
                origin.x, wallY, origin.z,
                width, storeyHeight, depth,
                wallDepthVoxels, style,
                ref order);

            int upperX = origin.x - overhang;
            int upperZ = origin.z - overhang;
            int upperWidth = width + 2 * overhang;
            int upperDepth = depth + overhang;
            int upperHeight = checked(Math.Max(0, massing.Storeys - 1) * storeyHeight);
            if (upperHeight > 0)
            {
                EmitShell(output,
                    upperX, wallY + storeyHeight, upperZ,
                    upperWidth, upperHeight, upperDepth,
                    wallDepthVoxels, style,
                    ref order);
            }

            EmitWing(
                massing, origin, wallY,
                width, depth, storeyHeight,
                voxelsPerDecimetre, wallDepthVoxels,
                style, output, ref order);

            BuildingOpening[] openings = composition.Openings;
            for (int i = 0; i < openings.Length; i++)
            {
                BuildingOpening opening = openings[i];
                bool upper = opening.Storey > 0;
                int facadeX = upper ? upperX : origin.x;
                int facadeZ = upper ? upperZ : origin.z;
                int facadeWidth = upper ? upperWidth : width;
                int center = checked(opening.CenterOffsetDm * voxelsPerDecimetre);
                int openingWidth = checked(opening.WidthDm * voxelsPerDecimetre);
                int openingHeight = checked(opening.HeightDm * voxelsPerDecimetre);
                if (openingWidth <= 0 || openingHeight <= 0)
                    throw new ArgumentException("Voxel scale collapsed a building opening.", nameof(voxelsPerDecimetre));

                int openingX = facadeX + facadeWidth / 2 + center - openingWidth / 2;
                int openingY = wallY
                             + opening.Storey * storeyHeight
                             + checked(opening.SillHeightDm * voxelsPerDecimetre);
                output.Add(BoxEmitter.Box(
                    new int3(openingX, openingY, facadeZ),
                    new int3(openingWidth, openingHeight, wallDepthVoxels + 1),
                    0, PrimitiveMode.Carve, order++));

                if (opening.DetailSocket == BuildingDetailSocketKind.ArchBay)
                {
                    BuildingDetailRequest request = new BuildingDetailRequest(
                        opening.DetailSocket,
                        opening.Storey,
                        opening.Bay,
                        opening.CenterOffsetDm,
                        opening.Storey * composition.StoreyHeightDm + opening.SillHeightDm,
                        opening.WidthDm,
                        opening.HeightDm);
                    BuildingArchPlacement placement = BuildingArchIntegration.Compile(
                        request,
                        voxelsPerDecimetre,
                        wallDepthVoxels,
                        style.ArchStyle,
                        seed ^ DetailSeed(opening.Storey, opening.Bay));

                    int archX = facadeX + facadeWidth / 2
                              + placement.CenterOffsetVoxels
                              - placement.Definition.Width / 2;
                    int archY = wallY + placement.BaseHeightVoxels;
                    if (archX < facadeX || archX + placement.Definition.Width > facadeX + facadeWidth)
                        throw new InvalidOperationException("Compiled arch bay escaped the generated facade width.");

                    if (!placement.Definition.Emit(
                        new int3(archX, archY, facadeZ),
                        output,
                        profileBlocks))
                        throw new InvalidOperationException("Core arch emitter rejected a compiled building arch.");
                    order = output.Length;
                    continue;
                }

                if (opening.Kind == BuildingOpeningKind.Window && style.WindowMaterial != 0)
                {
                    output.Add(BoxEmitter.Box(
                        new int3(openingX, openingY, facadeZ),
                        new int3(openingWidth, openingHeight, wallDepthVoxels),
                        style.WindowMaterial, PrimitiveMode.Fill, order++,
                        style.WallSurfaceStyle));
                }
            }

            EmitMainRoof(
                massing,
                upperHeight > 0 ? upperX : origin.x,
                wallY + massing.Storeys * storeyHeight,
                upperHeight > 0 ? upperZ : origin.z,
                upperHeight > 0 ? upperWidth : width,
                upperHeight > 0 ? upperDepth : depth,
                voxelsPerDecimetre,
                style,
                output,
                ref order);

            return true;
        }

        private static void EmitShell(
            NativeList<Primitive> output,
            int x, int y, int z,
            int width, int height, int depth,
            int wallDepth,
            in GeneratedBuildingVoxelStyle style,
            ref int order)
        {
            if (width <= wallDepth * 2 || depth <= wallDepth * 2 || height <= 0)
                throw new InvalidOperationException("Generated building massing collapsed below shell thickness.");

            output.Add(BoxEmitter.Box(
                new int3(x, y, z),
                new int3(width, height, wallDepth),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));
            output.Add(BoxEmitter.Box(
                new int3(x, y, z + depth - wallDepth),
                new int3(width, height, wallDepth),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));
            output.Add(BoxEmitter.Box(
                new int3(x, y, z + wallDepth),
                new int3(wallDepth, height, depth - 2 * wallDepth),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));
            output.Add(BoxEmitter.Box(
                new int3(x + width - wallDepth, y, z + wallDepth),
                new int3(wallDepth, height, depth - 2 * wallDepth),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));
        }

        private static void EmitWing(
            StructureForm massing,
            int3 origin,
            int wallY,
            int width,
            int depth,
            int storeyHeight,
            int scale,
            int wallDepth,
            in GeneratedBuildingVoxelStyle style,
            NativeList<Primitive> output,
            ref int order)
        {
            if (massing.Footprint != FootprintForm.RearWing
                && massing.Footprint != FootprintForm.SideWing)
                return;

            int wingWidth = checked(massing.WingWidthDm * scale);
            int wingDepth = checked(massing.WingDepthDm * scale);
            if (wingWidth <= wallDepth * 2 || wingDepth <= wallDepth * 2)
                throw new InvalidOperationException("Generated wing is too small for the configured wall depth.");

            int overlap = wallDepth;
            int wingX;
            int wingZ;
            if (massing.Footprint == FootprintForm.RearWing)
            {
                wingX = massing.WingOnRight
                    ? origin.x + width - wingWidth - wallDepth
                    : origin.x + wallDepth;
                wingZ = origin.z + depth - overlap;
            }
            else
            {
                wingX = massing.WingOnRight
                    ? origin.x + width - overlap
                    : origin.x - wingWidth + overlap;
                wingZ = origin.z + depth / 2 - wingDepth / 2;
            }

            if (style.FoundationHeightVoxels > 0 && style.FoundationMaterial != 0)
            {
                output.Add(BoxEmitter.Box(
                    new int3(wingX, origin.y, wingZ),
                    new int3(wingWidth, style.FoundationHeightVoxels, wingDepth),
                    style.FoundationMaterial, PrimitiveMode.Fill, order++,
                    style.WallSurfaceStyle, style.WallCoating));
            }

            EmitShell(output,
                wingX, wallY, wingZ,
                wingWidth, storeyHeight, wingDepth,
                wallDepth, style,
                ref order);

            byte roofMaterial = style.RoofMaterial != 0 ? style.RoofMaterial : style.WallMaterial;
            int wingRoofHeight = Math.Max(4, massing.RoofHeightDm * scale * 2 / 3);
            PrismProfile profile = massing.Roof == RoofForm.GableWithLeanTo
                ? PrismProfile.Shed
                : PrismProfile.Gable;
            output.Add(PrismEmitter.Prism(
                new int3(wingX, wallY + storeyHeight, wingZ),
                new int3(wingWidth, wingRoofHeight, wingDepth),
                profile,
                roofMaterial,
                PrimitiveMode.Fill,
                order++,
                style.WallSurfaceStyle,
                style.WallCoating));
        }

        private static void EmitMainRoof(
            StructureForm massing,
            int x,
            int y,
            int z,
            int width,
            int depth,
            int scale,
            in GeneratedBuildingVoxelStyle style,
            NativeList<Primitive> output,
            ref int order)
        {
            int roofHeight = checked(massing.RoofHeightDm * scale);
            if (roofHeight <= 0) return;
            byte roofMaterial = style.RoofMaterial != 0 ? style.RoofMaterial : style.WallMaterial;

            if (massing.Roof != RoofForm.TwinGable)
            {
                output.Add(PrismEmitter.Prism(
                    new int3(x, y, z),
                    new int3(width, roofHeight, depth),
                    PrismProfile.Gable,
                    roofMaterial,
                    PrimitiveMode.Fill,
                    order++,
                    style.WallSurfaceStyle,
                    style.WallCoating));
                return;
            }

            int overlap = Math.Max(1, scale * 2);
            int halfWidth = width / 2 + overlap;
            output.Add(PrismEmitter.Prism(
                new int3(x, y, z),
                new int3(halfWidth, roofHeight, depth),
                PrismProfile.Gable,
                roofMaterial,
                PrimitiveMode.Fill,
                order++,
                style.WallSurfaceStyle,
                style.WallCoating));
            output.Add(PrismEmitter.Prism(
                new int3(x + width / 2 - overlap, y, z),
                new int3(halfWidth, roofHeight, depth),
                PrismProfile.Gable,
                roofMaterial,
                PrimitiveMode.Fill,
                order++,
                style.WallSurfaceStyle,
                style.WallCoating));
        }

        private static uint DetailSeed(int storey, int bay)
        {
            uint h = (uint)(storey + 1) * 0x9E3779B9u
                   ^ (uint)(bay + 1) * 0x85EBCA6Bu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return h;
        }
    }
}
