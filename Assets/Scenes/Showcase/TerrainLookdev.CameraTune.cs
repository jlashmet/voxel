using UnityEngine;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private void LateUpdate()
        {
            Camera camera = SceneCamera;

            // The old 21.7 degree telephoto framing compressed the valley into a top-down map.
            // A moderately wider lens, lower camera and farther look target restore the strong
            // foreground-to-distant-valley perspective visible in the reference.
            camera.fieldOfView = 29.0f;
            camera.transform.position = new Vector3(-0.7f, 18.8f, -18.5f);
            camera.transform.LookAt(new Vector3(-0.1f, 2.4f, 18.5f));
        }
    }
}
