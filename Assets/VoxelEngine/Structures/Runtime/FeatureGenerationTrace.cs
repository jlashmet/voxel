using System;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Low-volume diagnostics for tracing authored structure instances through the streaming
    /// feature-generation pipeline. Enabled for generic SceneIssue diagnostic replays or when a
    /// player is launched explicitly with <c>--feature-generation-trace</c>; normal players stay silent.
    /// </summary>
    internal static class FeatureGenerationTrace
    {
        private const string CommandLineFlag = "--feature-generation-trace";
        private const string SceneIssueFlag = "-voxel-scene-issue";
        private const string Prefix = "FEATUREGEN_TRACE";
        private static readonly bool s_enabled = ResolveEnabled();

        public static bool ShouldTrace(FeatureKind kind) =>
            s_enabled && kind == FeatureKind.Structure;

        public static void Candidate(
            int3 regionCoord,
            int definitionId,
            FeatureKind kind,
            int3 position,
            int3 footprint)
        {
            if (!ShouldTrace(kind)) return;
            Debug.Log(
                $"{Prefix} candidate region={Format(regionCoord)} definition={definitionId} " +
                $"kind={kind} position={Format(position)} footprint={Format(footprint)}");
        }

        public static void EvaluationRejected(
            int3 regionCoord,
            int definitionId,
            FeatureKind kind,
            int3 position,
            EvaluationResult result)
        {
            if (!ShouldTrace(kind)) return;
            Debug.Log(
                $"{Prefix} rejected region={Format(regionCoord)} definition={definitionId} " +
                $"kind={kind} position={Format(position)} evaluation={result}");
        }

        public static void EvaluationAccepted(
            int3 regionCoord,
            int definitionId,
            FeatureKind kind,
            int3 position,
            int primitiveCount)
        {
            if (!ShouldTrace(kind)) return;
            Debug.Log(
                $"{Prefix} accepted region={Format(regionCoord)} definition={definitionId} " +
                $"kind={kind} position={Format(position)} primitives={primitiveCount}");
        }

        public static void Completed(
            int3 regionCoord,
            int definitionId,
            FeatureKind kind,
            int3 position,
            bool rasterisedAny,
            int voxelsWritten)
        {
            if (!ShouldTrace(kind)) return;
            Debug.Log(
                $"{Prefix} completed region={Format(regionCoord)} definition={definitionId} " +
                $"kind={kind} position={Format(position)} rasterised={rasterisedAny} voxels={voxelsWritten}");
        }

        private static bool ResolveEnabled()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], CommandLineFlag, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], SceneIssueFlag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string Format(int3 value) =>
            $"({value.x},{value.y},{value.z})";
    }
}
