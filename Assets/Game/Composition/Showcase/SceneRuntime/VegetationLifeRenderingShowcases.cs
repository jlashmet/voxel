using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Minimal presentation shell shared by lightweight subsystem showcases. Created only in play
    /// mode so the scenes stay tiny and tests do not need the full voxel world.
    /// </summary>
    internal static class SubsystemRenderingShowcaseEnvironment
    {
        public static void Ensure(Transform root)
        {
            EnsureGround(root);
            EnsureWall(root);
            EnsureCamera(root);
            EnsureLight(root);
        }

        private static void EnsureGround(Transform root)
        {
            if (root.Find("Showcase Ground") != null) return;
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Showcase Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(0f, -0.02f, 6f);
            ground.transform.localScale = new Vector3(2.6f, 1f, 2.2f);
        }

        private static void EnsureWall(Transform root)
        {
            if (root.Find("Showcase Vine Wall") != null) return;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Showcase Vine Wall";
            wall.transform.SetParent(root, false);
            wall.transform.localPosition = new Vector3(0f, 2.0f, 10f);
            wall.transform.localScale = new Vector3(18f, 4f, 0.22f);
        }

        private static void EnsureCamera(Transform root)
        {
            if (Camera.main != null || root.Find("Showcase Camera") != null) return;
            GameObject cameraObject = new GameObject("Showcase Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.68f, 0.79f, 0.88f, 1f);
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            cameraObject.transform.position = new Vector3(0f, 8.5f, -16f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.4f, 6f));
        }

        private static void EnsureLight(Transform root)
        {
            if (root.Find("Showcase Sun") != null) return;
            GameObject lightObject = new GameObject("Showcase Sun");
            lightObject.transform.SetParent(root, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }
    }
}
