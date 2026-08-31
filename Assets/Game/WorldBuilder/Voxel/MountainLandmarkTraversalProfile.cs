using System;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Physical traversal requirements supplied by the composition that owns the player/motor.
    /// WorldBuilder consumes only these semantic measurements and derives deterministic voxel-space
    /// clearance from them; it does not know which scene, character implementation, or voxel scale
    /// produced the values.
    /// </summary>
    public readonly struct MountainLandmarkTraversalProfile
    {
        public int VoxelSizeMillimetres { get; }
        public int BodyHeightMillimetres { get; }
        public int BodyRadiusMillimetres { get; }
        public int OverheadMarginMillimetres { get; }
        public int LateralMarginMillimetres { get; }
        public int MaximumGradePercent { get; }

        public MountainLandmarkTraversalProfile(
            int voxelSizeMillimetres,
            int bodyHeightMillimetres,
            int bodyRadiusMillimetres,
            int overheadMarginMillimetres,
            int lateralMarginMillimetres,
            int maximumGradePercent)
        {
            if (voxelSizeMillimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelSizeMillimetres));
            if (bodyHeightMillimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(bodyHeightMillimetres));
            if (bodyRadiusMillimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(bodyRadiusMillimetres));
            if (overheadMarginMillimetres < 0)
                throw new ArgumentOutOfRangeException(nameof(overheadMarginMillimetres));
            if (lateralMarginMillimetres < 0)
                throw new ArgumentOutOfRangeException(nameof(lateralMarginMillimetres));
            if (maximumGradePercent <= 0 || maximumGradePercent > 100)
                throw new ArgumentOutOfRangeException(nameof(maximumGradePercent));

            VoxelSizeMillimetres = voxelSizeMillimetres;
            BodyHeightMillimetres = bodyHeightMillimetres;
            BodyRadiusMillimetres = bodyRadiusMillimetres;
            OverheadMarginMillimetres = overheadMarginMillimetres;
            LateralMarginMillimetres = lateralMarginMillimetres;
            MaximumGradePercent = maximumGradePercent;
        }

        public int HeadroomVoxels => DivideRoundUp(
            BodyHeightMillimetres + OverheadMarginMillimetres,
            VoxelSizeMillimetres);

        public int ClearanceWidthVoxels => DivideRoundUp(
            BodyRadiusMillimetres * 2 + LateralMarginMillimetres * 2,
            VoxelSizeMillimetres);

        public bool SupportsRamp(int horizontalAdvanceVoxels, int riseVoxels)
        {
            if (horizontalAdvanceVoxels <= 0 || riseVoxels <= 0) return false;
            return (long)riseVoxels * 100L
                <= (long)horizontalAdvanceVoxels * MaximumGradePercent;
        }

        private static int DivideRoundUp(int value, int divisor) =>
            (value + divisor - 1) / divisor;
    }
}
