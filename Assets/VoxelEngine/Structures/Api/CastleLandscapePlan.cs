using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleLandscapeDecorationKind : byte
    {
        PerimeterMossShrub = 0,
        PerimeterGrassShrub,
        PerimeterStoneRubble,
        PerimeterDarkStoneRubble,
        ApproachDarkStoneRock,
        ApproachStoneRock,
        ApproachMossScrub,
    }

    /// <summary>
    /// One terrain-relative landscape decoration chosen during castle planning. Centre is local
    /// X/Z relative to CastlePlan.Centre; Runtime resolves only the final occupied surface Y.
    /// Cone-shaped decorations use Radius/Height. Rubble boxes use Size.
    /// </summary>
    public struct CastleLandscapeDecorationSpec
    {
        public int Id;
        public CastleLandscapeDecorationKind Kind;
        public int2 Centre;
        public int Radius;
        public int Height;
        public int3 Size;
    }

    /// <summary>
    /// Pure planned stage-8 dressing. No storage/material state or terrain mutation is retained.
    /// </summary>
    public sealed class CastleLandscapePlan
    {
        private readonly CastleLandscapeDecorationSpec[] _decorations;

        public CastleLandscapeDecorationSpec[] Decorations => _decorations;

        internal CastleLandscapePlan(CastleLandscapeDecorationSpec[] decorations)
        {
            _decorations = decorations ?? Array.Empty<CastleLandscapeDecorationSpec>();
        }

        /// <summary>
        /// Defensive copy for long-lived realization. Authoring/tests may mutate the planning array,
        /// but an in-flight build must not change after Runtime preflight has accepted it.
        /// </summary>
        public CastleLandscapePlan Snapshot() =>
            new CastleLandscapePlan(
                _decorations != null
                    ? (CastleLandscapeDecorationSpec[])_decorations.Clone()
                    : Array.Empty<CastleLandscapeDecorationSpec>());
    }
}
