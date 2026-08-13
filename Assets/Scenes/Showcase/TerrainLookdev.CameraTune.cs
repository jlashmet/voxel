using UnityEngine;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void LateUpdate()
        {
            Camera camera = SceneCamera;
            camera.fieldOfView = 21.7f;
            camera.transform.position = new Vector3(-0.66f, 23.0f, -20.0f);
            camera.transform.LookAt(new Vector3(-0.59f, 2.20f, 12.15f));
        }
    }
}
