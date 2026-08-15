namespace VoxelEngine.Rendering.Api
{
    /// <summary>
    /// Stable presentation-facing timing snapshot. Runtime extraction/cache types stay private to
    /// Rendering.Runtime; overlays and application diagnostics consume only these scalar values.
    /// </summary>
    public readonly struct SurfaceTimingDiagnostics
    {
        public readonly double FrameP95Ms;
        public readonly double DiscoveryP95Ms;
        public readonly double SnapshotP95Ms;
        public readonly double TopologyCompactP95Ms;
        public readonly double FacetedMergeP95Ms;
        public readonly double UploadP95Ms;
        public readonly double QueueLatencyP95Ms;

        public SurfaceTimingDiagnostics(
            double frameP95Ms,
            double discoveryP95Ms,
            double snapshotP95Ms,
            double topologyCompactP95Ms,
            double facetedMergeP95Ms,
            double uploadP95Ms,
            double queueLatencyP95Ms)
        {
            FrameP95Ms = frameP95Ms;
            DiscoveryP95Ms = discoveryP95Ms;
            SnapshotP95Ms = snapshotP95Ms;
            TopologyCompactP95Ms = topologyCompactP95Ms;
            FacetedMergeP95Ms = facetedMergeP95Ms;
            UploadP95Ms = uploadP95Ms;
            QueueLatencyP95Ms = queueLatencyP95Ms;
        }
    }
}
