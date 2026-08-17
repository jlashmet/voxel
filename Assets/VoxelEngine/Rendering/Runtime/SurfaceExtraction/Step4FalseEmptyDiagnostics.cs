using System.Threading;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Narrow production diagnostics for the step-4 false-empty investigation.
    ///
    /// These counters do not participate in scheduling or rendering. They answer which lifecycle
    /// branch adjudicated an exact step-4 chunk after the ordinary four-voxel extractor completed:
    /// exact ownership, profile suppression, feature-preserving fallback, or final publication.
    ///
    /// Ready-empty publication also records the guard inputs from that exact build. This matters
    /// because a coarse chunk may be background-built before a later camera-band test resets the
    /// aggregate counters; the publication-time booleans cannot be reconstructed reliably from a
    /// later visibility snapshot.
    /// </summary>
    public static class Step4FalseEmptyDiagnostics
    {
        private static long s_ExactOwnedSolidSnapshots;
        private static long s_ExactUnownedSnapshots;
        private static long s_ExactOwnedWithProfiles;
        private static long s_OrdinaryNonEmptyOwned;
        private static long s_OrdinaryEmptyOwnedNoProfiles;
        private static long s_OrdinaryEmptyOwnedWithProfiles;
        private static long s_FallbackScheduled;
        private static long s_FallbackCompletedNonEmpty;
        private static long s_FallbackCompletedEmpty;
        private static long s_FallbackPublishedNonEmpty;
        private static long s_ReadyEmptyPublications;
        private static long s_ReadyEmptyOwnedSolid;
        private static long s_ReadyEmptyUnowned;
        private static long s_ReadyEmptyWithProfiles;
        private static long s_ReadyEmptyUsedFallback;

        public readonly struct Snapshot
        {
            public readonly long ExactOwnedSolidSnapshots;
            public readonly long ExactUnownedSnapshots;
            public readonly long ExactOwnedWithProfiles;
            public readonly long OrdinaryNonEmptyOwned;
            public readonly long OrdinaryEmptyOwnedNoProfiles;
            public readonly long OrdinaryEmptyOwnedWithProfiles;
            public readonly long FallbackScheduled;
            public readonly long FallbackCompletedNonEmpty;
            public readonly long FallbackCompletedEmpty;
            public readonly long FallbackPublishedNonEmpty;
            public readonly long ReadyEmptyPublications;
            public readonly long ReadyEmptyOwnedSolid;
            public readonly long ReadyEmptyUnowned;
            public readonly long ReadyEmptyWithProfiles;
            public readonly long ReadyEmptyUsedFallback;

            internal Snapshot(
                long exactOwnedSolidSnapshots,
                long exactUnownedSnapshots,
                long exactOwnedWithProfiles,
                long ordinaryNonEmptyOwned,
                long ordinaryEmptyOwnedNoProfiles,
                long ordinaryEmptyOwnedWithProfiles,
                long fallbackScheduled,
                long fallbackCompletedNonEmpty,
                long fallbackCompletedEmpty,
                long fallbackPublishedNonEmpty,
                long readyEmptyPublications,
                long readyEmptyOwnedSolid,
                long readyEmptyUnowned,
                long readyEmptyWithProfiles,
                long readyEmptyUsedFallback)
            {
                ExactOwnedSolidSnapshots = exactOwnedSolidSnapshots;
                ExactUnownedSnapshots = exactUnownedSnapshots;
                ExactOwnedWithProfiles = exactOwnedWithProfiles;
                OrdinaryNonEmptyOwned = ordinaryNonEmptyOwned;
                OrdinaryEmptyOwnedNoProfiles = ordinaryEmptyOwnedNoProfiles;
                OrdinaryEmptyOwnedWithProfiles = ordinaryEmptyOwnedWithProfiles;
                FallbackScheduled = fallbackScheduled;
                FallbackCompletedNonEmpty = fallbackCompletedNonEmpty;
                FallbackCompletedEmpty = fallbackCompletedEmpty;
                FallbackPublishedNonEmpty = fallbackPublishedNonEmpty;
                ReadyEmptyPublications = readyEmptyPublications;
                ReadyEmptyOwnedSolid = readyEmptyOwnedSolid;
                ReadyEmptyUnowned = readyEmptyUnowned;
                ReadyEmptyWithProfiles = readyEmptyWithProfiles;
                ReadyEmptyUsedFallback = readyEmptyUsedFallback;
            }

            public override string ToString() =>
                $"owned:{ExactOwnedSolidSnapshots}/unowned:{ExactUnownedSnapshots}/"
              + $"ownedProfiles:{ExactOwnedWithProfiles}/"
              + $"ordinaryNonEmpty:{OrdinaryNonEmptyOwned}/"
              + $"ordinaryEmpty:{OrdinaryEmptyOwnedNoProfiles}/"
              + $"ordinaryEmptyProfiles:{OrdinaryEmptyOwnedWithProfiles}/"
              + $"fallback:{FallbackScheduled}/nonEmpty:{FallbackCompletedNonEmpty}/"
              + $"empty:{FallbackCompletedEmpty}/published:{FallbackPublishedNonEmpty}/"
              + $"readyEmpty:{ReadyEmptyPublications}/"
              + $"readyEmptyOwned:{ReadyEmptyOwnedSolid}/"
              + $"readyEmptyUnowned:{ReadyEmptyUnowned}/"
              + $"readyEmptyProfiles:{ReadyEmptyWithProfiles}/"
              + $"readyEmptyUsedFallback:{ReadyEmptyUsedFallback}";
        }

        public static Snapshot Current => new(
            Interlocked.Read(ref s_ExactOwnedSolidSnapshots),
            Interlocked.Read(ref s_ExactUnownedSnapshots),
            Interlocked.Read(ref s_ExactOwnedWithProfiles),
            Interlocked.Read(ref s_OrdinaryNonEmptyOwned),
            Interlocked.Read(ref s_OrdinaryEmptyOwnedNoProfiles),
            Interlocked.Read(ref s_OrdinaryEmptyOwnedWithProfiles),
            Interlocked.Read(ref s_FallbackScheduled),
            Interlocked.Read(ref s_FallbackCompletedNonEmpty),
            Interlocked.Read(ref s_FallbackCompletedEmpty),
            Interlocked.Read(ref s_FallbackPublishedNonEmpty),
            Interlocked.Read(ref s_ReadyEmptyPublications),
            Interlocked.Read(ref s_ReadyEmptyOwnedSolid),
            Interlocked.Read(ref s_ReadyEmptyUnowned),
            Interlocked.Read(ref s_ReadyEmptyWithProfiles),
            Interlocked.Read(ref s_ReadyEmptyUsedFallback));

        public static void Reset()
        {
            Interlocked.Exchange(ref s_ExactOwnedSolidSnapshots, 0);
            Interlocked.Exchange(ref s_ExactUnownedSnapshots, 0);
            Interlocked.Exchange(ref s_ExactOwnedWithProfiles, 0);
            Interlocked.Exchange(ref s_OrdinaryNonEmptyOwned, 0);
            Interlocked.Exchange(ref s_OrdinaryEmptyOwnedNoProfiles, 0);
            Interlocked.Exchange(ref s_OrdinaryEmptyOwnedWithProfiles, 0);
            Interlocked.Exchange(ref s_FallbackScheduled, 0);
            Interlocked.Exchange(ref s_FallbackCompletedNonEmpty, 0);
            Interlocked.Exchange(ref s_FallbackCompletedEmpty, 0);
            Interlocked.Exchange(ref s_FallbackPublishedNonEmpty, 0);
            Interlocked.Exchange(ref s_ReadyEmptyPublications, 0);
            Interlocked.Exchange(ref s_ReadyEmptyOwnedSolid, 0);
            Interlocked.Exchange(ref s_ReadyEmptyUnowned, 0);
            Interlocked.Exchange(ref s_ReadyEmptyWithProfiles, 0);
            Interlocked.Exchange(ref s_ReadyEmptyUsedFallback, 0);
        }

        internal static void RecordExactClassification(bool hasOwnedSolid, bool hasProfiles)
        {
            if (hasOwnedSolid)
            {
                Interlocked.Increment(ref s_ExactOwnedSolidSnapshots);
                if (hasProfiles) Interlocked.Increment(ref s_ExactOwnedWithProfiles);
                return;
            }
            Interlocked.Increment(ref s_ExactUnownedSnapshots);
        }

        internal static void RecordOrdinaryResult(bool hasOwnedSolid, bool hasProfiles,
                                                  int vertexCount, int indexCount)
        {
            if (!hasOwnedSolid) return;
            if (vertexCount != 0 || indexCount != 0)
            {
                Interlocked.Increment(ref s_OrdinaryNonEmptyOwned);
                return;
            }
            if (hasProfiles)
                Interlocked.Increment(ref s_OrdinaryEmptyOwnedWithProfiles);
            else
                Interlocked.Increment(ref s_OrdinaryEmptyOwnedNoProfiles);
        }

        internal static void RecordFallbackScheduled() =>
            Interlocked.Increment(ref s_FallbackScheduled);

        internal static void RecordFallbackCompleted(bool hasGeometry)
        {
            if (hasGeometry)
                Interlocked.Increment(ref s_FallbackCompletedNonEmpty);
            else
                Interlocked.Increment(ref s_FallbackCompletedEmpty);
        }

        internal static void RecordFallbackPublished() =>
            Interlocked.Increment(ref s_FallbackPublishedNonEmpty);

        internal static void RecordReadyEmptyPublication(
            bool hasOwnedSolid, bool hasProfiles, bool usedFallback)
        {
            Interlocked.Increment(ref s_ReadyEmptyPublications);
            if (hasOwnedSolid)
                Interlocked.Increment(ref s_ReadyEmptyOwnedSolid);
            else
                Interlocked.Increment(ref s_ReadyEmptyUnowned);
            if (hasProfiles)
                Interlocked.Increment(ref s_ReadyEmptyWithProfiles);
            if (usedFallback)
                Interlocked.Increment(ref s_ReadyEmptyUsedFallback);

            UnityEngine.Debug.Log(
                $"[Step4FalseEmptyLifecycle] publication owned={hasOwnedSolid} "
              + $"profiles={hasProfiles} usedFallback={usedFallback} {Current}");
        }
    }
}
