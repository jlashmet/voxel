using System;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Semantic presentation roles for a mountain surface. These deliberately do not identify
    /// voxel materials; composition owns the concrete palette used to render each role.
    /// </summary>
    public enum MountainSurfaceRole : byte
    {
        GroundCover = 0,
        Rock = 1,
        Snow = 2,
    }

    /// <summary>
    /// Reusable altitude/slope policy for mountain presentation. Altitude is normalized over the
    /// generated landform relief (0 = base, 1000 = highest generated point). Slope is rise/run in
    /// permille. Steep faces remain exposed rock regardless of altitude, which keeps snow and
    /// ground cover on surfaces that can plausibly retain them without coupling climate to shape.
    /// </summary>
    public sealed class MountainClimateProfile
    {
        public int GroundCoverCeilingPermille { get; }
        public int SnowLinePermille { get; }
        public int SteepRockSlopePermille { get; }

        public MountainClimateProfile(
            int groundCoverCeilingPermille,
            int snowLinePermille,
            int steepRockSlopePermille)
        {
            if (groundCoverCeilingPermille < 0 || groundCoverCeilingPermille >= 1000)
                throw new ArgumentOutOfRangeException(nameof(groundCoverCeilingPermille));
            if (snowLinePermille < 1 || snowLinePermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(snowLinePermille));
            if (groundCoverCeilingPermille >= snowLinePermille)
                throw new ArgumentException("Ground-cover ceiling must be below the snow line.", nameof(snowLinePermille));
            if (steepRockSlopePermille < 1 || steepRockSlopePermille > 10000)
                throw new ArgumentOutOfRangeException(nameof(steepRockSlopePermille));

            GroundCoverCeilingPermille = groundCoverCeilingPermille;
            SnowLinePermille = snowLinePermille;
            SteepRockSlopePermille = steepRockSlopePermille;
        }

        public MountainSurfaceRole RoleAt(int altitudePermille, int slopePermille)
        {
            if (altitudePermille < 0 || altitudePermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(altitudePermille));
            if (slopePermille < 0 || slopePermille > 10000)
                throw new ArgumentOutOfRangeException(nameof(slopePermille));

            if (slopePermille >= SteepRockSlopePermille)
                return MountainSurfaceRole.Rock;
            if (altitudePermille >= SnowLinePermille)
                return MountainSurfaceRole.Snow;
            if (altitudePermille <= GroundCoverCeilingPermille)
                return MountainSurfaceRole.GroundCover;
            return MountainSurfaceRole.Rock;
        }
    }
}
