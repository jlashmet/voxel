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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIntoPlayableSlice()
        {
            if (SceneManager.GetActiveScene().name != KentridgeSceneName) return;

            GameObject playerCamera = GameObject.Find("Kentridge Player Camera");
            if (playerCamera == null) return;
            if (playerCamera.GetComponent<KentridgeForestBanditEncounter>() == null)
                playerCamera.AddComponent<KentridgeForestBanditEncounter>();
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
            var root = new GameObject("Forest Bandit " + (index + 1));
            root.transform.position = groundPosition;
            var rootCollider = root.AddComponent<CapsuleCollider>();
            rootCollider.center = new Vector3(0f, 0.95f, 0f);
            rootCollider.radius = 0.42f;
            rootCollider.height = 1.9f;

            Color coat = index == 0
                ? new Color(0.24f, 0.12f, 0.09f)
                : index == 1
                    ? new Color(0.13f, 0.20f, 0.12f)
                    : new Color(0.16f, 0.15f, 0.18f);

            AddPrimitive(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.92f, 0f), new Vector3(0.68f, 0.72f, 0.54f), coat);
            AddPrimitive(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.72f, 0f), new Vector3(0.43f, 0.43f, 0.43f), new Color(0.62f, 0.43f, 0.30f));
            AddPrimitive(root.transform, PrimitiveType.Sphere, "Hood", new Vector3(0f, 1.79f, 0.02f), new Vector3(0.51f, 0.44f, 0.49f), coat * 0.7f);
            AddPrimitive(root.transform, PrimitiveType.Cube, "Belt", new Vector3(0f, 0.86f, 0f), new Vector3(0.72f, 0.10f, 0.57f), new Color(0.11f, 0.07f, 0.04f));
            AddPrimitive(root.transform, PrimitiveType.Cube, "LeftArm", new Vector3(-0.46f, 1.10f, 0f), new Vector3(0.18f, 0.72f, 0.20f), coat);
            AddPrimitive(root.transform, PrimitiveType.Cube, "RightArm", new Vector3(0.46f, 1.10f, 0f), new Vector3(0.18f, 0.72f, 0.20f), coat);

            GameObject blade = AddPrimitive(root.transform, PrimitiveType.Cube, "Sword", new Vector3(0.60f, 0.87f, 0.12f), new Vector3(0.08f, 0.82f, 0.10f), new Color(0.55f, 0.58f, 0.60f));
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
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
