using System;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Produces the scheduler's per-frame worker visit order without allocating.
    ///
    /// Phase-9 workers own paged GPU results that may already be fence-complete. Poll those
    /// bounded completion consumers before ordinary admission and keep them visitable after the
    /// ordinary admission deadline has expired, so a ready GPU publication cannot be stranded
    /// behind unrelated CPU work. Round-robin order is preserved independently inside the
    /// completion and ordinary groups.
    /// </summary>
    internal static class SurfaceGpuCompletionPollOrder
    {
        internal const int PagedGpuCompletionPhase = 9;

        internal static bool CanVisit(int activeBuildPhase, double remainingBudgetMs) =>
            activeBuildPhase == PagedGpuCompletionPhase || remainingBudgetMs > 0.0;

        internal static int Build(int[] activeBuildPhases, int cursor, int[] destination)
        {
            if (activeBuildPhases == null)
                throw new ArgumentNullException(nameof(activeBuildPhases));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            int workerCount = activeBuildPhases.Length;
            if (destination.Length < workerCount)
                throw new ArgumentException(
                    "Completion-poll order storage must cover every worker.", nameof(destination));
            if (workerCount == 0) return 0;

            int start = cursor % workerCount;
            if (start < 0) start += workerCount;
            int written = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantPagedCompletion = pass == 0;
                for (int offset = 0; offset < workerCount; offset++)
                {
                    int index = (start + offset) % workerCount;
                    bool isPagedCompletion =
                        activeBuildPhases[index] == PagedGpuCompletionPhase;
                    if (isPagedCompletion != wantPagedCompletion) continue;
                    destination[written++] = index;
                }
            }
            return written;
        }
    }
}
