using System.Collections;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Validation-only compatibility for the resumed Kentridge macro-world evidence drivers.
    /// The production ShowcaseWorld no longer exposes its former presentation-column helper, so
    /// the validation assembly derives the same source-readiness fact from current world state
    /// without adding scene policy back to the shared production API.
    /// </summary>
    internal static class KentridgeMacroWorldShowcaseReadinessExtensions
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo s_PendingFeatureRegionsField =
            typeof(ShowcaseWorld).GetField("_pendingFeatureRegions", InstancePrivate);
        private static readonly FieldInfo s_DeferredFeatureRegionsField =
            typeof(ShowcaseWorld).GetField("_deferredFeatureRegions", InstancePrivate);
        private static readonly FieldInfo s_FeatureBuildField =
            typeof(ShowcaseWorld).GetField("_featureBuild", InstancePrivate);
        private static readonly MethodInfo s_SurfaceLayerSpanMethod =
            typeof(ShowcaseWorld).GetMethod("SurfaceLayerSpan", InstancePrivate);

        public static bool IsPresentationColumnContentSettled(
            this ShowcaseWorld world,
            Vector3 presentationPoint)
        {
            if (world == null || s_SurfaceLayerSpanMethod == null) return false;

            int3 pointRegion = ShowcaseWorld.RegionAt(presentationPoint);
            if (!TryGetColumnSpan(world, pointRegion.x, pointRegion.z, out int minLayer, out int maxLayer))
                return false;

            // Current ShowcaseWorld folds authored feature footprints into SurfaceLayerSpan.
            // Requiring the complete returned span is deliberately conservative relative to the
            // historical helper: it may wait for an extra empty layer, but it cannot accept a
            // terrain-only column while a later authored feature publication is still pending.
            for (int ry = minLayer; ry <= maxLayer; ry++)
                if (!IsRegionContentSettled(world, new int3(pointRegion.x, ry, pointRegion.z)))
                    return false;

            if ((pointRegion.y < minLayer || pointRegion.y > maxLayer)
                && !IsRegionContentSettled(world, pointRegion))
                return false;

            return true;
        }

        private static bool TryGetColumnSpan(
            ShowcaseWorld world,
            int regionX,
            int regionZ,
            out int minLayer,
            out int maxLayer)
        {
            minLayer = 0;
            maxLayer = -1;
            try
            {
                object[] arguments = { regionX, regionZ, 0, 0 };
                s_SurfaceLayerSpanMethod.Invoke(world, arguments);
                if (!(arguments[2] is int min) || !(arguments[3] is int max) || max < min)
                    return false;
                minLayer = min;
                maxLayer = max;
                return true;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }

        private static bool IsRegionContentSettled(ShowcaseWorld world, int3 region)
        {
            if (!world.IsGenerated(region)) return false;
            if (ContainsRegion(s_PendingFeatureRegionsField, world, region)) return false;
            if (ContainsRegion(s_DeferredFeatureRegionsField, world, region)) return false;
            return !FeatureBuildTargetsRegion(s_FeatureBuildField?.GetValue(world), region);
        }

        private static bool ContainsRegion(FieldInfo field, ShowcaseWorld world, int3 region)
        {
            if (!(field?.GetValue(world) is IEnumerable values)) return false;
            foreach (object value in values)
                if (value is int3 candidate && candidate.Equals(region))
                    return true;
            return false;
        }

        private static bool FeatureBuildTargetsRegion(object featureBuild, int3 region)
        {
            if (featureBuild == null) return false;

            TypeInfo type = featureBuild.GetType().GetTypeInfo();
            PropertyInfo property = type.GetDeclaredProperty("RegionCoord");
            if (property?.GetValue(featureBuild) is int3 propertyRegion)
                return propertyRegion.Equals(region);

            FieldInfo field = type.GetDeclaredField("RegionCoord");
            return field?.GetValue(featureBuild) is int3 fieldRegion && fieldRegion.Equals(region);
        }
    }
}

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Legacy validation telemetry shape retained only so historical macro-world evidence can
    /// compile against the current renderer contract. Aggregate camera-visible counts remain
    /// observable; retired per-bounds index/source-step data is reported as unavailable (-1).
    /// </summary>
    internal readonly struct SurfaceBoundsCoverage
    {
        public readonly int VisibleChunkCount;
        public readonly int ReadyChunkCount;
        public readonly int ReadyIndexCount;
        public readonly int MinimumSourceStep;
        public readonly int MaximumSourceStep;

        public SurfaceBoundsCoverage(
            int visibleChunkCount,
            int readyChunkCount,
            int readyIndexCount,
            int minimumSourceStep,
            int maximumSourceStep)
        {
            VisibleChunkCount = visibleChunkCount;
            ReadyChunkCount = readyChunkCount;
            ReadyIndexCount = readyIndexCount;
            MinimumSourceStep = minimumSourceStep;
            MaximumSourceStep = maximumSourceStep;
        }
    }

    internal static class RenderingSurfaceCoverageDiagnostics
    {
        public static bool TryQueryVisibleSolidBounds(
            Bounds bounds,
            float voxelSize,
            out SurfaceBoundsCoverage coverage)
        {
            _ = bounds;
            _ = voxelSize;

            RenderingComposition.GetVoxelSurfaceCounts(out int visible, out _);
            RenderingComposition.TryGetSurfaceBuildStatus(
                out _,
                out _,
                out int resident,
                out _);

            coverage = new SurfaceBoundsCoverage(
                visible,
                resident,
                readyIndexCount: -1,
                minimumSourceStep: -1,
                maximumSourceStep: -1);

            // The current public renderer deliberately does not expose a caller-bounds query.
            // Returning false preserves that distinction instead of pretending aggregate metrics
            // are bounds-specific; the historical call sites use this value for telemetry only.
            return false;
        }
    }
}
