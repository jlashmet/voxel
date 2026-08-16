using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes a castle perimeter from already-planned local X/Z geometry without making topology
    /// decisions. Planning owns vertices, tower locations, and gate orientation; this component
    /// owns only their voxel realization.
    /// </summary>
    public static class CastlePerimeterRealizer
    {
        public static void Walls(ref VoxelBrush brush, in CastlePlan plan, int2[] localVertices) =>
            Walls(ref brush, in plan, localVertices, -1, default, 0);

        public static void Walls(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localVertices,
            int gateEdgeIndex,
            int2 localGateCentre,
            int gateClearWidth)
        {
            if (localVertices == null || localVertices.Length < 3)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int2 gateCentre = ToWorld(in plan, localGateCentre);
            for (int i = 0; i < localVertices.Length; i++)
            {
                int2 start = ToWorld(in plan, localVertices[i]);
                int2 end = ToWorld(in plan, localVertices[(i + 1) % localVertices.Length]);

                if (i == gateEdgeIndex && gateClearWidth > 0)
                    WallWithOpening(
                        ref brush, in plan, start, end, gateCentre, gateClearWidth, baseY);
                else
                    WallSegment(ref brush, in plan, start, end, baseY);
            }
        }

        public static void Towers(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localCentres,
            int cornerCount)
        {
            if (localCentres == null)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int corners = math.clamp(cornerCount, 0, localCentres.Length);
            for (int i = 0; i < localCentres.Length; i++)
            {
                int2 world = ToWorld(in plan, localCentres[i]);
                uint variationSeed = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Walls, (uint)(0x2000 + i));
                int heightVariation = 8 + (int)(variationSeed % 51u);
                int height = plan.TowerHeight + heightVariation;
                bool roof = i < corners && ((variationSeed >> 8) & 1u) != 0u;

                CastleTowerRealizer.Build(
                    ref brush,
                    in plan,
                    new int3(world.x, baseY, world.y),
                    plan.TowerRadius,
                    height,
                    roof);
            }
        }

        /// <summary>
        /// Compatibility wrapper for callers that still provide only gate placement. Production
        /// spatial builds carry a frozen CastleGatehousePlan and call CastlePlannedGatehouseRealizer
        /// directly through CastleBuildPipeline. The historical recipe is an API compatibility value,
        /// not a Runtime planner invocation.
        /// </summary>
        public static void Gatehouse(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 localGateCentre,
            float2 outward)
        {
            var placement = new CastleGatePlacementSpec
            {
                EdgeIndex = -1,
                Centre = localGateCentre,
                Outward = outward,
            };
            CastleGatehousePlan gatehouse = CastleGatehouseRecipe.Historical(in plan);
            CastlePlannedGatehouseRealizer.Build(
                ref brush, in plan, in placement, in gatehouse);
        }

        private static void WallWithOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 start,
            int2 end,
            int2 gateCentre,
            int gateClearWidth,
            int baseY)
        {
            float2 a = new float2(start.x, start.y);
            float2 delta = new float2(end.x - start.x, end.y - start.y);
            float length = math.length(delta);
            if (length < 1f)
                return;

            float2 tangent = delta / length;
            float2 gate = new float2(gateCentre.x, gateCentre.y);
            float gateDistance = math.clamp(math.dot(gate - a, tangent), 0f, length);
            float halfGap = math.max(1f, gateClearWidth * 0.5f);
            float beforeEnd = gateDistance - halfGap;
            float afterStart = gateDistance + halfGap;

            if (beforeEnd > 0.5f)
                WallSegment(
                    ref brush, in plan, start, Round(a + tangent * beforeEnd), baseY);
            if (afterStart < length - 0.5f)
                WallSegment(
                    ref brush, in plan, Round(a + tangent * afterStart), end, baseY);
        }

        private static void WallSegment(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 start,
            int2 end,
            int baseY)
        {
            int height = plan.WallHeight;
            int thickness = plan.WallThickness;
            if (height <= 0 || thickness <= 0)
                return;

            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY, height, thickness, Mat.Stone);

            int plinthHeight = math.min(22, height);
            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY, plinthHeight, thickness, Mat.DarkStone);

            if (height >= 4)
            {
                int courseY = baseY + (int)(height * 0.66f);
                VoxelWallRasterizer.FillSegment(
                    ref brush, start, end, courseY, 2, thickness, Mat.DarkStone);
            }

            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY + height, 1, thickness, Mat.Stone);

            CarveArrowSlits(ref brush, start, end, baseY, height, thickness);
            Crenellate(ref brush, start, end, baseY + height + 1, thickness);
        }

        private static void CarveArrowSlits(
            ref VoxelBrush brush,
            int2 start,
            int2 end,
            int baseY,
            int wallHeight,
            int wallThickness)
        {
            if (wallHeight < 70)
                return;

            float2 a = new float2(start.x, start.y);
            float2 delta = new float2(end.x - start.x, end.y - start.y);
            float length = math.length(delta);
            if (length < 1f)
                return;

            float2 tangent = delta / length;
            float2 normal = new float2(-tangent.y, tangent.x);
            float halfDepth = math.max(1f, wallThickness * 0.65f);

            for (float distance = 40f; distance < length - 20f; distance += 90f)
            {
                float2 centre = a + tangent * distance;
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    Round(centre - normal * halfDepth),
                    Round(centre + normal * halfDepth),
                    baseY + 40,
                    math.min(28, wallHeight - 40),
                    2,
                    Mat.Empty);
            }
        }

        private static void Crenellate(
            ref VoxelBrush brush,
            int2 start,
            int2 end,
            int parapetY,
            int wallThickness)
        {
            float2 a = new float2(start.x, start.y);
            float2 delta = new float2(end.x - start.x, end.y - start.y);
            float length = math.length(delta);
            if (length < 1f)
                return;

            float2 tangent = delta / length;
            const float merlon = 26f;
            const float gap = 18f;
            float period = merlon + gap;
            int thickness = math.max(2, math.min(8, wallThickness));

            for (float distance = 0f; distance < length; distance += period)
            {
                float endDistance = math.min(length, distance + merlon);
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    Round(a + tangent * distance),
                    Round(a + tangent * endDistance),
                    parapetY,
                    20,
                    thickness,
                    Mat.Stone);
            }
        }

        private static int2 ToWorld(in CastlePlan plan, int2 local) =>
            new int2(plan.Centre.x + local.x, plan.Centre.z + local.y);

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}
