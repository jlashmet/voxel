using System;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    public enum MeshVoxelCaptureSubject : byte
    {
        Overall = 0,
        HeadHorns = 1,
        Wing = 2,
        FeetClaws = 3,
        Tail = 4,
    }

    public readonly struct MeshVoxelCaptureView
    {
        public readonly string Id;
        public readonly MeshVoxelCaptureSubject Subject;
        public readonly float3 ViewDirection;
        public readonly bool Elevated;

        public MeshVoxelCaptureView(
            string id,
            MeshVoxelCaptureSubject subject,
            float3 viewDirection,
            bool elevated = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Capture id is required.", nameof(id));
            if (!math.all(math.isfinite(viewDirection)) || math.lengthsq(viewDirection) < 1e-6f)
                throw new ArgumentOutOfRangeException(nameof(viewDirection));

            Id = id;
            Subject = subject;
            ViewDirection = math.normalize(viewDirection);
            Elevated = elevated;
        }
    }

    /// <summary>
    /// Semantic capture contract for matched source-mesh/voxel evidence. This contains no scene
    /// coordinates or model-specific anatomy bounds; the built-player composition resolves each
    /// subject target from the actual staged exhibit while preserving these required viewpoints.
    /// </summary>
    public static class MeshVoxelComparisonCapturePlan
    {
        public static MeshVoxelCaptureView[] CreateRequiredViews() => new[]
        {
            new MeshVoxelCaptureView("front", MeshVoxelCaptureSubject.Overall, new float3(0f, 0f, -1f)),
            new MeshVoxelCaptureView("side", MeshVoxelCaptureSubject.Overall, new float3(-1f, 0f, 0f)),
            new MeshVoxelCaptureView("rear", MeshVoxelCaptureSubject.Overall, new float3(0f, 0f, 1f)),
            new MeshVoxelCaptureView("front-three-quarter", MeshVoxelCaptureSubject.Overall, new float3(-1f, 0f, -1f)),
            new MeshVoxelCaptureView("rear-three-quarter", MeshVoxelCaptureSubject.Overall, new float3(1f, 0f, 1f)),
            new MeshVoxelCaptureView("elevated-top-three-quarter", MeshVoxelCaptureSubject.Overall, new float3(-1f, -0.65f, -1f), elevated: true),
            new MeshVoxelCaptureView("head-horns-closeup", MeshVoxelCaptureSubject.HeadHorns, new float3(-1f, -0.12f, -1f)),
            new MeshVoxelCaptureView("wing-closeup", MeshVoxelCaptureSubject.Wing, new float3(-1f, -0.18f, -0.35f)),
            new MeshVoxelCaptureView("feet-claws-closeup", MeshVoxelCaptureSubject.FeetClaws, new float3(-0.45f, -0.35f, -1f)),
            new MeshVoxelCaptureView("tail-closeup", MeshVoxelCaptureSubject.Tail, new float3(1f, -0.10f, 1f)),
        };
    }
}
