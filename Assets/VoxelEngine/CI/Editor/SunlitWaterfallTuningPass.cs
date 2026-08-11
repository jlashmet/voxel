using UnityEngine;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallTuningPass
    {
        private static bool _applied;

        public static void Apply(Camera camera)
        {
            if (_applied || camera == null) return;
            _applied = true;

            GameObject root = GameObject.Find("Sunlit Waterfall Target Scene");
            if (root == null) return;

            Vector3 origin = ResolveOrigin(root.transform);

            // Rounded terrace caps currently use the reverse winding from the side shell. Make
            // those painterly top surfaces two-sided for the lookdev shot, and give them a tiny
            // self-lit lift so the lawns stay bright under the warm directional light.
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                string n = r.gameObject.name;
                if (n.Contains(" turf"))
                {
                    if (r.sharedMaterial != null)
                    {
                        r.sharedMaterial.SetFloat("_Cull", 0f);
                        if (r.sharedMaterial.HasProperty("_BaseColor") && r.sharedMaterial.HasProperty("_EmissionColor"))
                        {
                            Color c = r.sharedMaterial.GetColor("_BaseColor");
                            r.sharedMaterial.SetColor("_EmissionColor", c * 0.06f);
                        }
                    }
                }

                if (n.Contains(" cliff"))
                {
                    Vector3 s = r.transform.localScale;
                    r.transform.localScale = new Vector3(s.x, s.y * 0.62f, s.z);
                }
            }

            // More top-down visibility matches the reference's readable garden terraces.
            camera.fieldOfView = 35f;
            camera.transform.position = origin + new Vector3(0.45f, 6.05f, -23.6f);
            camera.transform.LookAt(origin + new Vector3(-0.25f, 2.65f, 4.3f));

            // Push the landmark into atmospheric distance rather than letting it dominate the
            // right edge. The castle remains fully 3D; this is only composition scaling.
            Vector3 oldAnchor = origin + new Vector3(8.8f, 7.8f, 22.5f);
            Vector3 newAnchor = origin + new Vector3(8.3f, 7.2f, 29.0f);
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                string n = t.name;
                if (n.Contains("Distant castle hill") || n.Contains("Distant tower") ||
                    n.Contains("Castle spire") || n.Contains("Distant castle keep"))
                {
                    Vector3 offset = t.position - oldAnchor;
                    t.position = newAnchor + offset * 0.72f;
                    t.localScale *= 0.72f;
                }
            }
        }

        private static Vector3 ResolveOrigin(Transform root)
        {
            Transform hero = Find(root, "Hero terrace turf");
            if (hero != null) return hero.position - new Vector3(0f, 0.215f, -3.7f);
            return Vector3.zero;
        }

        private static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
            return null;
        }
    }
}
