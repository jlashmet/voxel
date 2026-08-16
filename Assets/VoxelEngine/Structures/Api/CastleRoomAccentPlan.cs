using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// One planner-owned variable accent inside a keep floor, expressed relative to the keep
    /// interior minimum corner. Fixed room furniture remains part of the realization recipe; only
    /// choices that historically consumed RNG are represented here.
    /// </summary>
    public readonly struct CastleRoomAccentSpec
    {
        public readonly int Id;
        public readonly int LocalX;
        public readonly int LocalZ;
        public readonly int Radius;
        public readonly int Height;

        public CastleRoomAccentSpec(int id, int localX, int localZ, int radius, int height)
        {
            Id = id;
            LocalX = localX;
            LocalZ = localZ;
            Radius = radius;
            Height = height;
        }
    }

    /// <summary>Immutable snapshot of planner-owned variable room accents for one keep floor.</summary>
    public sealed class CastleRoomAccentPlan
    {
        private readonly CastleRoomAccentSpec[] _accents;

        internal CastleRoomAccentPlan(CastleRoomAccentSpec[] accents)
        {
            _accents = accents != null
                ? (CastleRoomAccentSpec[])accents.Clone()
                : Array.Empty<CastleRoomAccentSpec>();
        }

        public int Count => _accents.Length;

        public CastleRoomAccentSpec AccentAt(int index)
        {
            if ((uint)index >= (uint)_accents.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _accents[index];
        }

        public CastleRoomAccentSpec[] Snapshot() =>
            (CastleRoomAccentSpec[])_accents.Clone();
    }

    public enum CastleRoomAccentPlanIssue : byte
    {
        None = 0,
        MissingPlan,
        IdMismatch,
        OutsideKeepInterior,
        InvalidRadius,
        InvalidHeight,
    }

    /// <summary>Pure validation for variable room accents before Runtime sees them.</summary>
    public static class CastleRoomAccentPlanValidator
    {
        public static bool TryValidate(
            in CastlePlan dimensions,
            CastleRoomAccentPlan plan,
            out CastleRoomAccentPlanIssue issue)
        {
            if (plan == null)
            {
                issue = CastleRoomAccentPlanIssue.MissingPlan;
                return false;
            }

            int width = dimensions.KeepHalfX * 2;
            int depth = dimensions.KeepHalfZ * 2;
            for (int i = 0; i < plan.Count; i++)
            {
                CastleRoomAccentSpec accent = plan.AccentAt(i);
                if (accent.Id != i)
                {
                    issue = CastleRoomAccentPlanIssue.IdMismatch;
                    return false;
                }
                if (accent.LocalX < 0 || accent.LocalX >= width ||
                    accent.LocalZ < 0 || accent.LocalZ >= depth)
                {
                    issue = CastleRoomAccentPlanIssue.OutsideKeepInterior;
                    return false;
                }
                if (accent.Radius <= 0)
                {
                    issue = CastleRoomAccentPlanIssue.InvalidRadius;
                    return false;
                }
                if (accent.Height <= 0)
                {
                    issue = CastleRoomAccentPlanIssue.InvalidHeight;
                    return false;
                }
            }

            issue = CastleRoomAccentPlanIssue.None;
            return true;
        }
    }

    /// <summary>
    /// Moves the historical keep-room accent RNG out of Runtime. The draw order intentionally
    /// preserves the legacy loop exactly: NextInt(2,5) is re-evaluated in the loop condition on
    /// every iteration rather than being sampled once as a fixed count.
    /// </summary>
    public static class CastleRoomAccentPlanner
    {
        private const int InnerInset = 8;

        public static CastleRoomAccentPlan Create(
            in CastlePlan dimensions,
            in CastleKeepFloorPlan floorPlan)
        {
            int width = dimensions.KeepHalfX * 2;
            int depth = dimensions.KeepHalfZ * 2;
            if (width <= InnerInset * 2 + 30 || depth <= InnerInset * 2 + 20)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dimensions),
                    "Keep footprint is too small for the authored variable room accents.");
            }

            var rng = new Random(floorPlan.SemanticSeed);
            var accents = new List<CastleRoomAccentSpec>(4);

            // Do not collapse this to `int count = rng.NextInt(2, 5)`. The historical Runtime
            // recipe sampled the loop condition again after every accent, and seed parity depends
            // on preserving that exact sequence of draws.
            for (int i = 0; i < rng.NextInt(2, 5); i++)
            {
                bool leftWall = rng.NextBool();
                int localX = leftWall
                    ? InnerInset + 22
                    : width - InnerInset - 30;
                int localZ = rng.NextInt(
                    InnerInset + 8,
                    depth - InnerInset - 12);
                int radius = rng.NextInt(4, 7);
                int height = rng.NextInt(8, 14);
                accents.Add(new CastleRoomAccentSpec(
                    accents.Count,
                    localX,
                    localZ,
                    radius,
                    height));
            }

            var result = new CastleRoomAccentPlan(accents.ToArray());
            if (!CastleRoomAccentPlanValidator.TryValidate(
                    in dimensions, result, out CastleRoomAccentPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle room accent planning produced an invalid plan: {issue}.");
            }
            return result;
        }
    }
}
