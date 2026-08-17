using System.Threading;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Narrow production diagnostics for the step-4 false-empty investigation.
    ///
    /// These counters do not participate in scheduling or rendering. They answer which lifecycle
    /// branch adjudicated an exact step-4 chunk after the ordinary four-voxel extractor completed:
    /// exact ownership, profile suppression, feature-preserving fallback, or final publication.
    /// The focused LOD fixture resets them before its scene run. While the false-empty
    /// investigation is active, an authoritative empty publication logs the current snapshot so
    /// the exact adjudication branch is preserved even if the fixture fails before formatting its
    /// final assertion message.
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
                long readyEmptyPublications)
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
            }

            public override string ToString() =>
                $"owned:{ExactOwnedSolidSnapshots}/unowned:{ExactUnownedSnapshots}/"
              + $"ownedProfiles:{ExactOwnedWithProfiles}/"
              + $"ordinaryNonEmpty:{OrdinaryNonEmptyOwned}/"
              + $"ordinaryEmpty:{OrdinaryEmptyOwnedNoProfiles}/"
              + $"ordinaryEmptyProfiles:{OrdinaryEmptyOwnedWithProfiles}/"
              + $"fallback:{FallbackScheduled}/nonEmpty:{FallbackCompletedNonEmpty}/"
              + $"empty:{FallbackCompletedEmpty}/published:{FallbackPublishedNonEmpty}/"
              + $"readyEmpty:{ReadyEmptyPublications}";
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
            Interlocked.Read(ref s_ReadyEmptyPublications));

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

        internal static void RecordReadyEmptyPublication()
        {
            Interlocked.Increment(ref s_ReadyEmptyPublications);
            UnityEngine.Debug.Log($"[Step4FalseEmptyLifecycle] {Current}");
        }
    }
}
