using System;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>Composition wiring for nonresident semantic structure presentation capture.</summary>
    public static class StructurePresentationComposition
    {
        public static IStructurePresentationCaptureSession CreateCaptureSession(
            Func<int, int, int, byte> baselineMaterial = null) =>
            new StructurePresentationCaptureSession(baselineMaterial);
    }
}
