using UnityEngine;
using VoxelEngine.Rendering;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void Update()
        {
            if (!Application.isPlaying) return;
            VoxelRenderBridge.SunDirection = new Vector3(-0.24f, 0.96f, -0.24f).normalized;
        }
    }
}
