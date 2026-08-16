using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Backend-level shape bytecode builder that understands architectural geometry roles.
    ///
    /// City grammars call FoundationBox/ShellBox/OpeningCarve/DetailBox instead of manually choosing
    /// EmitBox versus EmitRoundedBox or engine surface-style ids. That makes low-level shape and
    /// reconstruction policy explicit and reusable while settlement/architecture assemblies remain
    /// renderer independent.
    /// </summary>
    public sealed class ArchitectureShapeProgramBuilder
    {
        private readonly List<int> _code = new List<int>();
        private readonly StructureGeometryProfile _profile;
        private readonly int _scale;

        public ArchitectureShapeProgramBuilder(
            StructureGeometryProfile profile,
            int voxelsPerDecimetre)
        {
            if (voxelsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));
            _profile = profile;
            _scale = voxelsPerDecimetre;
        }

        public void FoundationBox(
            int x, int y, int z,
            int sx, int sy, int sz,
            byte material,
            PrimitiveMode mode = PrimitiveMode.Fill) =>
            SemanticBox(
                x, y, z, sx, sy, sz, material, mode,
                _profile.FoundationCornerRadiusDm,
                _profile.FoundationSurface);

        public void ShellBox(
            int x, int y, int z,
            int sx, int sy, int sz,
            byte material,
            PrimitiveMode mode = PrimitiveMode.Fill) =>
            SemanticBox(
                x, y, z, sx, sy, sz, material, mode,
                _profile.ShellCornerRadiusDm,
                _profile.ShellSurface);

        public void OpeningCarve(
            int x, int y, int z,
            int sx, int sy, int sz) =>
            SemanticBox(
                x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve,
                _profile.OpeningCornerRadiusDm,
                _profile.OpeningSurface);

        public void DetailBox(
            int x, int y, int z,
            int sx, int sy, int sz,
            byte material,
            PrimitiveMode mode = PrimitiveMode.Fill) =>
            SemanticBox(
                x, y, z, sx, sy, sz, material, mode,
                _profile.DetailCornerRadiusDm,
                _profile.DetailSurface);

        /// <summary>
        /// Broad interior clearance is deliberately sharp by default. It is spatial subtraction,
        /// not a visible architectural opening, and rounding it can reduce guaranteed walkable room
        /// corners. Door/window apertures should use <see cref="OpeningCarve"/> instead.
        /// </summary>
        public void InteriorCarve(
            int x, int y, int z,
            int sx, int sy, int sz)
        {
            RawBox(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);
        }

        public void RawBox(
            int x, int y, int z,
            int sx, int sy, int sz,
            byte material,
            PrimitiveMode mode = PrimitiveMode.Fill,
            ushort surfaceStyle = SurfaceStyles.MaterialDefault,
            byte coating = Coatings.None)
        {
            if (sx <= 0 || sy <= 0 || sz <= 0) return;
            Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                material, surfaceStyle, coating, (int)mode);
        }

        /// <summary>
        /// Emits a roof/massing prism. Unless a treatment is supplied explicitly, roof
        /// reconstruction comes from the structure geometry profile so roofs participate in the
        /// same city-independent style policy as walls, openings and details.
        /// </summary>
        public void Prism(
            int x, int y, int z,
            int sx, int sy, int sz,
            PrismProfile profile,
            byte material,
            StructureSurfaceTreatment? surface = null)
        {
            if (sx <= 0 || sy <= 0 || sz <= 0) return;
            StructureSurfaceTreatment treatment = surface ?? _profile.RoofSurface;
            Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                (int)profile,
                material,
                ArchitectureVoxelSurfaceStyle.Map(treatment, SurfaceStyles.MaterialDefault),
                Coatings.None,
                (int)PrimitiveMode.Fill);
        }

        public void Anchor(int index, int3 position, Facing facing)
        {
            Op(ShapeOp.SetAnchor,
                index, position.x, position.y, position.z, (int)facing);
        }

        public int[] Finish()
        {
            Op(ShapeOp.End);
            return _code.ToArray();
        }

        private void SemanticBox(
            int x, int y, int z,
            int sx, int sy, int sz,
            byte material,
            PrimitiveMode mode,
            int radiusDm,
            StructureSurfaceTreatment surface)
        {
            if (sx <= 0 || sy <= 0 || sz <= 0) return;

            ushort style = ArchitectureVoxelSurfaceStyle.Map(
                surface, SurfaceStyles.MaterialDefault);
            int radius = ClampRadius(radiusDm * _scale, sx, sy, sz);
            if (radius <= 0)
            {
                RawBox(x, y, z, sx, sy, sz, material, mode, style);
                return;
            }

            Op(ShapeOp.EmitRoundedBox,
                x, y, z,
                sx, sy, sz,
                radius,
                material,
                style,
                Coatings.None,
                (int)mode);
        }

        private static int ClampRadius(int requested, int sx, int sy, int sz)
        {
            if (requested <= 0 || sx <= 2 || sy <= 2 || sz <= 2) return 0;
            int minExtent = math.min(sx, math.min(sy, sz));
            return math.clamp(requested, 1, math.max(1, (minExtent - 1) / 2));
        }

        private void Op(ShapeOp op, params int[] operands)
        {
            _code.Add((int)op);
            _code.Add(0);
            _code.AddRange(operands);
        }
    }

    internal static class ArchitectureVoxelSurfaceStyle
    {
        internal static ushort Map(
            StructureSurfaceTreatment treatment,
            ushort fallback)
        {
            switch (treatment)
            {
                case StructureSurfaceTreatment.Smooth: return SurfaceStyles.Smooth;
                case StructureSurfaceTreatment.Rounded: return SurfaceStyles.Rounded;
                case StructureSurfaceTreatment.Planar: return SurfaceStyles.Planar;
                case StructureSurfaceTreatment.Sharp: return SurfaceStyles.Sharp;
                case StructureSurfaceTreatment.Beveled: return SurfaceStyles.Beveled;
                case StructureSurfaceTreatment.MasonryJoint: return SurfaceStyles.MasonryJoint;
                default: return fallback;
            }
        }
    }
}
