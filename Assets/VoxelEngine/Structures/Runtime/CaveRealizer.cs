using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Generic voxel realization for a planned natural cave. Topology and chamber placement are
    /// fixed upstream; this component only carves connected void geometry and deliberately owns no
    /// castle, dungeon, terrain, or decoration policy.
    /// </summary>
    public static class CaveRealizer
    {
        public static void Build(ref VoxelBrush brush, CavePlan plan)
        {
            if (!CavePlanValidator.TryValidate(plan, out CavePlanIssue issue))
                throw new InvalidOperationException($"Invalid cave plan: {issue}.");

            CaveChamberPlan[] chambers = plan.Chambers;
            for (int i = 0; i < chambers.Length; i++)
                CarveChamber(ref brush, in chambers[i]);

            CavePassagePlan[] passages = plan.Passages;
            if (passages == null) return;
            for (int i = 0; i < passages.Length; i++)
            {
                CavePassagePlan passage = passages[i];
                CaveChamberPlan from = chambers[passage.FromChamberId];
                CaveChamberPlan to = chambers[passage.ToChamberId];
                CarvePassage(ref brush, in from, in to, in passage);
            }
        }

        private static void CarveChamber(ref VoxelBrush brush, in CaveChamberPlan chamber)
        {
            int rx = chamber.Radii.x;
            int ry = chamber.Radii.y;
            int rz = chamber.Radii.z;
            float cos = math.cos(chamber.RotationRadians);
            float sin = math.sin(chamber.RotationRadians);
            float invRxSq = 1f / (rx * (float)rx);
            float invRzSq = 1f / (rz * (float)rz);

            // A rotated ellipse can extend beyond its unrotated rx/rz box. Iterate the exact
            // projected AABB (rounded outward) and let the ellipse test reject unused columns.
            int extentX = (int)math.ceil(math.sqrt(
                rx * (float)rx * cos * cos + rz * (float)rz * sin * sin));
            int extentZ = (int)math.ceil(math.sqrt(
                rx * (float)rx * sin * sin + rz * (float)rz * cos * cos));

            for (int dz = -extentZ; dz <= extentZ; dz++)
            for (int dx = -extentX; dx <= extentX; dx++)
            {
                float rotatedX = cos * dx + sin * dz;
                float rotatedZ = -sin * dx + cos * dz;
                float horizontal = rotatedX * rotatedX * invRxSq
                                 + rotatedZ * rotatedZ * invRzSq;
                if (horizontal > 1f) continue;

                int halfY = math.max(1,
                    (int)math.floor(ry * math.sqrt(math.max(0f, 1f - horizontal))));
                brush.FillColumnBulk(
                    chamber.Centre.x + dx,
                    chamber.Centre.y - halfY,
                    chamber.Centre.y + halfY + 1,
                    chamber.Centre.z + dz,
                    Mat.Empty);
            }
        }

        private static void CarvePassage(
            ref VoxelBrush brush,
            in CaveChamberPlan from,
            in CaveChamberPlan to,
            in CavePassagePlan passage)
        {
            int3 delta = to.Centre - from.Centre;
            int distance = math.max(math.abs(delta.x), math.max(math.abs(delta.y), math.abs(delta.z)));
            int radius = math.max(1, passage.Width / 2);
            int halfHeight = math.max(1, passage.Height / 2);
            int stride = math.max(2, radius / 2);
            int samples = math.max(1, (distance + stride - 1) / stride);
            int radiusSq = radius * radius;

            float3 start = new float3(from.Centre.x, from.Centre.y, from.Centre.z);
            float3 end = new float3(to.Centre.x, to.Centre.y, to.Centre.z);
            for (int sample = 0; sample <= samples; sample++)
            {
                float t = sample / (float)samples;
                float3 point = math.lerp(start, end, t);
                int3 centre = new int3(
                    (int)math.round(point.x),
                    (int)math.round(point.y),
                    (int)math.round(point.z));

                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dz * dz > radiusSq) continue;
                    brush.FillColumnBulk(
                        centre.x + dx,
                        centre.y - halfHeight,
                        centre.y + halfHeight + 1,
                        centre.z + dz,
                        Mat.Empty);
                }
            }
        }
    }
}
