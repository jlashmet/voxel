using Game.Composition.WorldObjects.Runtime;
using Game.Structures.Runtime;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-only host for the dedicated interaction vocabulary gallery. Gameplay semantics stay in WorldObject;
    /// this component only loads authored descriptors and adds lightweight gallery dressing/labels.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Interactables Showcase Scene")]
    [DisallowMultipleComponent]
    public sealed class InteractablesShowcaseScene : MonoBehaviour
    {
        public const string SceneName = "InteractablesShowcase";
        public const uint DefaultSeed = 0x1A7E2AC7u;

        [SerializeField] private uint m_Seed = DefaultSeed;
        [SerializeField] private bool m_CreateGalleryDressing = true;

        private WorldObjectSceneRegistry _registry;
        private GameObject _galleryRoot;

        public WorldObjectSceneRegistry Registry => _registry;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            _ = WorldObjectRuntimeBootstrap.Current;
            _registry = new WorldObjectSceneRegistry();
            var authoring = new WorldObjectAuthoringSession(m_Seed, ExplorationInteractablesSecretsShowcase.ParentId);
            ExplorationInteractablesSecretsShowcase.Author(authoring, ExplorationInteractablesSecretsShowcase.Origin);
            _registry.LoadAuthored(
                ExplorationInteractablesSecretsShowcase.ParentId,
                authoring.BuildObjects(),
                authoring.BuildConnections());

            if (m_CreateGalleryDressing)
                BuildGalleryDressing();
        }

        private void OnDisable()
        {
            _registry?.Unload(ExplorationInteractablesSecretsShowcase.ParentId);
            _registry = null;
            if (_galleryRoot != null)
                Destroy(_galleryRoot);
            _galleryRoot = null;
        }

        private void BuildGalleryDressing()
        {
            _galleryRoot = new GameObject("Gallery Dressing");
            _galleryRoot.transform.SetParent(transform, false);

            CreateBlock("Floor", new Vector3(36f, 0f, 27f), new Vector3(76f, 1f, 58f));
            CreateBlock("Row Divider A", new Vector3(36f, 0.55f, 14f), new Vector3(76f, 0.15f, 0.35f));
            CreateBlock("Row Divider B", new Vector3(36f, 0.55f, 33f), new Vector3(76f, 0.15f, 0.35f));

            CreateLabel("Legend", new Vector3(36f, 12f, -2f),
                "INTERACTABLES + SECRETS\nPrimary: use / toggle    Secondary: lock / unlock    Attack: break\nWalk onto plates / proximity bays\nDoors | Locked Door | Timed Gate | Portcullis | Trapdoor | Elevator | Drawbridge\nSecrets: Breakable Nook | Lever Reveal | Concealed Passage", 0.52f);
            CreateLabel("Direct Bay", new Vector3(25f, 1.3f, 0f),
                "DIRECT: DOOR   LOCKED   TIMED GATE   PORTCULLIS", 0.36f);
            CreateLabel("Movement Bay", new Vector3(28f, 1.3f, 16f),
                "MOVEMENT: TRAPDOOR   ELEVATOR   DRAWBRIDGE   PRESSURE   PROXIMITY", 0.34f);
            CreateLabel("Secrets Bay", new Vector3(27f, 1.3f, 36f),
                "SECRETS: BREAK WALL   PULL LEVER   FIND CONCEALED PLATE", 0.36f);
        }

        private void CreateBlock(string name, Vector3 position, Vector3 scale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(_galleryRoot.transform, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
        }

        private void CreateLabel(string name, Vector3 position, string text, float characterSize)
        {
            var label = new GameObject(name);
            label.transform.SetParent(_galleryRoot.transform, false);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
        }
    }
}
