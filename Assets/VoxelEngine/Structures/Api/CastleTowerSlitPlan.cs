using System;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Frozen per-storey arrow-slit rotation for one circular tower. The phases preserve the
    /// historical world-position seed sequence while allowing planned realization to avoid RNG.
    /// </summary>
    public sealed class CastleTowerSlitPlan
    {
        private readonly float[] _phaseRadians;

        internal CastleTowerSlitPlan(float[] phaseRadians)
        {
            _phaseRadians = phaseRadians != null
                ? (float[])phaseRadians.Clone()
                : Array.Empty<float>();
        }

        public int FloorCount => _phaseRadians.Length;

        public float PhaseRadiansAt(int floorIndex)
        {
            if ((uint)floorIndex >= (uint)_phaseRadians.Length)
                throw new ArgumentOutOfRangeException(nameof(floorIndex));
            return _phaseRadians[floorIndex];
        }

        public float[] Snapshot() => (float[])_phaseRadians.Clone();
    }

    public enum CastleTowerSlitPlanIssue : byte
    {
        None,
        MissingPlan,
        FloorCountMismatch,
        InvalidPhase,
    }

    public static class CastleTowerSlitPlanValidator
    {
        public static bool TryValidate(
            CastleTowerSlitPlan plan,
            int towerHeight,
            int floorHeight,
            out CastleTowerSlitPlanIssue issue)
        {
            if (plan == null)
            {
                issue = CastleTowerSlitPlanIssue.MissingPlan;
                return false;
            }

            if (towerHeight <= 0 || floorHeight <= 0)
            {
                issue = CastleTowerSlitPlanIssue.FloorCountMismatch;
                return false;
            }

            int expected = CastleTowerSlitPlanner.RequiredFloorCount(towerHeight, floorHeight);
            if (plan.FloorCount != expected)
            {
                issue = CastleTowerSlitPlanIssue.FloorCountMismatch;
                return false;
            }

            for (int floor = 0; floor < plan.FloorCount; floor++)
            {
                float phase = plan.PhaseRadiansAt(floor);
                if (!math.isfinite(phase) || phase < 0f || phase >= 6.28f)
                {
                    issue = CastleTowerSlitPlanIssue.InvalidPhase;
                    return false;
                }
            }

            issue = CastleTowerSlitPlanIssue.None;
            return true;
        }

        public static void RequireValid(
            CastleTowerSlitPlan plan,
            int towerHeight,
            int floorHeight)
        {
            if (TryValidate(plan, towerHeight, floorHeight, out CastleTowerSlitPlanIssue issue))
                return;

            throw new InvalidOperationException($"Castle tower slit plan is invalid: {issue}.");
        }
    }

    public static class CastleTowerSlitPlanner
    {
        public static CastleTowerSlitPlan Create(
            int2 worldCentre,
            int towerHeight,
            int floorHeight)
        {
            if (towerHeight <= 0) throw new ArgumentOutOfRangeException(nameof(towerHeight));
            if (floorHeight <= 0) throw new ArgumentOutOfRangeException(nameof(floorHeight));

            int floors = RequiredFloorCount(towerHeight, floorHeight);
            var phases = new float[floors];
            uint historicalSeed = unchecked(
                (uint)(worldCentre.x * 8191 + worldCentre.y * 131071) | 1u);
            var rng = new Random(historicalSeed);
            for (int floor = 0; floor < floors; floor++)
                phases[floor] = rng.NextFloat(0f, 6.28f);

            var plan = new CastleTowerSlitPlan(phases);
            CastleTowerSlitPlanValidator.RequireValid(plan, towerHeight, floorHeight);
            return plan;
        }

        internal static int RequiredFloorCount(int towerHeight, int floorHeight)
        {
            int count = 0;
            for (int floor = 0; floor * floorHeight < towerHeight - 40; floor++)
                count++;
            return count;
        }
    }
}
