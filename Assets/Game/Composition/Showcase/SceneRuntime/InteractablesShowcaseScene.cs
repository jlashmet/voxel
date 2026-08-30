using Game.Composition.WorldObjects.Runtime;
using Game.Structures.Runtime;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-only host for the dedicated interaction vocabulary gallery. Gameplay semantics stay in WorldObject;
    /// this component only loads authored descriptors, exposes a deterministic developer reset, and adds dressing.
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
            LoadFreshShowcase();
            if (m_CreateGalleryDressing) BuildGalleryDressing();
        }

        private void OnDisable()
        {
            _registry?.Unload(ExplorationInteractablesSecretsShowcase.ParentId);
            _registry = null;
            if (_galleryRoot != null) Destroy(_galleryRoot);
            _galleryRoot = null;
        }

        /// <summary>Deterministic replay path used by validation tooling and developer controls.</summary>
        public void ResetShowcase()
        {
            if (!Application.isPlaying) return;
            _registry?.Unload(ExplorationInteractablesSecretsShowcase.ParentId);
            _registry = null;
            LoadFreshShowcase();
        }

        private void LoadFreshShowcase()
        {
            _registry = new WorldObjectSceneRegistry();
            var authoring = new WorldObjectAuthoringSession(m_Seed, ExplorationInteractablesSecretsShowcase.ParentId);
            ExplorationInteractablesSecretsShowcase.Author(authoring, ExplorationInteractablesSecretsShowcase.Origin);
            _registry.LoadAuthored(ExplorationInteractablesSecretsShowcase.ParentId,
                authoring.BuildObjects(), authoring.BuildConnections());
        }

        private void BuildGalleryDressing()
        {
            _galleryRoot = new GameObject("Gallery Dressing");
            _galleryRoot.transform.SetParent(transform, false);

            CreateBlock("Floor", new Vector3(36f, 0f, 28f), new Vector3(76f, 1f, 60f));
            CreateBlock("Row Divider A", new Vector3(36f, 0.55f, 16f), new Vector3(76f, 0.15f, 0.35f));
            CreateBlock("Row Divider B", new Vector3(36f, 0.55f, 34f), new Vector3(76f, 0.15f, 0.35f));

            CreateLabel("Legend", new Vector3(36f, 12f, -2f),
                "INTERACTABLES + SECRETS\nShared sources: LEVER / BUTTON / PRESSURE PLATE\nMechanisms: DOOR / TRAPDOOR / GATE / PORTCULLIS / ELEVATOR / DRAWBRIDGE / SECRET PANEL\nReset: deterministic runtime reset path available on scene host", 0.46f);
            CreateLabel("Direct Bay", new Vector3(30f, 1.3f, 0f),
                "DIRECT + LINKED: DOORS   TRAPDOOR   PRESSURE   PORTCULLIS", 0.34f);
            CreateLabel("Movement Bay", new Vector3(28f, 1.3f, 18f),
                "MOVEMENT: ELEVATOR   DRAWBRIDGE LEVER   VISIBLE BUTTON -> GATE", 0.34f);
            CreateLabel("Secrets Bay", new Vector3(32f, 1.3f, 36f),
                "SECRETS: HIDDEN BUTTON -> FALSE WALL   ELEVATOR HIGH PLACE   REMOTE LEVER ROUTE", 0.32f);
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
