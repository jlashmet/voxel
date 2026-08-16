namespace VoxelEngine.Structures.Api
{
    public enum CastleLandscapePlanIssue : byte
    {
        None = 0,
        MissingPlan,
        MissingDecorations,
        DecorationIdMismatch,
        InvalidDecorationKind,
        InvalidConeDimensions,
        InvalidRubbleDimensions,
    }

    /// <summary>Pure validation for the planned stage-8 decoration payload.</summary>
    public static class CastleLandscapePlanValidator
    {
        public static bool TryValidate(
            CastleLandscapePlan landscape,
            out CastleLandscapePlanIssue issue)
        {
            if (landscape == null)
            {
                issue = CastleLandscapePlanIssue.MissingPlan;
                return false;
            }

            CastleLandscapeDecorationSpec[] decorations = landscape.Decorations;
            if (decorations == null || decorations.Length == 0)
            {
                issue = CastleLandscapePlanIssue.MissingDecorations;
                return false;
            }

            for (int i = 0; i < decorations.Length; i++)
            {
                CastleLandscapeDecorationSpec decoration = decorations[i];
                if (decoration.Id != i)
                {
                    issue = CastleLandscapePlanIssue.DecorationIdMismatch;
                    return false;
                }

                switch (decoration.Kind)
                {
                    case CastleLandscapeDecorationKind.PerimeterMossShrub:
                    case CastleLandscapeDecorationKind.PerimeterGrassShrub:
                    case CastleLandscapeDecorationKind.ApproachDarkStoneRock:
                    case CastleLandscapeDecorationKind.ApproachStoneRock:
                    case CastleLandscapeDecorationKind.ApproachMossScrub:
                        if (decoration.Radius <= 0 || decoration.Height <= 0)
                        {
                            issue = CastleLandscapePlanIssue.InvalidConeDimensions;
                            return false;
                        }
                        break;

                    case CastleLandscapeDecorationKind.PerimeterStoneRubble:
                    case CastleLandscapeDecorationKind.PerimeterDarkStoneRubble:
                        if (decoration.Size.x <= 0 ||
                            decoration.Size.y <= 0 ||
                            decoration.Size.z <= 0)
                        {
                            issue = CastleLandscapePlanIssue.InvalidRubbleDimensions;
                            return false;
                        }
                        break;

                    default:
                        issue = CastleLandscapePlanIssue.InvalidDecorationKind;
                        return false;
                }
            }

            issue = CastleLandscapePlanIssue.None;
            return true;
        }
    }
}
