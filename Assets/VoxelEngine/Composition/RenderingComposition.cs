using UnityEngine;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Rendering-specific application wiring. Presentation policy stays with scene/application
    /// code while concrete renderer catalogues remain private to Rendering.Runtime.
    /// </summary>
    public static class RenderingComposition
    {
        /// <summary>
        /// Returns the renderer's authoritative albedo for a semantic voxel material.
        /// Far-field/application presentation can stay colour-consistent with the near field
        /// without taking a direct dependency on Rendering.Runtime.
        /// </summary>
        public static Vector4 GetMaterialAlbedo(byte material) =>
            VoxelPresentationCatalogue.MaterialAlbedo[material];
    }
}
