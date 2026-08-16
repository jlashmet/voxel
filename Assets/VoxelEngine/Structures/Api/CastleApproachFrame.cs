using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Orthonormal local X/Z frame derived from a planned castle gate. Distances along Tangent run
    /// parallel to the gate wall; positive Outward distances move away from the castle.
    /// </summary>
    public readonly struct CastleApproachFrame
    {
        public readonly int2 GateCentre;
        public readonly float2 Outward;
        public readonly float2 Tangent;

        private CastleApproachFrame(int2 gateCentre, float2 outward, float2 tangent)
        {
            GateCentre = gateCentre;
            Outward = outward;
            Tangent = tangent;
        }

        public static CastleApproachFrame FromGate(in CastleGatePlacementSpec gate)
        {
            float length = math.length(gate.Outward);
            float2 outward = length > 0.001f
                ? gate.Outward / length
                : new float2(0f, -1f);
            float2 tangent = new float2(-outward.y, outward.x);
            return new CastleApproachFrame(gate.Centre, outward, tangent);
        }

        /// <summary>Maps gate-local tangent/outward distances to local castle X/Z coordinates.</summary>
        public int2 LocalPoint(float tangentDistance, float outwardDistance)
        {
            float2 gate = new float2(GateCentre.x, GateCentre.y);
            float2 point = gate + Tangent * tangentDistance + Outward * outwardDistance;
            return new int2((int)math.round(point.x), (int)math.round(point.y));
        }
    }
}
