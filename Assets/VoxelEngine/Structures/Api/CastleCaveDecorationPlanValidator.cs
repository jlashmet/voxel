using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleCaveDecorationPlanIssue : byte
    {
        None,
        MissingPlan,
        CaveSeedMismatch,
        MissingElements,
        ElementIdMismatch,
        InvalidChamberId,
        InvalidElementGeometry,
        ElementOutsideChamber,
        EntryDecorationMismatch,
    }

    /// <summary>Pure structural validation for castle-specific cave decoration instructions.</summary>
    public static class CastleCaveDecorationPlanValidator
    {
        public static bool TryValidate(
            CavePlan cave,
            CastleCaveDecorationPlan decoration,
            out CastleCaveDecorationPlanIssue issue)
        {
            if (cave == null || decoration == null)
            {
                issue = CastleCaveDecorationPlanIssue.MissingPlan;
                return false;
            }

            if (!CavePlanValidator.TryValidate(cave, out _))
            {
                issue = CastleCaveDecorationPlanIssue.MissingPlan;
                return false;
            }

            if (decoration.CaveSeed != cave.Seed)
            {
                issue = CastleCaveDecorationPlanIssue.CaveSeedMismatch;
                return false;
            }

            CastleCaveDecorationSpec[] elements = decoration.Elements;
            if (elements == null || elements.Length == 0)
            {
                issue = CastleCaveDecorationPlanIssue.MissingElements;
                return false;
            }

            int entryPools = 0;
            int causeways = 0;
            for (int i = 0; i < elements.Length; i++)
            {
                CastleCaveDecorationSpec spec = elements[i];
                if (spec.Id != i)
                {
                    issue = CastleCaveDecorationPlanIssue.ElementIdMismatch;
                    return false;
                }

                if (spec.ChamberId < 0 || spec.ChamberId >= cave.Chambers.Length)
                {
                    issue = CastleCaveDecorationPlanIssue.InvalidChamberId;
                    return false;
                }

                CaveChamberPlan chamber = cave.Chambers[spec.ChamberId];
                if (!AnchorInside(in chamber, spec.Position))
                {
                    issue = CastleCaveDecorationPlanIssue.ElementOutsideChamber;
                    return false;
                }

                switch (spec.Kind)
                {
                    case CastleCaveDecorationKind.EntryPool:
                        entryPools++;
                        if (spec.ChamberId != cave.EntryChamberId ||
                            spec.Radius <= 0 || spec.Height <= 0 ||
                            math.abs(spec.Position.x - chamber.Centre.x) + spec.Radius > chamber.Radii.x ||
                            math.abs(spec.Position.z - chamber.Centre.z) + spec.Radius > chamber.Radii.z)
                        {
                            issue = CastleCaveDecorationPlanIssue.InvalidElementGeometry;
                            return false;
                        }
                        break;

                    case CastleCaveDecorationKind.DryCauseway:
                        causeways++;
                        if (spec.ChamberId != cave.EntryChamberId || math.any(spec.Size <= 0) ||
                            !AnchorInside(in chamber, spec.Position + spec.Size - 1))
                        {
                            issue = CastleCaveDecorationPlanIssue.InvalidElementGeometry;
                            return false;
                        }
                        break;

                    case CastleCaveDecorationKind.CrystalSpire:
                    case CastleCaveDecorationKind.MossSpire:
                    case CastleCaveDecorationKind.Stalagmite:
                        if (spec.Radius <= 0 || spec.Height <= 0 ||
                            spec.Position.y + spec.Height > chamber.Centre.y + chamber.Radii.y + 1)
                        {
                            issue = CastleCaveDecorationPlanIssue.InvalidElementGeometry;
                            return false;
                        }
                        break;

                    case CastleCaveDecorationKind.Stalactite:
                        if (spec.Radius <= 0 || spec.Height <= 0 ||
                            spec.Position.y - spec.Height < chamber.Centre.y - chamber.Radii.y - 1)
                        {
                            issue = CastleCaveDecorationPlanIssue.InvalidElementGeometry;
                            return false;
                        }
                        break;

                    case CastleCaveDecorationKind.LightMarker:
                        if (spec.Position.y + 2 > chamber.Centre.y + chamber.Radii.y)
                        {
                            issue = CastleCaveDecorationPlanIssue.InvalidElementGeometry;
                            return false;
                        }
                        break;

                    default:
                        issue = CastleCaveDecorationPlanIssue.InvalidElementGeometry;
                        return false;
                }
            }

            if (entryPools != 1 || causeways != 1)
            {
                issue = CastleCaveDecorationPlanIssue.EntryDecorationMismatch;
                return false;
            }

            issue = CastleCaveDecorationPlanIssue.None;
            return true;
        }

        private static bool AnchorInside(in CaveChamberPlan chamber, int3 point) =>
            math.abs(point.x - chamber.Centre.x) <= chamber.Radii.x &&
            math.abs(point.y - chamber.Centre.y) <= chamber.Radii.y &&
            math.abs(point.z - chamber.Centre.z) <= chamber.Radii.z;
    }
}
