using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Input.Api;
using Game.Input.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
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
    /// input-context ownership, battle stepping, and presentation identities. Combat rules remain in Game.Combat,
    /// while stable gameplay identity/lifecycle/kinematics live in Game.Characters and actor life truth lives in Vitality.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class KentridgeForestBanditEncounter : MonoBehaviour
    {
        private const string KentridgeSceneName = "KentridgePlayableSlice";
        private const string PlayerCameraName = "Kentridge Player Camera";
        private const string MaleCharacterResource = "Characters/placeholder_male";
        private const float DecimetresToMetres = 0.1f;
        private const int ForestEntryInsetDm = 240;
        private const int AutonomousBattleSeed = 20260829;
        private const int InitialCombatVitality = 6;
        private const float BattleActionIntervalSeconds = 0.10f;
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);
        private static readonly CombatParticipantId PlayerParticipant = new CombatParticipantId("kentridge-player");

        [SerializeField] private float _triggerRadiusMetres = 9f;
        [SerializeField] private float _groundResolveRadiusMetres = 96f;

        private readonly List<GameObject> _bandits = new List<GameObject>(3);
        private readonly bool[] _grounded = new bool[3];
        private readonly CharacterId[] _banditCharacterIds = new CharacterId[3];
        private ICharacterRegistry _characters;
        private InputContextService _inputContexts;
        private UnityPlayerInputReader _inputReader;
        private VitalityRegistry _vitality;
        private CombatService _combat;
        private CombatInputController _combatInput;
        private CombatAiBattleDriver _battleDriver;
        private IInputContextLease _combatContext;
        private Vector3 _ambushCenterWorld;
        private RegionThemeKind _ambushTheme;
        private float _nextBattleActionTime;
        private bool _encounterResolved;

        public int BanditCount => _bandits.Count;
        public IReadOnlyList<GameObject> Bandits => _bandits;
        public float TriggerRadiusMetres => _triggerRadiusMetres;
        public Vector3 AmbushCenterWorld => _ambushCenterWorld;
        public RegionThemeKind AmbushTheme => _ambushTheme;
        public bool CombatActive => _combat != null && _combat.IsActive;
        public bool CombatResolved => _encounterResolved;
        public CombatTeam? WinningTeam => _combat == null ? null : _combat.WinningTeam;
        public int CombatActionCount => _combat == null ? 0 : _combat.ActionCount;
        public int CombatTurnNumber => _combat == null ? 0 : _combat.TurnNumber;
        public int BattleSeed => AutonomousBattleSeed;
        public bool HasPendingCombatWork =>
            (_combat != null && _combat.HasPendingBattleWork) ||
            (_battleDriver != null && _battleDriver.HasPendingAction);
        public string BattleDiagnostic => _battleDriver == null
            ? "seed=" + AutonomousBattleSeed + " state=" + (_combat == null ? CombatLifecycleState.Idle : _combat.State)
            : _battleDriver.Diagnostic("Kentridge forest battle");
        public InputContextId ActiveInputContext =>
            _inputContexts == null ? InputContextId.Exploration : _inputContexts.ActiveContext;
        public ICombatService CombatService => _combat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneInstaller()
        {
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
            _vitality = new VitalityRegistry();
            _combat = new CombatService(_vitality);
            BuildAuthoredAmbushPlan();
        }

        private void Start()
        {
            KentridgeCharacterRegistryAnchor anchor = GetComponent<KentridgeCharacterRegistryAnchor>();
            if (anchor == null || anchor.Characters == null)
                throw new InvalidOperationException(
                    "Kentridge forest encounter requires the playable character registry anchor before actor realization.");
            _characters = anchor.Characters;
            SpawnBandits();
        }

        private void Update()
        {
            if (_bandits.Count != 3 || _combat == null) return;

            ResolveBanditGroundNearPlayer();
            SyncBanditCharacters();

            if (!_combat.IsActive)
            {
                if (_encounterResolved) return;

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
                return;
            }

            if (_combatInput != null)
                _combatInput.Tick(Time.deltaTime);

            if (_battleDriver != null && Time.unscaledTime >= _nextBattleActionTime)
            {
                _battleDriver.Step();
                _nextBattleActionTime = Time.unscaledTime + BattleActionIntervalSeconds;
                if (!_combat.IsActive)
                    SettleCompletedCombat();
            }

            _inputReader.SuppressLegacyReadersForCurrentFrame();
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

                string participant = "forest-bandit-" + (i + 1);
                CharacterId id = CharacterId.FromStableKey("enemy", "kentridge-" + participant);
                _banditCharacterIds[i] = id;
                if (!_characters.TryGet(id, out _))
                    RequireSuccess(
                        _characters.Create(
                            new CharacterDefinition(id, CharacterTraits.Combatant),
                            ToKinematics(bandit.transform),
                            out _),
                        "create Kentridge forest bandit character");
                RequireSuccess(
                    _characters.Bind(id, new CharacterBinding("combat-participant", participant)),
                    "bind forest bandit combat identity");
                RequireSuccess(
                    _characters.Bind(id, new CharacterBinding("encounter-member", "kentridge-forest-bandits/" + (i + 1))),
                    "bind forest bandit encounter identity");
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
                    desired.y = player.y - 1.7f;
                }

                bandit.transform.position = desired;
            }
        }

        private void SyncBanditCharacters()
        {
            for (int i = 0; i < _bandits.Count; i++)
            {
                GameObject bandit = _bandits[i];
                if (bandit == null || !_banditCharacterIds[i].IsValid) continue;
                RequireSuccess(
                    _characters.UpdateKinematics(_banditCharacterIds[i], ToKinematics(bandit.transform), out _),
                    "synchronize Kentridge forest bandit character");
            }
        }

        private void BeginBanditCombat()
        {
            if (_combat.IsActive || _encounterResolved) return;

            CharacterId playerCharacter;
            if (!_characters.TryResolve(new CharacterBinding("combat-participant", PlayerParticipant.Value), out playerCharacter))
                throw new InvalidOperationException("Kentridge combat player is not bound to gameplay character authority.");

            EnsureVitalityRegistered(playerCharacter);
            var participants = new CombatParticipant[4];
            participants[0] = CombatParticipant.FromCharacter(playerCharacter, CombatTeam.Player);
            for (int i = 0; i < 3; i++)
            {
                EnsureVitalityRegistered(_banditCharacterIds[i]);
                participants[i + 1] = CombatParticipant.FromCharacter(_banditCharacterIds[i], CombatTeam.Enemy);
            }

            _combat.BeginCombat(new CombatEncounterRequest("kentridge-forest-bandits", participants));
            _combatContext = _inputContexts.Push(InputContextId.Combat);
            _combatInput = new CombatInputController(_combat, _inputReader, LocalPlayer, participants[0].Id);
            _battleDriver = new CombatAiBattleDriver(_combat, AutonomousBattleSeed);
            _nextBattleActionTime = Time.unscaledTime + BattleActionIntervalSeconds;
        }

        private void EnsureVitalityRegistered(CharacterId characterId)
        {
            if (_vitality.TryGet(characterId, out _)) return;
            if (!_vitality.Register(VitalitySnapshot.Alive(characterId, InitialCombatVitality)))
                throw new InvalidOperationException("Failed to register combat vitality for character '" + characterId + "'.");
        }

        private void SettleCompletedCombat()
        {
            if (_combat == null || _combat.IsActive) return;
            if (!_combat.WinningTeam.HasValue)
                throw new InvalidOperationException("Kentridge combat completed without a terminal team outcome.");
            if (_battleDriver != null && _battleDriver.HasPendingAction)
                throw new InvalidOperationException(_battleDriver.Diagnostic("Kentridge combat completed with pending AI work."));

            if (_combat.WinningTeam.Value == CombatTeam.Player)
            {
                for (int i = 0; i < _banditCharacterIds.Length; i++)
                    MarkDefeated(_banditCharacterIds[i], "mark defeated forest bandit");
            }
            else
            {
                CharacterId player;
                if (!_characters.TryResolve(new CharacterBinding("combat-participant", PlayerParticipant.Value), out player))
                    throw new InvalidOperationException("Kentridge combat player is not bound to gameplay character authority.");
                MarkDefeated(player, "mark defeated Kentridge player");
            }

            _encounterResolved = true;
            _combatInput = null;
            ReleaseCombatContext();
            Debug.Log(
                "[KentridgeCombat] battle-complete seed=" + AutonomousBattleSeed +
                " winner=" + _combat.WinningTeam.Value +
                " actions=" + _combat.ActionCount +
                " turns=" + _combat.TurnNumber +
                " pending=" + HasPendingCombatWork);
        }

        private void MarkDefeated(CharacterId id, string operation)
        {
            CharacterRegistryFailure failure = _characters.MarkDefeated(id, out _);
            if (failure != CharacterRegistryFailure.None &&
                failure != CharacterRegistryFailure.CharacterAlreadyDefeated)
                RequireSuccess(failure, operation);
        }

        private void ReleaseCombatContext()
        {
            if (_combatContext == null) return;
            _combatContext.Dispose();
            _combatContext = null;
        }

        private static CharacterKinematicState ToKinematics(Transform actor)
        {
            Vector3 forward = actor.forward;
            return new CharacterKinematicState(
                ToCharacterVector(actor.position),
                new CharacterVector3(0f, 0f, 0f),
                ToCharacterVector(forward));
        }

        private static CharacterVector3 ToCharacterVector(Vector3 value) =>
            new CharacterVector3(value.x, value.y, value.z);

        private static void RequireSuccess(CharacterRegistryFailure failure, string operation)
        {
            if (failure != CharacterRegistryFailure.None)
                throw new InvalidOperationException(operation + " failed: " + failure + ".");
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
            ReleaseCombatContext();
        }
    }
}
