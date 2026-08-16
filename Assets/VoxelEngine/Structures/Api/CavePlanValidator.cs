using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CavePlanIssue : byte
    {
        None,
        MissingChambers,
        InvalidEntryChamber,
        ChamberIdMismatch,
        InvalidChamberRadii,
        InvalidChamberRotation,
        EntranceOutsideEntryChamber,
        MissingPassages,
        InvalidPassageEndpoint,
        SelfPassage,
        InvalidPassageSize,
        DuplicatePassage,
        DisconnectedGraph,
    }

    /// <summary>
    /// Pure topology/spatial validation for natural caves. Chamber overlap is intentionally valid:
    /// intersecting lobes are a useful cave shape, unlike overlapping authored dungeon rooms.
    /// </summary>
    public static class CavePlanValidator
    {
        public static bool TryValidate(CavePlan plan, out CavePlanIssue issue)
        {
            if (plan == null || plan.Chambers == null || plan.Chambers.Length == 0)
            {
                issue = CavePlanIssue.MissingChambers;
                return false;
            }

            CaveChamberPlan[] chambers = plan.Chambers;
            if (plan.EntryChamberId < 0 || plan.EntryChamberId >= chambers.Length)
            {
                issue = CavePlanIssue.InvalidEntryChamber;
                return false;
            }

            for (int i = 0; i < chambers.Length; i++)
            {
                CaveChamberPlan chamber = chambers[i];
                if (chamber.Id != i)
                {
                    issue = CavePlanIssue.ChamberIdMismatch;
                    return false;
                }

                if (math.any(chamber.Radii <= 0))
                {
                    issue = CavePlanIssue.InvalidChamberRadii;
                    return false;
                }

                if (!math.isfinite(chamber.RotationRadians))
                {
                    issue = CavePlanIssue.InvalidChamberRotation;
                    return false;
                }
            }

            CaveChamberPlan entry = chambers[plan.EntryChamberId];
            int3 entranceDelta = math.abs(plan.Entrance - entry.Centre);
            if (math.any(entranceDelta > entry.Radii))
            {
                issue = CavePlanIssue.EntranceOutsideEntryChamber;
                return false;
            }

            CavePassagePlan[] passages = plan.Passages;
            if (chambers.Length > 1 && (passages == null || passages.Length == 0))
            {
                issue = CavePlanIssue.MissingPassages;
                return false;
            }
            if (passages == null) passages = System.Array.Empty<CavePassagePlan>();

            for (int i = 0; i < passages.Length; i++)
            {
                CavePassagePlan passage = passages[i];
                if (passage.FromChamberId < 0 || passage.FromChamberId >= chambers.Length ||
                    passage.ToChamberId < 0 || passage.ToChamberId >= chambers.Length)
                {
                    issue = CavePlanIssue.InvalidPassageEndpoint;
                    return false;
                }

                if (passage.FromChamberId == passage.ToChamberId)
                {
                    issue = CavePlanIssue.SelfPassage;
                    return false;
                }

                if (passage.Width <= 0 || passage.Height <= 0)
                {
                    issue = CavePlanIssue.InvalidPassageSize;
                    return false;
                }

                int a = math.min(passage.FromChamberId, passage.ToChamberId);
                int b = math.max(passage.FromChamberId, passage.ToChamberId);
                for (int other = 0; other < i; other++)
                {
                    int otherA = math.min(
                        passages[other].FromChamberId, passages[other].ToChamberId);
                    int otherB = math.max(
                        passages[other].FromChamberId, passages[other].ToChamberId);
                    if (a != otherA || b != otherB) continue;
                    issue = CavePlanIssue.DuplicatePassage;
                    return false;
                }
            }

            var adjacency = new List<int>[chambers.Length];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            for (int i = 0; i < passages.Length; i++)
            {
                CavePassagePlan passage = passages[i];
                adjacency[passage.FromChamberId].Add(passage.ToChamberId);
                adjacency[passage.ToChamberId].Add(passage.FromChamberId);
            }

            var visited = new bool[chambers.Length];
            var queue = new Queue<int>();
            queue.Enqueue(plan.EntryChamberId);
            visited[plan.EntryChamberId] = true;
            int visitedCount = 0;
            while (queue.Count > 0)
            {
                int chamber = queue.Dequeue();
                visitedCount++;
                List<int> neighbours = adjacency[chamber];
                for (int i = 0; i < neighbours.Count; i++)
                {
                    int next = neighbours[i];
                    if (visited[next]) continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            if (visitedCount != chambers.Length)
            {
                issue = CavePlanIssue.DisconnectedGraph;
                return false;
            }

            issue = CavePlanIssue.None;
            return true;
        }
    }
}
