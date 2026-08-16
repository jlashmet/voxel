using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Castle-specific dressing for an already planned natural cave. Planning owns every placement
    /// and variation choice; this component only maps stable decoration specs to voxel materials.
    /// </summary>
    public static class CastlePlannedCaveDecorator
    {
        public static void Build(
            ref VoxelBrush brush,
            CavePlan cave,
            CastleCaveDecorationPlan decoration)
        {
            if (!CastleCaveDecorationPlanValidator.TryValidate(
                    cave, decoration, out CastleCaveDecorationPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot realize invalid castle cave decoration plan: {issue}.");
            }

            CastleCaveDecorationSpec[] elements = decoration.Elements;
            for (int i = 0; i < elements.Length; i++)
            {
                CastleCaveDecorationSpec spec = elements[i];
                BuildPlannedElement(ref brush, in spec);
            }
        }

        private static void BuildPlannedElement(
            ref VoxelBrush brush,
            in CastleCaveDecorationSpec spec)
        {
            switch (spec.Kind)
            {
                case CastleCaveDecorationKind.EntryPool:
                {
                    int radiusSq = spec.Radius * spec.Radius;
                    for (int dz = -spec.Radius; dz <= spec.Radius; dz++)
                    for (int dx = -spec.Radius; dx <= spec.Radius; dx++)
                    {
                        if (dx * dx + dz * dz > radiusSq) continue;
                        brush.FillColumnBulk(
                            spec.Position.x + dx,
                            spec.Position.y,
                            spec.Position.y + spec.Height,
                            spec.Position.z + dz,
                            Mat.Water);
                    }
                    break;
                }

                case CastleCaveDecorationKind.DryCauseway:
                    brush.Box(spec.Position, spec.Size, Mat.DarkStone);
                    break;

                case CastleCaveDecorationKind.CrystalSpire:
                    brush.Cone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.Crystal);
                    break;

                case CastleCaveDecorationKind.MossSpire:
                    brush.Cone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.Moss);
                    break;

                case CastleCaveDecorationKind.Stalagmite:
                    brush.Cone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.DarkStone);
                    break;

                case CastleCaveDecorationKind.Stalactite:
                    brush.HangingCone(
                        spec.Position.x, spec.Position.y, spec.Position.z,
                        spec.Radius, spec.Height, Mat.DarkStone);
                    break;

                case CastleCaveDecorationKind.LightMarker:
                    brush.Box(spec.Position, new int3(1, 3, 1), Mat.Glass);
                    brush.Box(
                        spec.Position - new int3(1, 1, 1),
                        new int3(3, 1, 3),
                        Mat.Gold);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported castle cave decoration kind: {spec.Kind}.");
            }
        }
    }
}
