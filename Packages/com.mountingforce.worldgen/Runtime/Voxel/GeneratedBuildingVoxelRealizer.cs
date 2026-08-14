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

        public GeneratedBuildingVoxelStyle(
            byte foundationMaterial,
            byte wallMaterial,
            byte windowMaterial,
            ushort wallSurfaceStyle,
            byte wallCoating,
            int foundationHeightVoxels,
            ArchFeatureStyle archStyle)
        {
            FoundationMaterial = foundationMaterial;
            WallMaterial = wallMaterial;
            WindowMaterial = windowMaterial;
            WallSurfaceStyle = wallSurfaceStyle;
            WallCoating = wallCoating;
            FoundationHeightVoxels = foundationHeightVoxels;
            ArchStyle = archStyle;
        }
    }

    /// <summary>
    /// Town-agnostic local-space backend for generated building compositions.
    ///
    /// The shell and ordinary openings are emitted here. Arch sockets are never approximated by
    /// boxes: they are lowered through <see cref="BuildingArchIntegration"/> and then delegated to
    /// <see cref="ArchBayFeatureDefinition.Emit(int3, NativeList{Primitive}, ProfileBlockStore)"/>,
    /// preserving the existing Core arch implementation as the single source of arch geometry.
    /// Frontage is local -Z/positive-depth; world placement/orientation remains a caller concern.
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

            int width = checked(composition.Massing.WidthDm * voxelsPerDecimetre);
            int depth = checked(composition.Massing.DepthDm * voxelsPerDecimetre);
            int storeyHeight = checked(composition.StoreyHeightDm * voxelsPerDecimetre);
            if (width <= wallDepthVoxels * 2 || depth <= wallDepthVoxels * 2 || storeyHeight <= 0)
                throw new ArgumentException("Voxel scale is too coarse for the generated building shell.", nameof(voxelsPerDecimetre));

            int wallHeight = checked(storeyHeight * composition.Massing.Storeys);
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

            // Four independent wall runs avoid filling the interior only to carve it again. The
            // front wall occupies local z=0..wallDepth-1; building depth grows toward +z.
            output.Add(BoxEmitter.Box(
                new int3(origin.x, wallY, origin.z),
                new int3(width, wallHeight, wallDepthVoxels),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));
            output.Add(BoxEmitter.Box(
                new int3(origin.x, wallY, origin.z + depth - wallDepthVoxels),
                new int3(width, wallHeight, wallDepthVoxels),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));
            output.Add(BoxEmitter.Box(
                new int3(origin.x, wallY, origin.z + wallDepthVoxels),
                new int3(wallDepthVoxels, wallHeight, depth - 2 * wallDepthVoxels),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));
            output.Add(BoxEmitter.Box(
                new int3(origin.x + width - wallDepthVoxels, wallY, origin.z + wallDepthVoxels),
                new int3(wallDepthVoxels, wallHeight, depth - 2 * wallDepthVoxels),
                style.WallMaterial, PrimitiveMode.Fill, order++,
                style.WallSurfaceStyle, style.WallCoating));

            BuildingOpening[] openings = composition.Openings;
            for (int i = 0; i < openings.Length; i++)
            {
                BuildingOpening opening = openings[i];
                int center = checked(opening.CenterOffsetDm * voxelsPerDecimetre);
                int openingWidth = checked(opening.WidthDm * voxelsPerDecimetre);
                int openingHeight = checked(opening.HeightDm * voxelsPerDecimetre);
                if (openingWidth <= 0 || openingHeight <= 0)
                    throw new ArgumentException("Voxel scale collapsed a building opening.", nameof(voxelsPerDecimetre));

                int openingX = origin.x + width / 2 + center - openingWidth / 2;
                int openingY = wallY
                             + opening.Storey * storeyHeight
                             + checked(opening.SillHeightDm * voxelsPerDecimetre);
                output.Add(BoxEmitter.Box(
                    new int3(openingX, openingY, origin.z),
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

                    int archX = origin.x + width / 2
                              + placement.CenterOffsetVoxels
                              - placement.Definition.Width / 2;
                    int archY = wallY + placement.BaseHeightVoxels;
                    int archZ = origin.z;
                    if (archX < origin.x || archX + placement.Definition.Width > origin.x + width)
                        throw new InvalidOperationException("Compiled arch bay escaped the generated facade width.");

                    if (!placement.Definition.Emit(
                        new int3(archX, archY, archZ),
                        output,
                        profileBlocks))
                        throw new InvalidOperationException("Core arch emitter rejected a compiled building arch.");
                    order = output.Length;
                    continue;
                }

                if (opening.Kind == BuildingOpeningKind.Window && style.WindowMaterial != 0)
                {
                    output.Add(BoxEmitter.Box(
                        new int3(openingX, openingY, origin.z),
                        new int3(openingWidth, openingHeight, wallDepthVoxels),
                        style.WindowMaterial, PrimitiveMode.Fill, order++,
                        style.WallSurfaceStyle));
                }
            }

            return true;
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
