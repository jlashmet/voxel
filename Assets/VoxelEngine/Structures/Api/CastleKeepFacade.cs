using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Cardinal keep wall selected as the public-facing entrance facade.</summary>
    public enum CastleKeepFace : byte
    {
        South,
        East,
        North,
        West,
    }

    /// <summary>
    /// Integer keep-local basis for one cardinal facade. Tangent runs along the wall while Outward
    /// points from the keep centre through that facade. This is pure planning geometry: Runtime can
    /// consume the same basis without choosing an orientation itself.
    /// </summary>
    public readonly struct CastleKeepFacadeFrame
    {
        public readonly CastleKeepFace Face;
        public readonly int2 Tangent;
        public readonly int2 Outward;

        private CastleKeepFacadeFrame(
            CastleKeepFace face,
            int2 tangent,
            int2 outward)
        {
            Face = face;
            Tangent = tangent;
            Outward = outward;
        }

        public int2 Inward => -Outward;

        public static CastleKeepFacadeFrame For(CastleKeepFace face)
        {
            switch (face)
            {
                case CastleKeepFace.South:
                    return new CastleKeepFacadeFrame(face, new int2(1, 0), new int2(0, -1));
                case CastleKeepFace.East:
                    return new CastleKeepFacadeFrame(face, new int2(0, 1), new int2(1, 0));
                case CastleKeepFace.North:
                    return new CastleKeepFacadeFrame(face, new int2(-1, 0), new int2(0, 1));
                case CastleKeepFace.West:
                    return new CastleKeepFacadeFrame(face, new int2(0, -1), new int2(-1, 0));
                default:
                    return new CastleKeepFacadeFrame(
                        CastleKeepFace.South, new int2(1, 0), new int2(0, -1));
            }
        }

        /// <summary>Half-size of the keep measured normal to this facade.</summary>
        public int NormalHalfExtent(in CastlePlan plan) =>
            Face == CastleKeepFace.East || Face == CastleKeepFace.West
                ? plan.KeepHalfX
                : plan.KeepHalfZ;

        /// <summary>Half-size of the keep measured along this facade.</summary>
        public int TangentHalfExtent(in CastlePlan plan) =>
            Face == CastleKeepFace.East || Face == CastleKeepFace.West
                ? plan.KeepHalfZ
                : plan.KeepHalfX;

        /// <summary>
        /// Maps facade-relative coordinates into keep-local X/Z. Positive tangent follows
        /// <see cref="Tangent"/>; positive outward follows <see cref="Outward"/>.
        /// </summary>
        public int2 LocalPoint(int tangentDistance, int outwardDistance) =>
            Tangent * tangentDistance + Outward * outwardDistance;

        /// <summary>
        /// Returns a point measured from the selected wall toward the keep interior. An inward
        /// inset of zero lies on the wall centreline; positive values move into the keep.
        /// </summary>
        public int2 PointFromFacade(
            in CastlePlan plan,
            int tangentDistance,
            int inwardInset) =>
            LocalPoint(tangentDistance, NormalHalfExtent(in plan) - inwardInset);
    }

    /// <summary>Pure semantic choice of the keep facade that faces the primary castle approach.</summary>
    public static class CastleKeepFacadePlanner
    {
        public static CastleKeepFace FacingPrimaryGate(
            int2 localKeepCentre,
            in CastleGatePlacementSpec primaryGate)
        {
            int2 towardGate = primaryGate.Centre - localKeepCentre;
            int absX = math.abs(towardGate.x);
            int absZ = math.abs(towardGate.y);

            // Prefer the Z face on exact diagonal ties. This preserves the historical south/north
            // convention for symmetric layouts while still making cardinal side approaches explicit.
            if (absX > absZ)
                return towardGate.x >= 0 ? CastleKeepFace.East : CastleKeepFace.West;

            return towardGate.y >= 0 ? CastleKeepFace.North : CastleKeepFace.South;
        }
    }
}
