using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Exact world-voxel envelope for a validated castle cave decoration plan. Min is inclusive;
    /// MaxExclusive is exclusive.
    /// </summary>
    public readonly struct CastleCaveDecorationBuildBounds
    {
        public readonly int3 Min;
        public readonly int3 MaxExclusive;

        internal CastleCaveDecorationBuildBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int3 voxel) =>
            math.all(voxel >= Min) && math.all(voxel < MaxExclusive);
    }

    /// <summary>
    /// Pure bounds resolver mirroring CastlePlannedCaveDecorator's primitive footprints. Because
    /// every placement choice already lives in CastleCaveDecorationPlan, dependency sizing does not
    /// need a castle-specific safety halo around the entire CavePlan.
    /// </summary>
    public static class CastleCaveDecorationBuildBoundsResolver
    {
        public static CastleCaveDecorationBuildBounds Resolve(
            CavePlan cave,
            CastleCaveDecorationPlan decoration)
        {
            if (!CastleCaveDecorationPlanValidator.TryValidate(
                    cave, decoration, out CastleCaveDecorationPlanIssue issue))
            {
                throw new ArgumentException(
                    $"Cannot resolve bounds for invalid castle cave decoration plan: {issue}.",
                    nameof(decoration));
            }

            CastleCaveDecorationSpec[] elements = decoration.Elements;
            ElementBounds(in elements[0], out int3 min, out int3 maxExclusive);
            for (int i = 1; i < elements.Length; i++)
            {
                ElementBounds(in elements[i], out int3 elementMin, out int3 elementMaxExclusive);
                min = math.min(min, elementMin);
                maxExclusive = math.max(maxExclusive, elementMaxExclusive);
            }

            return new CastleCaveDecorationBuildBounds(min, maxExclusive);
        }

        private static void ElementBounds(
            in CastleCaveDecorationSpec spec,
            out int3 min,
            out int3 maxExclusive)
        {
            switch (spec.Kind)
            {
                case CastleCaveDecorationKind.EntryPool:
                    min = new int3(
                        spec.Position.x - spec.Radius,
                        spec.Position.y,
                        spec.Position.z - spec.Radius);
                    maxExclusive = new int3(
                        spec.Position.x + spec.Radius + 1,
                        spec.Position.y + spec.Height,
                        spec.Position.z + spec.Radius + 1);
                    return;

                case CastleCaveDecorationKind.DryCauseway:
                    min = spec.Position;
                    maxExclusive = spec.Position + spec.Size;
                    return;

                case CastleCaveDecorationKind.CrystalSpire:
                case CastleCaveDecorationKind.MossSpire:
                case CastleCaveDecorationKind.Stalagmite:
                    min = new int3(
                        spec.Position.x - spec.Radius,
                        spec.Position.y,
                        spec.Position.z - spec.Radius);
                    maxExclusive = new int3(
                        spec.Position.x + spec.Radius + 1,
                        spec.Position.y + spec.Height,
                        spec.Position.z + spec.Radius + 1);
                    return;

                case CastleCaveDecorationKind.Stalactite:
                    min = new int3(
                        spec.Position.x - spec.Radius,
                        spec.Position.y - spec.Height + 1,
                        spec.Position.z - spec.Radius);
                    maxExclusive = new int3(
                        spec.Position.x + spec.Radius + 1,
                        spec.Position.y + 1,
                        spec.Position.z + spec.Radius + 1);
                    return;

                case CastleCaveDecorationKind.LightMarker:
                    min = spec.Position - new int3(1, 1, 1);
                    maxExclusive = spec.Position + new int3(2, 3, 2);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(spec), spec.Kind, "Unsupported cave decoration kind.");
            }
        }
    }
}
