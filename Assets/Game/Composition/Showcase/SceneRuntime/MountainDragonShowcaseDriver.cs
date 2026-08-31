using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-facing adapter for the reusable mountain encounter. It only converts the showcase
    /// transform into authored voxel coordinates and presents the cutscene cue; proximity and
    /// story decisions stay in WorldBuilder/Story/Campaign.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MountainDragonShowcaseDriver : MonoBehaviour
    {
        private const uint ShowcaseSeed = 0x5EED1234;
        private MountainDragonEncounterRuntime _encounter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToShowcase()
        {
            VoxelShowcase[] showcases = Object.FindObjectsByType<VoxelShowcase>(FindObjectsSortMode.None);
            for (int i = 0; i < showcases.Length; i++)
            {
                VoxelShowcase showcase = showcases[i];
                if (showcase.gameObject.scene.name != "VoxelShowcase") continue;
                if (showcase.GetComponent<MountainDragonShowcaseDriver>() == null)
                    showcase.gameObject.AddComponent<MountainDragonShowcaseDriver>();
            }
        }

        private void Awake()
        {
            _encounter = new MountainDragonEncounterRuntime(ShowcaseSeed);
        }

        private void Update()
        {
            if (_encounter == null) return;
            int x = Mathf.FloorToInt(transform.position.x / ShowcaseWorld.VoxelSize);
            int z = Mathf.FloorToInt(transform.position.z / ShowcaseWorld.VoxelSize);
            int elapsedMilliseconds = Mathf.Max(0, Mathf.RoundToInt(Time.deltaTime * 1000f));
            _encounter.Update(x, z, elapsedMilliseconds);
        }

        private void OnGUI()
        {
            if (_encounter == null || string.IsNullOrEmpty(_encounter.ActiveDialogue)) return;

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            GUI.Box(
                new Rect(Screen.width * 0.5f - 220f, Screen.height - 180f, 440f, 64f),
                _encounter.ActiveDialogue,
                style);
        }
    }
}
