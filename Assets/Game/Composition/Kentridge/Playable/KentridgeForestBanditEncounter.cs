using System;
using System.Collections.Generic;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Input.Api;
using Game.Input.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Hightown;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Production composition seam for the first Combat/Input vertical slice in Kentridge.
    /// The encounter owns only cross-module wiring: authored world placement, proximity lifecycle,
    /// input-context ownership, and presentation identities. Combat rules remain in Game.Combat.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class KentridgeForestBanditEncounter : MonoBehaviour
    {
        private const string KentridgeSceneName = "KentridgePlayableSlice";
        private const string PlayerCameraName = "Kentridge Player Camera";
        private const string MaleCharacterResource = "Characters/placeholder_male";
        private const float DecimetresToMetres = 0.1f;
        private const int ForestEntryInsetDm = 240;
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);
        private static readonly CombatParticipantId PlayerParticipant = new CombatParticipantId("kentridge-player");

        [SerializeField] private float _triggerRadiusMetres = 9f;
        [SerializeField] private float _groundResolveRadiusMetres = 96f;

        private readonly List<GameObject> _bandits = new List<GameObject>(3);
        private readonly bool[] _grounded = new bool[3];
        private InputContextService _inputContexts;
        private UnityPlayerInputReader _inputReader;
        private CombatService _combat;
        private CombatInputController _combatInput;
        private IInputContextLease _combatContext;
        private Vector3 _ambushCenterWorld;
        private RegionThemeKind _ambushTheme;

        public int BanditCount => _bandits.Count;
        public IReadOnlyList<GameObject> Bandits => _bandits;
        public float TriggerRadiusMetres => _triggerRadiusMetres;
        public Vector3 AmbushCenterWorld => _ambushCenterWorld;
        public RegionThemeKind AmbushTheme => _ambushTheme;
        public bool CombatActive => _combat != null && _combat.IsActive;
        public InputContextId ActiveInputContext =>
            _inputContexts == null ? InputContextId.Exploration : _inputContexts.ActiveContext;
        public ICombatService CombatService => _combat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneInstaller()
        {
            // RuntimeInitialize can be invoked more than once when editor domain reload is disabled.
            // Remove first so every loaded Kentridge scene gets exactly one installation callback.
            SceneManager.sceneLoaded -= InstallIntoPlayableSlice;
            SceneManager.sceneLoaded += InstallIntoPlayableSlice;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitialScene()
        {
            InstallIntoPlayableSlice(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void InstallIntoPlayableSlice(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || scene.name != KentridgeSceneName) return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (!string.Equals(root.name, PlayerCameraName, StringComparison.Ordinal)) continue;
                if (root.GetComponent<KentridgeForestBanditEncounter>() == null)
                    root.AddComponent<KentridgeForestBanditEncounter>();
                return;
            }
        }

        private void Awake()
        {
            _inputContexts = new InputContextService();
            _inputReader = new UnityPlayerInputReader(_inputContexts);
            _combat = new CombatService();
            BuildAuthoredAmbushPlan();
            SpawnBandits();
        }

        private void Update()
        {
            if (_bandits.Count != 3 || _combat == null) return;

            ResolveBanditGroundNearPlayer();

            if (!_combat.IsActive)
            {
                float triggerSquared = _triggerRadiusMetres * _triggerRadiusMetres;
                Vector3 player = transform.position;
                for (int i = 0; i < _bandits.Count; i++)
                {
                    GameObject bandit = _bandits[i];
                    if (bandit == null) continue;
                    FacePlayer(bandit.transform, player);
                    if (PlanarDistanceSquared(player, bandit.transform.position) > triggerSquared) continue;
                    BeginBanditCombat();
                    _inputReader.SuppressLegacyReadersForCurrentFrame();
                    break;
                }
            }
            else
            {
                if (_combatInput != null)
                    _combatInput.Tick(Time.deltaTime);

                // The existing Kentridge exploration controller is a legacy direct Unity input reader.
                // Consume the physical frame here after Combat sampled it so both systems can never
                // apply the same WASD/mouse intent while the Combat context owns control.
                _inputReader.SuppressLegacyReadersForCurrentFrame();
            }
        }

        private void BuildAuthoredAmbushPlan()
        {
            Int2 kentridge = KentridgeDefinition.TownCentreDm;
            Int2 hightown = HightownDefinition.TownCentreDm;
            if (kentridge.X != hightown.X)
                throw new InvalidOperationException("Kentridge forest ambush requires the authored inter-town road axis.");

            int south = Math.Min(kentridge.Y, hightown.Y);
            int north = Math.Max(kentridge.Y, hightown.Y);
            int crossing = (south + north) / 2;
            RegionThemeMap themes = RegionThemeMap.ForKentridgeHightown(kentridge.Y, hightown.Y, crossing);

            int firstPineDm = int.MinValue;
            for (int zDm = south; zDm < north; zDm += 10)
            {
                if (themes.ThemeAt(zDm) != RegionThemeKind.PineForest) continue;
                firstPineDm = zDm;
                break;
            }

            if (firstPineDm == int.MinValue)
                throw new InvalidOperationException("Kentridge-Hightown corridor contains no authored PineForest band.");

            int ambushZDm = firstPineDm + ForestEntryInsetDm;
            _ambushTheme = themes.ThemeAt(ambushZDm);
            if (_ambushTheme != RegionThemeKind.PineForest)
                throw new InvalidOperationException("Forest ambush anchor escaped the authored PineForest band.");

            _ambushCenterWorld = new Vector3(
                kentridge.X * DecimetresToMetres,
                0f,
                ambushZDm * DecimetresToMetres);
        }

        private void SpawnBandits()
        {
            if (_bandits.Count != 0) return;

            Vector2[] offsets =
            {
                new Vector2(-5.4f, -0.8f),
                new Vector2(0.8f, 1.2f),
                new Vector2(5.8f, 0.1f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 position = new Vector3(
                    _ambushCenterWorld.x + offsets[i].x,
                    transform.position.y - 1.7f,
                    _ambushCenterWorld.z + offsets[i].y);
                GameObject bandit = CreateBandit(i, position);
                FacePlayer(bandit.transform, transform.position);
                _bandits.Add(bandit);
            }
        }

        private void ResolveBanditGroundNearPlayer()
        {
            Vector3 player = transform.position;
            float resolveSquared = _groundResolveRadiusMetres * _groundResolveRadiusMetres;
            if (PlanarDistanceSquared(player, _ambushCenterWorld) > resolveSquared) return;

            for (int i = 0; i < _bandits.Count; i++)
            {
                GameObject bandit = _bandits[i];
                if (bandit == null || _grounded[i]) continue;

                Vector3 desired = bandit.transform.position;
                Vector3 origin = new Vector3(desired.x, player.y + 64f, desired.z);
                RaycastHit hit;
                if (Physics.Raycast(origin, Vector3.down, out hit, 160f, ~0, QueryTriggerInteraction.Ignore) &&
                    Mathf.Abs(hit.point.y - player.y) < 32f)
                {
                    desired.y = hit.point.y;
                    _grounded[i] = true;
                }
                else if (PlanarDistanceSquared(player, desired) < 40f * 40f)
                {
                    // Collision publication can lag the saved-pose replay by a frame. Keep the
                    // actor readable at player-ground height until a real surface hit is available.
                    desired.y = player.y - 1.7f;
                }

                bandit.transform.position = desired;
            }
        }

        private void BeginBanditCombat()
        {
            if (_combat.IsActive) return;

            var participants = new CombatParticipant[4];
            participants[0] = new CombatParticipant(PlayerParticipant, CombatTeam.Player);
            for (int i = 0; i < 3; i++)
                participants[i + 1] = new CombatParticipant(
                    new CombatParticipantId("forest-bandit-" + (i + 1)),
                    CombatTeam.Enemy);

            _combat.BeginCombat(new CombatEncounterRequest("kentridge-forest-bandits", participants));
            _combatContext = _inputContexts.Push(InputContextId.Combat);
            _combatInput = new CombatInputController(_combat, _inputReader, LocalPlayer, PlayerParticipant);
        }

        private static GameObject CreateBandit(int index, Vector3 groundPosition)
        {
            GameObject prefab = Resources.Load<GameObject>(MaleCharacterResource);
            GameObject root;
            if (prefab != null)
            {
                root = Instantiate(prefab);
                root.name = "Forest Bandit " + (index + 1);
                root.transform.position = groundPosition;
                root.transform.rotation = Quaternion.identity;
                root.SetActive(true);
            }
            else
            {
                // Missing Resources are a build-integrity fault, but retain a readable emergency
                // body so the world does not silently lose encounter actors in a damaged build.
                root = new GameObject("Forest Bandit " + (index + 1));
                root.transform.position = groundPosition;
                AddPrimitive(root.transform, PrimitiveType.Capsule, "Emergency Body",
                    new Vector3(0f, 0.95f, 0f), new Vector3(0.68f, 0.82f, 0.54f),
                    new Color(0.20f, 0.15f, 0.12f));
            }

            CapsuleCollider rootCollider = root.GetComponent<CapsuleCollider>();
            if (rootCollider == null) rootCollider = root.AddComponent<CapsuleCollider>();
            rootCollider.center = new Vector3(0f, 0.95f, 0f);
            rootCollider.radius = 0.42f;
            rootCollider.height = 1.9f;

            Color coat = index == 0
                ? new Color(0.24f, 0.12f, 0.09f)
                : index == 1
                    ? new Color(0.13f, 0.20f, 0.12f)
                    : new Color(0.16f, 0.15f, 0.18f);
            Color leather = new Color(0.11f, 0.07f, 0.04f);

            // Distinct layered gear keeps the rigged production character readable as a forest
            // outlaw at the saved-pose distance without replacing its authored body/animation.
            AddPrimitive(root.transform, PrimitiveType.Sphere, "Hood",
                new Vector3(0f, 1.70f, 0.01f), new Vector3(0.50f, 0.42f, 0.48f), coat * 0.72f);
            AddPrimitive(root.transform, PrimitiveType.Cube, "Belt",
                new Vector3(0f, 0.91f, 0f), new Vector3(0.70f, 0.09f, 0.30f), leather);
            AddPrimitive(root.transform, PrimitiveType.Cube, "Shoulder Strap",
                new Vector3(-0.12f, 1.18f, 0.15f), new Vector3(0.10f, 0.78f, 0.07f), leather)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            AddPrimitive(root.transform, PrimitiveType.Cube, "Pouch",
                new Vector3(-0.31f, 0.79f, 0.12f), new Vector3(0.20f, 0.24f, 0.12f), leather);
            GameObject sword = AddPrimitive(root.transform, PrimitiveType.Cube, "Sword",
                new Vector3(0.48f, 0.82f, 0.11f), new Vector3(0.07f, 0.86f, 0.09f),
                new Color(0.55f, 0.58f, 0.60f));
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            AddPrimitive(sword.transform, PrimitiveType.Cube, "Guard",
                new Vector3(0f, 0.36f, 0f), new Vector3(0.30f, 0.06f, 0.12f), leather);
            return root;
        }

        private static GameObject AddPrimitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
            return part;
        }

        private static void FacePlayer(Transform bandit, Vector3 player)
        {
            Vector3 look = player - bandit.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                bandit.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private static float PlanarDistanceSquared(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private void OnDestroy()
        {
            if (_combatContext != null)
            {
                _combatContext.Dispose();
                _combatContext = null;
            }
        }
    }
}
