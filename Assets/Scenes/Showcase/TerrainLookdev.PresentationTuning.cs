using UnityEngine;
using VoxelEngine.Rendering;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void OnPreCull()
        {
            VoxelPresentationCatalogue.MaterialAlbedo[22] = new Vector4(0.25f, 0.32f, 0.13f, 1f);
        }
    }
}
