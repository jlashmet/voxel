using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Reprojects the frozen keep aperture pattern onto the facade already selected for circulation.
    /// The entrance face is planned first from gate/keep geometry; windows must consume that choice
    /// rather than silently reverting to the historical south-facing compatibility layout.
    /// </summary>
    public static class CastleKeepFacadeWindowCompletion
    {
        public static CastleSpatialPlan AlignWithEntrance(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleKeepCirculationPlan circulation = spatial.KeepCirculation;
            if (!CastleKeepCirculationPlanner.TryValidate(
                    in plan, in circulation, out CastleKeepCirculationPlanIssue circulationIssue))
            {
                throw new InvalidOperationException(
                    $"Cannot align keep windows with invalid circulation: {circulationIssue}.");
            }

            CastleKeepFace frontFace = circulation.EntranceFace;
            CastleKeepWindowSpec[] windows =
                CastleKeepWindowPlanner.Create(in plan, frontFace).SnapshotWindows();
            if (!CastleKeepWindowPlanner.TryValidate(
                    in plan, windows, frontFace, out string windowError))
            {
                throw new InvalidOperationException(
                    $"Facade-aligned keep windows are invalid: {windowError}.");
            }

            CastleSpatialPlan detached = CastleSpatialPlanSnapshot.CloneDetached(spatial);
            CastleKeepWindowSpec[] destination = detached.KeepWindows;
            if (destination == null || destination.Length != windows.Length)
            {
                throw new InvalidOperationException(
                    "Completed castle does not contain the expected keep-window aperture count.");
            }

            Array.Copy(windows, destination, windows.Length);
            return detached;
        }
    }
}
