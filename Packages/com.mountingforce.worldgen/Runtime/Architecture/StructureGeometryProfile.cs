using System;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Renderer-independent low-level geometry controls for one realised structure.
    ///
    /// Settlement planning deliberately does not own these values. A city chooses plots, roles,
    /// districts and a style id; an architecture style resolver chooses the local massing; this
    /// profile controls how that massing is finally shaped. Keeping the profile independent of the
    /// voxel backend means the same city grammar can be realised by voxels, meshes, SDFs or editor
    /// previews without baking engine-specific surface ids into settlement content.
    /// </summary>
    public readonly struct StructureGeometryProfile
    {
        /// <summary>Radius applied to foundation/coursing solids, in decimetres.</summary>
        public readonly int FoundationCornerRadiusDm;

        /// <summary>Radius applied to primary wall/shell solids, in decimetres.</summary>
        public readonly int ShellCornerRadiusDm;

        /// <summary>
        /// Radius applied to door/window/opening cuts, in decimetres. This is intentionally separate
        /// from shell rounding: a city can keep a heavy masonry mass while using softer reveals, or
        /// vice versa.
        /// </summary>
        public readonly int OpeningCornerRadiusDm;

        /// <summary>
        /// Radius available to a backend for smaller architectural solids such as piers, trims,
        /// frames, awnings and chimneys.
        /// </summary>
        public readonly int DetailCornerRadiusDm;

        public StructureGeometryProfile(
            int foundationCornerRadiusDm,
            int shellCornerRadiusDm,
            int openingCornerRadiusDm,
            int detailCornerRadiusDm)
        {
            if (foundationCornerRadiusDm < 0)
                throw new ArgumentOutOfRangeException(nameof(foundationCornerRadiusDm));
            if (shellCornerRadiusDm < 0)
                throw new ArgumentOutOfRangeException(nameof(shellCornerRadiusDm));
            if (openingCornerRadiusDm < 0)
                throw new ArgumentOutOfRangeException(nameof(openingCornerRadiusDm));
            if (detailCornerRadiusDm < 0)
                throw new ArgumentOutOfRangeException(nameof(detailCornerRadiusDm));

            FoundationCornerRadiusDm = foundationCornerRadiusDm;
            ShellCornerRadiusDm = shellCornerRadiusDm;
            OpeningCornerRadiusDm = openingCornerRadiusDm;
            DetailCornerRadiusDm = detailCornerRadiusDm;
        }

        public bool HasRoundedGeometry =>
            FoundationCornerRadiusDm > 0
            || ShellCornerRadiusDm > 0
            || OpeningCornerRadiusDm > 0
            || DetailCornerRadiusDm > 0;

        public static StructureGeometryProfile Sharp =>
            new StructureGeometryProfile(0, 0, 0, 0);
    }

    /// <summary>
    /// Pluggable architecture policy for low-level geometry. A future city can provide its own
    /// resolver without changing the voxel evaluator or the settlement-planning model.
    /// </summary>
    public interface IStructureGeometryProfileResolver
    {
        StructureGeometryProfile Resolve(StructureIntent intent, StructureForm form);
    }

    /// <summary>
    /// Reusable conventional-settlement defaults. These are intentionally based on semantic district
    /// and archetype information rather than Kentridge role ids, so another town can use them as-is
    /// or replace them with a style-specific resolver.
    /// </summary>
    public sealed class HumanSettlementGeometryProfileResolver : IStructureGeometryProfileResolver
    {
        public static readonly HumanSettlementGeometryProfileResolver Instance =
            new HumanSettlementGeometryProfileResolver();

        private HumanSettlementGeometryProfileResolver()
        {
        }

        public StructureGeometryProfile Resolve(StructureIntent intent, StructureForm form)
        {
            int shellRadius;
            switch (intent.District)
            {
                case DistrictKind.Civic:
                case DistrictKind.Noble:
                    shellRadius = 4;
                    break;
                case DistrictKind.Market:
                    shellRadius = 3;
                    break;
                default:
                    shellRadius = 2;
                    break;
            }

            if (intent.Archetype == StructureArchetype.Warehouse)
                shellRadius = Math.Min(shellRadius, 2);
            else if (intent.Archetype == StructureArchetype.Well)
                shellRadius = 1;

            int foundationRadius = Math.Max(1, shellRadius - 1);
            int openingRadius = shellRadius >= 3 ? 2 : 1;
            int detailRadius = shellRadius >= 3 ? 2 : 1;
            return new StructureGeometryProfile(
                foundationRadius,
                shellRadius,
                openingRadius,
                detailRadius);
        }
    }
}
