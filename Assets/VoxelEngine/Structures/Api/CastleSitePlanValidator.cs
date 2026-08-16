namespace VoxelEngine.Structures.Api
{
    public enum CastleSitePlanIssue : byte
    {
        None,
        MissingGrassPatternSeed,
        MissingCourtyardPatternSeed,
        InvalidGeometry,
    }

    /// <summary>
    /// Pure structural validation for the frozen castle-site recipe. A topology plan must not reach
    /// spatial placement with a default seed payload or malformed terrain/approach geometry.
    /// </summary>
    public static class CastleSitePlanValidator
    {
        public static bool TryValidate(in CastleSitePlan plan, out CastleSitePlanIssue issue)
        {
            if (plan.GrassPatternSeed == 0u)
            {
                issue = CastleSitePlanIssue.MissingGrassPatternSeed;
                return false;
            }

            if (plan.CourtyardStonePercent > 0 && plan.CourtyardPatternSeed == 0u)
            {
                issue = CastleSitePlanIssue.MissingCourtyardPatternSeed;
                return false;
            }

            CastleSiteGeometryPlan geometry = plan.Geometry;
            if (!PositiveFinite(geometry.EdgeFrequencyA) || geometry.EdgeAmplitudeA < 0f ||
                !PositiveFinite(geometry.EdgeFrequencyB) || geometry.EdgeAmplitudeB < 0f ||
                !PositiveFinite(geometry.EdgeFrequencyC) || geometry.EdgeAmplitudeC < 0f ||
                !PositiveFinite(geometry.CliffFalloffExponent) ||
                !PositiveFinite(geometry.CliffNoiseAngularFrequency) ||
                !PositiveFinite(geometry.CliffNoiseProgressFrequency) ||
                geometry.CliffNoiseAmplitude < 0f ||
                geometry.CliffGroundInset < 0 || geometry.GrassEdgeInset < 0 ||
                geometry.ApproachReachInset < 0 || geometry.RiverOffset < 0 ||
                geometry.RiverHalfWidth <= 0 || geometry.WaterHalfWidth <= 0 ||
                geometry.WaterHalfWidth > geometry.RiverHalfWidth || geometry.RiverDepth <= 0 ||
                !PositiveFinite(geometry.MeanderFrequencyA) || geometry.MeanderAmplitudeA < 0f ||
                !PositiveFinite(geometry.MeanderFrequencyB) || geometry.MeanderAmplitudeB < 0f)
            {
                issue = CastleSitePlanIssue.InvalidGeometry;
                return false;
            }

            issue = CastleSitePlanIssue.None;
            return true;
        }

        private static bool PositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
