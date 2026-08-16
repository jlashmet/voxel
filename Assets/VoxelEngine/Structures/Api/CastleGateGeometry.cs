using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure world-space geometry for one castle gate leaf. The placement owns where the gate is;
    /// this value owns the shared local-to-world basis used by realization and interaction.
    /// </summary>
    public readonly struct CastleGateGeometry
    {
        public readonly int3 Origin;
        public readonly float2 PerimeterCentre;
        public readonly float2 Tangent;
        public readonly float2 Outward;
        public readonly int Width;
        public readonly int Height;
        public readonly int Depth;

        internal CastleGateGeometry(
            int3 origin,
            float2 perimeterCentre,
            float2 tangent,
            float2 outward,
            int width,
            int height,
            int depth)
        {
            Origin = origin;
            PerimeterCentre = perimeterCentre;
            Tangent = tangent;
            Outward = outward;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public int RectangularVoxelCount => Width * Height * Depth;

        /// <summary>Maps a local gate voxel to the authoritative world-space voxel coordinate.</summary>
        public int3 WorldVoxel(int widthIndex, int heightIndex, int depthIndex)
        {
            float2 originXZ = new float2(Origin.x, Origin.z);
            float2 worldXZ = originXZ
                           + Tangent * widthIndex
                           - Outward * depthIndex;
            return new int3(
                (int)math.round(worldXZ.x),
                Origin.y + heightIndex,
                (int)math.round(worldXZ.y));
        }

        /// <summary>Matches the semicircular head used by VoxelBrush.Arch.</summary>
        public bool ContainsArchVoxel(int widthIndex, int heightIndex)
        {
            if (widthIndex < 0 || widthIndex >= Width ||
                heightIndex < 0 || heightIndex >= Height)
                return false;

            int half = Width / 2;
            int dx = widthIndex - half;
            int archTop = Height - half;
            return heightIndex <= archTop ||
                   dx * dx + (heightIndex - archTop) * (heightIndex - archTop) <= half * half;
        }

        /// <summary>
        /// Enumerates the authored arch leaf through one stable rectangular linear index. Returns
        /// false for indices that land in the empty corners above the semicircular head. Callers
        /// that clear or decorate the gate can therefore share exactly the same voxel set as the
        /// realizer without reimplementing the arch equation.
        /// </summary>
        public bool TryGetArchVoxel(
            int linearIndex,
            out int3 voxel,
            out int heightIndex)
        {
            if (linearIndex < 0 || linearIndex >= RectangularVoxelCount)
            {
                voxel = default;
                heightIndex = 0;
                return false;
            }

            int plane = Width * Height;
            int depthIndex = linearIndex / plane;
            int remainder = linearIndex - depthIndex * plane;
            int widthIndex = remainder / Height;
            heightIndex = remainder - widthIndex * Height;
            if (!ContainsArchVoxel(widthIndex, heightIndex))
            {
                voxel = default;
                return false;
            }

            voxel = WorldVoxel(widthIndex, heightIndex, depthIndex);
            return true;
        }

        /// <summary>
        /// Player-facing interaction point eight voxels outside the gate face, preserving the
        /// historical showcase reach point for the legacy front gate.
        /// </summary>
        public float3 InteractionPointVoxels
        {
            get
            {
                float2 originXZ = new float2(Origin.x, Origin.z);
                float2 point = originXZ + Tangent * (Width * 0.5f) + Outward * 8f;
                return new float3(point.x, Origin.y, point.y);
            }
        }
    }

    /// <summary>Pure resolver shared by castle construction, interaction, and tests.</summary>
    public static class CastleGateGeometryResolver
    {
        public static CastleGateGeometry Resolve(
            in CastlePlan plan,
            in CastleGatePlacementSpec placement)
        {
            float2 outward = placement.Outward;
            float length = math.length(outward);
            outward = length > 0.001f ? outward / length : new float2(0f, -1f);
            float2 tangent = new float2(-outward.y, outward.x);

            float2 perimeterCentre = new float2(
                plan.Centre.x + placement.Centre.x,
                plan.Centre.z + placement.Centre.y);

            // The legacy gate starts just outside the curtain wall and extends inward through it.
            // Keeping that convention makes the axis-aligned compatibility placement byte-for-byte
            // identical while allowing the same basis to rotate around arbitrary perimeter edges.
            float2 originXZ = perimeterCentre
                            - tangent * (CastleLayout.FrontGateWidth * 0.5f)
                            + outward * (plan.WallThickness - 2f);
            int3 origin = new int3(
                (int)math.round(originXZ.x),
                plan.Centre.y + plan.PlateauHeight + 1,
                (int)math.round(originXZ.y));

            return new CastleGateGeometry(
                origin,
                perimeterCentre,
                tangent,
                outward,
                CastleLayout.FrontGateWidth,
                CastleLayout.FrontGateHeight,
                CastleLayout.FrontGateDepth);
        }

        public static CastleGateGeometry Resolve(
            in CastlePlan plan,
            int2 localCentre,
            float2 outward)
        {
            var placement = new CastleGatePlacementSpec
            {
                EdgeIndex = -1,
                Centre = localCentre,
                Outward = outward,
            };
            return Resolve(in plan, in placement);
        }

        /// <summary>Compatibility placement for the historical centred -Z front gate.</summary>
        public static CastleGateGeometry LegacyFront(in CastlePlan plan)
        {
            var placement = new CastleGatePlacementSpec
            {
                EdgeIndex = 0,
                Centre = new int2(0, -plan.BaileyHalfZ),
                Outward = new float2(0f, -1f),
            };
            return Resolve(in plan, in placement);
        }
    }
}
