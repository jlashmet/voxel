using Game.Composition.WorldObjects.Runtime;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-only host for the dedicated interaction vocabulary gallery. Gameplay semantics stay in WorldObject;
    /// this component loads authored descriptors, translates showcase input into semantic interactions, and adds
    /// validation dressing. It does not own mechanism state or source-target behavior.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Interactables Showcase Scene")]
    [DisallowMultipleComponent]
    public sealed class InteractablesShowcaseScene : MonoBehaviour
    {
        public const string SceneName = "InteractablesShowcase";
        public const uint DefaultSeed = 0x1A7E2AC7u;
        private const float GalleryScale = UnityWorldObjectPresentationSink.DefaultWorldUnitsPerVoxel;

        private static readonly SecretCandidateId BookshelfSecret = new SecretCandidateId("showcase.bookshelf-passage");
        private static readonly SecretCandidateId ElevatedSecret = new SecretCandidateId("showcase.elevator-high-place");
        private static readonly SecretCandidateId RemoteSecret = new SecretCandidateId("showcase.remote-lever-route");

        [SerializeField] private uint m_Seed = DefaultSeed;
        [SerializeField] private bool m_CreateGalleryDressing = true;

        private WorldObjectSceneRegistry _registry;
        private WorldObjectGeneratedScene _scene;
        private readonly SecretDiscoveryState _discoveries = new SecretDiscoveryState();
        private GameObject _galleryRoot;
        private string _status = "Ready. Click a visible mechanism or source.";

        public WorldObjectSceneRegistry Registry => _registry;
        public SecretDiscoveryState Discoveries => _discoveries;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _ = WorldObjectRuntimeBootstrap.Current;
            ConfigureCamera();
            LoadFreshShowcase();
            if (m_CreateGalleryDressing) BuildGalleryDressing();
            Debug.Log($"INTERACTABLES_SHOWCASE_READY objects={_scene.Objects.Length} connections={_scene.Connections.Length}");
        }

        private void Update()
        {
            if (!Application.isPlaying || _scene == null) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetShowcase();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
                RevealBookshelfControl();

            if (Input.GetMouseButtonDown(0))
                InteractAtPointer(false);
            else if (Input.GetMouseButtonDown(1))
                InteractAtPointer(true);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            GUI.Box(new Rect(16f, 16f, 760f, 92f),
                "INTERACTABLES + SECRETS SHOWCASE\n" +
                "LMB: primary / plate enter    RMB: secondary / plate exit    F: inspect bookshelf fixture    R: reset\n" +
                "Hidden controls have no visible/clickable affordance until explicitly discovered.  " + _status);
        }

        private void OnDisable()
        {
            _registry?.Unload(ExplorationInteractablesSecretsShowcase.ParentId);
            _registry = null;
            _scene = null;
            if (_galleryRoot != null) Destroy(_galleryRoot);
            _galleryRoot = null;
        }

        /// <summary>Deterministic replay path used by validation tooling and developer controls.</summary>
        public void ResetShowcase()
        {
            if (!Application.isPlaying) return;
            _registry?.Unload(ExplorationInteractablesSecretsShowcase.ParentId);
            _registry = null;
            _scene = null;
            _discoveries.Reset();
            LoadFreshShowcase();
            _status = "Reset to deterministic initial state.";
        }

        private void LoadFreshShowcase()
        {
            _registry = new WorldObjectSceneRegistry();
            var authoring = new WorldObjectAuthoringSession(m_Seed, ExplorationInteractablesSecretsShowcase.ParentId);
            ExplorationInteractablesSecretsShowcase.Author(authoring, ExplorationInteractablesSecretsShowcase.Origin);
            _scene = _registry.LoadAuthored(ExplorationInteractablesSecretsShowcase.ParentId,
                authoring.BuildObjects(), authoring.BuildConnections());
        }

        private void InteractAtPointer(bool secondary)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                _status = "No MainCamera is available.";
                return;
            }

            if (!Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100f))
            {
                _status = "Nothing interactable under pointer.";
                return;
            }

            UnityWorldObjectProxyIdentity identity = hit.collider.GetComponentInParent<UnityWorldObjectProxyIdentity>();
            if (identity == null || !identity.InteractionEnabled)
            {
                _status = "That surface has no visible interaction affordance.";
                return;
            }

            if (!_scene.Runtime.TryResolve(identity.Id, out WorldObjectResolvedState current))
            {
                _status = "World-object runtime could not resolve the picked proxy.";
                return;
            }

            WorldObjectInteraction interaction;
            if (current.Descriptor.Kind == WorldObjectKind.PressurePlate)
                interaction = secondary ? WorldObjectInteraction.Exit : WorldObjectInteraction.Enter;
            else
                interaction = secondary ? WorldObjectInteraction.Secondary : WorldObjectInteraction.Primary;

            if (!_scene.Runtime.TryInteract(identity.Id, interaction, out _))
            {
                _status = $"{current.Descriptor.Kind} rejected {interaction}.";
                return;
            }

            CreditSecretIfReached(current.Descriptor.LocalKey);
            _status = $"{current.Descriptor.Kind}: {interaction} applied through shared runtime.";
        }

        private void RevealBookshelfControl()
        {
            WorldObjectId hiddenButton = ExplorationInteractablesSecretsShowcase.Id(
                m_Seed, ExplorationInteractablesSecretsShowcase.HiddenBookshelfButtonKey);
            if (!_scene.Runtime.TryResolve(hiddenButton, out WorldObjectResolvedState current)) return;
            if ((current.State & WorldObjectStateFlags.Hidden) == 0)
            {
                _status = "Bookshelf control already discovered; click it to open the passage.";
                return;
            }

            if (_scene.Runtime.TryApply(hiddenButton, WorldObjectAction.Reveal))
                _status = "Bookshelf fixture inspected: concealed button revealed. No secret credit awarded yet.";
        }

        private void CreditSecretIfReached(uint localKey)
        {
            SecretCandidateId secret;
            if (localKey == ExplorationInteractablesSecretsShowcase.BookshelfSecretMarkerKey)
                secret = BookshelfSecret;
            else if (localKey == ExplorationInteractablesSecretsShowcase.ElevatedSecretKey)
                secret = ElevatedSecret;
            else if (localKey == ExplorationInteractablesSecretsShowcase.SecretRouteMarkerKey)
                secret = RemoteSecret;
            else
                return;

            bool first = _discoveries.TryDiscover(secret);
            _status = first
                ? $"Secret discovered: {secret.Id} (canonical credit recorded once)."
                : $"Secret revisited: {secret.Id} (no duplicate credit).";
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.transform.position = new Vector3(3.6f, 4.4f, -6.6f);
            camera.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
        }

        private void BuildGalleryDressing()
        {
            _galleryRoot = new GameObject("Gallery Dressing");
            _galleryRoot.transform.SetParent(transform, false);

            CreateBlock("Floor", new Vector3(36f, 0f, 28f), new Vector3(76f, 1f, 60f));
            CreateBlock("Row Divider A", new Vector3(36f, 0.55f, 16f), new Vector3(76f, 0.15f, 0.35f));
            CreateBlock("Row Divider B", new Vector3(36f, 0.55f, 34f), new Vector3(76f, 0.15f, 0.35f));

            // Keep verification labels short and local to their stations. The prior centered bay-long copy crossed
            // most of the playable geometry in real-player captures and made a dedicated scene read like debug UI.
            CreateLabel("Direct Bay", new Vector3(4f, 15f, 3f), "DIRECT + LINKED", 0.22f);
            CreateLabel("Normal Door Label", new Vector3(6f, 12.5f, 10f), "DOOR", 0.16f);
            CreateLabel("Locked Door Label", new Vector3(17.5f, 12.5f, 10f), "LOCKED DOOR", 0.16f);
            CreateLabel("Trapdoor Label", new Vector3(30f, 5f, 8f), "TRAPDOOR", 0.16f);
            CreateLabel("Pressure Door Label", new Vector3(43.5f, 12.5f, 10f), "PLATE -> DOOR", 0.16f);
            CreateLabel("Portcullis Label", new Vector3(57.5f, 12.5f, 10f), "PLATE -> PORTCULLIS", 0.16f);

            CreateLabel("Movement Bay", new Vector3(4f, 15f, 20f), "MOVEMENT", 0.22f);
            CreateLabel("Elevator Label", new Vector3(6f, 5f, 27f), "ELEVATOR", 0.16f);
            CreateLabel("Drawbridge Label", new Vector3(25f, 9f, 27f), "LEVER -> DRAWBRIDGE", 0.16f);
            CreateLabel("Button Gate Label", new Vector3(48f, 12.5f, 27f), "BUTTON -> GATE", 0.16f);

            CreateLabel("Secrets Bay", new Vector3(4f, 15f, 38f), "SECRETS", 0.22f);
            CreateLabel("Bookshelf Label", new Vector3(12f, 12.5f, 45f), "HIDDEN BUTTON -> PANEL", 0.16f);
            CreateLabel("High Place Label", new Vector3(7f, 16.5f, 27f), "HIGH-PLACE CHEST", 0.16f);
            CreateLabel("Remote Route Label", new Vector3(49f, 12.5f, 45f), "LEVER -> REMOTE GATE", 0.16f);
        }

        private void CreateBlock(string name, Vector3 voxelPosition, Vector3 voxelScale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(_galleryRoot.transform, false);
            block.transform.localPosition = voxelPosition * GalleryScale;
            block.transform.localScale = voxelScale * GalleryScale;

            Collider collider = block.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            ApplyGalleryMaterial(block);
        }

        private void CreateLabel(string name, Vector3 voxelPosition, string text, float characterSizeVoxels)
        {
            var label = new GameObject(name);
            label.transform.SetParent(_galleryRoot.transform, false);
            label.transform.localPosition = voxelPosition * GalleryScale;
            label.transform.localRotation = Quaternion.identity;
            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = characterSizeVoxels * GalleryScale;
            mesh.color = Color.white;
        }

        private static void ApplyGalleryMaterial(GameObject block)
        {
            MeshRenderer renderer = block.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            if (s_GalleryMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    renderer.enabled = false;
                    return;
                }

                s_GalleryMaterial = new Material(shader)
                {
                    name = "Interactables Showcase Dressing",
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(0.10f, 0.12f, 0.15f, 1f),
                };
            }
            renderer.sharedMaterial = s_GalleryMaterial;
        }

        private static Material s_GalleryMaterial;
    }
}
