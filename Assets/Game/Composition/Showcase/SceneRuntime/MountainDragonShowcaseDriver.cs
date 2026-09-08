using Game.Cutscenes.Presentation;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-facing adapter for the reusable mountain encounter. It only converts the showcase
    /// transform into authored voxel coordinates and binds shared cutscene presentation; proximity,
    /// story decisions, and UI behavior stay in WorldBuilder/Story/Campaign/Cutscenes.
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
            CutsceneDialogueOverlay overlay = GetComponent<CutsceneDialogueOverlay>();
            if (overlay == null) overlay = gameObject.AddComponent<CutsceneDialogueOverlay>();
            overlay.Bind(_encounter);
        }

        private void Update()
        {
            if (_encounter == null) return;
            int x = Mathf.FloorToInt(transform.position.x / ShowcaseWorld.VoxelSize);
            int z = Mathf.FloorToInt(transform.position.z / ShowcaseWorld.VoxelSize);
            int elapsedMilliseconds = Mathf.Max(0, Mathf.RoundToInt(Time.deltaTime * 1000f));
            _encounter.Update(x, z, elapsedMilliseconds);
        }
    }
}
