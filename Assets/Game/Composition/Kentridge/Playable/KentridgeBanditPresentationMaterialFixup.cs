using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// One-shot presentation repair for runtime primitive gear. GameObject.CreatePrimitive uses
    /// the built-in Standard material, which renders magenta under the project's URP player.
    /// Reuse the rigged character's shipped material/shader so gear stays pipeline-compatible.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class KentridgeBanditPresentationMaterialFixup : MonoBehaviour
    {
        private const string KentridgeSceneName = "KentridgePlayableSlice";
        private const string PlayerCameraName = "Kentridge Player Camera";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitialScene()
        {
            Install(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || scene.name != KentridgeSceneName) return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root.name != PlayerCameraName) continue;
                if (root.GetComponent<KentridgeBanditPresentationMaterialFixup>() == null)
                    root.AddComponent<KentridgeBanditPresentationMaterialFixup>();
                return;
            }
        }

        private void Start()
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject bandit = GameObject.Find("Forest Bandit " + (i + 1));
                if (bandit != null) FixBandit(bandit, i);
            }

            Destroy(this);
        }

        private static void FixBandit(GameObject bandit, int index)
        {
            Renderer[] renderers = bandit.GetComponentsInChildren<Renderer>(true);
            Material source = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (IsGear(renderer.gameObject.name)) continue;
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null) continue;
                source = renderer.sharedMaterial;
                break;
            }

            if (source == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsGear(renderer.gameObject.name)) continue;

                Material material = new Material(source)
                {
                    name = "Forest Bandit Gear Material"
                };
                Color color = GearColor(index, renderer.gameObject.name);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                renderer.sharedMaterial = material;
            }
        }

        private static bool IsGear(string name)
        {
            return name == "Emergency Body" ||
                   name == "Hood" ||
                   name == "Belt" ||
                   name == "Shoulder Strap" ||
                   name == "Pouch" ||
                   name == "Sword" ||
                   name == "Guard";
        }

        private static Color GearColor(int index, string part)
        {
            if (part == "Sword") return new Color(0.55f, 0.58f, 0.60f);
            if (part == "Belt" || part == "Shoulder Strap" || part == "Pouch" || part == "Guard")
                return new Color(0.11f, 0.07f, 0.04f);

            if (index == 0) return new Color(0.24f, 0.12f, 0.09f);
            if (index == 1) return new Color(0.13f, 0.20f, 0.12f);
            return new Color(0.16f, 0.15f, 0.18f);
        }
    }
}
