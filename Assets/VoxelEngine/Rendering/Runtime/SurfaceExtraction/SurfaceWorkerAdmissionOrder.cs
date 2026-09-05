using System;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Produces the scheduler's per-frame worker visit order without allocating.
    ///
    /// Existing builds own finite CPU jobs, GPU extraction slots and publication state. They must
    /// be serviced before a renderer-wide deadline is spent admitting unrelated new work, or an
    /// asynchronous completion can sit ready while its slot remains occupied for many frames.
    /// Round-robin order is preserved independently inside the active and inactive groups.
    /// </summary>
    internal static class SurfaceWorkerAdmissionOrder
    {
        internal static int Build(bool[] activeWorkers, int cursor, int[] destination)
        {
            if (activeWorkers == null) throw new ArgumentNullException(nameof(activeWorkers));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int workerCount = activeWorkers.Length;
            if (destination.Length < workerCount)
                throw new ArgumentException(
                    "Admission-order storage must cover every worker.", nameof(destination));
            if (workerCount == 0) return 0;

            int start = cursor % workerCount;
            if (start < 0) start += workerCount;
            int written = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantActive = pass == 0;
                for (int offset = 0; offset < workerCount; offset++)
                {
                    int index = (start + offset) % workerCount;
                    if (activeWorkers[index] != wantActive) continue;
                    destination[written++] = index;
                }
            }
            return written;
        }
    }
}
