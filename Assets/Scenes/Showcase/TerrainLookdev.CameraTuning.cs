using UnityEngine;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Camera camera = SceneCamera;
            camera.fieldOfView = 28f;
            camera.transform.position = new Vector3(-0.25f, 14.0f, -17.0f);
            camera.transform.LookAt(new Vector3(0.10f, 4.0f, 25.0f));
        }
    }
}
