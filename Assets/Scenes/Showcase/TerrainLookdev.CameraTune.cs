using UnityEngine;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void LateUpdate()
        {
            Camera camera = SceneCamera;
            camera.fieldOfView = 21.7f;
            camera.transform.position = new Vector3(-0.59f, 23.10f, -20.0f);
            camera.transform.LookAt(new Vector3(-0.52f, 2.30f, 12.15f));
        }
    }
}
