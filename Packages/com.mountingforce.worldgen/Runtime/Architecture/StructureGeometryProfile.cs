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
        /// <summary>Radius applied to foundation/coursing box primitives, in decimetres.</summary>
        public readonly int FoundationCornerRadiusDm;

        /// <summary>Radius applied to primary wall/shell box primitives, in decimetres.</summary>
        public readonly int ShellCornerRadiusDm;

        /// <summary>
        /// Radius available to a backend for smaller architectural solids such as piers, trims and
        /// chimneys. The first voxel realiser intentionally keeps these sharp unless it can identify
        /// them semantically; the field is part of the stable contract for more granular backends.
        /// </summary>
        public readonly int DetailCornerRadiusDm;

        public StructureGeometryProfile(
            int foundationCornerRadiusDm,
            int shellCornerRadiusDm,
            int detailCornerRadiusDm)
        {
            if (foundationCornerRadiusDm < 0)
                throw new ArgumentOutOfRangeException(nameof(foundationCornerRadiusDm));
            if (shellCornerRadiusDm < 0)
                throw new ArgumentOutOfRangeException(nameof(shellCornerRadiusDm));
            if (detailCornerRadiusDm < 0)
                throw new ArgumentOutOfRangeException(nameof(detailCornerRadiusDm));

            FoundationCornerRadiusDm = foundationCornerRadiusDm;
            ShellCornerRadiusDm = shellCornerRadiusDm;
            DetailCornerRadiusDm = detailCornerRadiusDm;
        }

        public bool HasRoundedGeometry =>
            FoundationCornerRadiusDm > 0
            || ShellCornerRadiusDm > 0
            || DetailCornerRadiusDm > 0;

        public static StructureGeometryProfile Sharp =>
            new StructureGeometryProfile(0, 0, 0);
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
            int detailRadius = shellRadius >= 3 ? 2 : 1;
            return new StructureGeometryProfile(
                foundationRadius,
                shellRadius,
                detailRadius);
        }
    }
}
