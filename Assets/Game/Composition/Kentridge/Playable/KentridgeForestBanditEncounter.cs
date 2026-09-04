using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Composition.EncounterRealization;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Encounters.Api;
using Game.Encounters.Runtime;
using Game.Input.Api;
using Game.Input.Runtime;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using MountingForce.WorldGen;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Kentridge-specific presentation/composition adapter for the forest Encounter/Combat slice.
    /// Its authoritative Encounter/Input/Vitality/Combat runtimes are created only when the production
    /// session graph composes this extension, and authored placement comes from the WorldBuilder encounter
    /// realization bridge. Scene-specific proximity and participant/team mapping remain here rather than
    /// leaking into SessionOrchestration.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class KentridgeForestBanditEncounter : MonoBehaviour,
        IKentridgeSessionRuntimeExtensionFactory,
        IKentridgeSessionRuntimeExtension
    {
        private const string KentridgeSceneName = "KentridgePlayableSlice";
        private const string PlayerCameraName = "Kentridge Player Camera";
        private const string MaleCharacterResource = "Characters/placeholder_male";
        private const int AutonomousBattleSeed = 20260829;
        private const int InitialCombatVitality = 6;
        private const float BattleActionIntervalSeconds = 0.10f;
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);
        private static readonly CombatParticipantId PlayerParticipant = new CombatParticipantId("kentridge-player");
        private static readonly EncounterId ForestBanditEncounterId = new EncounterId("kentridge-forest-bandits");

        [SerializeField] private float _triggerRadiusMetres = 9f;
        [SerializeField] private float _groundResolveRadiusMetres = 96f;

        private readonly List<GameObject> _bandits = new List<GameObject>(3);
        private readonly bool[] _grounded = new bool[3];
        private readonly CharacterId[] _banditCharacterIds = new CharacterId[3];
        private ICharacterRegistry _characters;
        private EncounterRegistry _encounters;
        private InputContextService _inputContexts;
        private UnityPlayerInputReader _inputReader;
        private VitalityRegistry _vitality;
        private CombatService _combat;
        private CombatInputController _combatInput;
        private CombatAiBattleDriver _battleDriver;
        private IInputContextLease _combatContext;
        private EncounterRealization _realization;
        private IReadOnlyList<ISessionUpdateStep> _steps;
        private Vector3 _ambushCenterWorld;
        private RegionThemeKind _ambushTheme;
        private float _battleActionAccumulator;
        private bool _composed;
        private bool _commandsEnabled;
        private bool _disposed;

        public int BanditCount => _bandits.Count;
        public IReadOnlyList<GameObject> Bandits => _bandits;
        public float TriggerRadiusMetres => _triggerRadiusMetres;
        public Vector3 AmbushCenterWorld => _ambushCenterWorld;
        public RegionThemeKind AmbushTheme => _ambushTheme;
        public bool CombatActive => _combat != null && _combat.IsActive;
        public bool CombatResolved
        {
            get
            {
                return _encounters != null &&
                       _encounters.TryGet(ForestBanditEncounterId, out EncounterSnapshot snapshot) &&
                       (snapshot.Lifecycle == EncounterLifecycleState.Resolved || snapshot.Lifecycle == EncounterLifecycleState.Cleaned);
            }
        }
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

        public bool GameplayBindingsReady =>
            _composed
            && !_disposed
            && _characters != null
            && _encounters != null
            && _inputContexts != null
            && _inputReader != null
            && _vitality != null
            && _combat != null
            && _realization != null
            && _bandits.Count == 3;

        public IReadOnlyList<ISessionUpdateStep> UpdateSteps =>
            _steps ?? Array.Empty<ISessionUpdateStep>();

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

        public IKentridgeSessionRuntimeExtension Compose(
            GameSessionIdentity identity,
            IKentridgeCampaignActorHost actors)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (_composed && !_disposed)
                throw new InvalidOperationException(
                    "Kentridge forest session extension is already composed for the active run.");
            if (!(actors is KentridgeCharacterHost characterHost))
                throw new InvalidOperationException(
                    "Kentridge forest session extension requires the production KentridgeCharacterHost.");

            _disposed = false;
            _commandsEnabled = false;
            _characters = characterHost.Characters
                ?? throw new InvalidOperationException("Kentridge character authority is unavailable.");
            _inputContexts = new InputContextService();
            _inputReader = new UnityPlayerInputReader(_inputContexts);
            _vitality = new VitalityRegistry();
            _combat = new CombatService(_vitality);
            _encounters = new EncounterRegistry(_characters);

            for (int i = 0; i < _banditCharacterIds.Length; i++)
            {
                string participant = "forest-bandit-" + (i + 1);
                _banditCharacterIds[i] = CharacterId.FromStableKey("enemy", "kentridge-" + participant);
            }

            var definition = new EncounterDefinition(
                ForestBanditEncounterId,
                EncounterCombatPolicy.Required,
                "forest-ambush");
            EncounterRealizationResult realization = KentridgeForestEncounterRealization.Compose(
                definition,
                _banditCharacterIds[0],
                _banditCharacterIds[1],
                _banditCharacterIds[2]);
            if (!realization.IsSuccess)
                throw new InvalidOperationException(
                    "Kentridge forest encounter realization failed: " + realization.Diagnostic);
            _realization = realization.Realization;
            if (_realization.Characters.Count != _banditCharacterIds.Length)
                throw new InvalidOperationException(
                    "Kentridge forest encounter realization did not provide all bandit spawn bindings.");
            _ambushCenterWorld = ToUnityVector(_realization.Anchor);
            _ambushTheme = RegionThemeKind.PineForest;

            RequireEncounterSuccess(
                _encounters.Register(_realization.Definition, out _),
                "register Kentridge forest encounter");
            SpawnBandits();
            _steps = Array.AsReadOnly<ISessionUpdateStep>(new ISessionUpdateStep[]
            {
                new ForestEncounterStep(this),
                new ForestCombatStep(this)
            });
            _composed = true;
            return this;
        }

        public void StartCommands()
        {
            RequireComposed();
            _commandsEnabled = true;
        }

        public void StopCommands()
        {
            _commandsEnabled = false;
            _combatInput = null;
            ReleaseCombatContext();
        }

        public void SettleAuthoritativeState()
        {
            if (!_composed || _disposed || _combat == null || _combat.IsActive) return;
            if (_combat.WinningTeam.HasValue && !CombatResolved)
                SettleCompletedCombat();
        }

        public void DetachExternalAdapters()
        {
            _commandsEnabled = false;
            _combatInput = null;
            ReleaseCombatContext();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _commandsEnabled = false;
            _combatInput = null;
            ReleaseCombatContext();

            if (_characters != null)
            {
                for (int i = 0; i < _banditCharacterIds.Length; i++)
                {
                    CharacterId id = _banditCharacterIds[i];
                    if (!id.IsValid) continue;
                    _vitality?.Remove(id);
                    CharacterRegistryFailure failure = _characters.Remove(id);
                    if (failure != CharacterRegistryFailure.None &&
                        failure != CharacterRegistryFailure.UnknownCharacterId)
                        RequireSuccess(failure, "remove forest bandit during session teardown");
                    _banditCharacterIds[i] = default;
                }
            }

            for (int i = 0; i < _bandits.Count; i++)
                if (_bandits[i] != null) Destroy(_bandits[i]);
            _bandits.Clear();
            Array.Clear(_grounded, 0, _grounded.Length);

            _battleDriver = null;
            _combat = null;
            _vitality = null;
            _inputReader = null;
            _inputContexts = null;
            _encounters = null;
            _characters = null;
            _realization = null;
            _steps = null;
            _battleActionAccumulator = 0f;
            _composed = false;
            _disposed = true;
        }

        private void TickEncounterPhase(int elapsedMilliseconds)
        {
            if (!_commandsEnabled || !GameplayBindingsReady) return;

            ResolveBanditGroundNearPlayer();
            SyncBanditCharacters();

            if (_combat.IsActive)
            {
                _inputReader.SuppressLegacyReadersForCurrentFrame();
                return;
            }
            if (CombatResolved) return;

            float triggerSquared = _triggerRadiusMetres * _triggerRadiusMetres;
            Vector3 player = transform.position;
            for (int i = 0; i < _bandits.Count; i++)
            {
                GameObject bandit = _bandits[i];
                if (bandit == null) continue;
                FacePlayer(bandit.transform, player);
                if (PlanarDistanceSquared(player, bandit.transform.position) > triggerSquared) continue;
                ReportProximityActivation();
                _inputReader.SuppressLegacyReadersForCurrentFrame();
                break;
            }
        }

        private void TickCombatPhase(int elapsedMilliseconds)
        {
            if (!_commandsEnabled || !GameplayBindingsReady || !_combat.IsActive) return;

            float dt = Mathf.Max(0f, elapsedMilliseconds * 0.001f);
            _combatInput?.Tick(dt);
            _battleActionAccumulator += dt;
            if (_battleDriver != null && _battleActionAccumulator >= BattleActionIntervalSeconds)
            {
                _battleActionAccumulator -= BattleActionIntervalSeconds;
                _battleDriver.Step();
                if (!_combat.IsActive)
                    SettleCompletedCombat();
            }
            _inputReader.SuppressLegacyReadersForCurrentFrame();
        }

        private void SpawnBandits()
        {
            if (_bandits.Count != 0) return;
            if (_realization == null)
                throw new InvalidOperationException(
                    "Kentridge forest bandits cannot spawn before encounter realization.");

            for (int i = 0; i < _realization.Characters.Count; i++)
            {
                EncounterCharacterBinding binding = _realization.Characters[i];
                CharacterId id = binding.Participant.CharacterId;
                if (id != _banditCharacterIds[i])
                    throw new InvalidOperationException(
                        "Kentridge forest encounter realization changed authored bandit identity ordering.");

                Vector3 position = ToUnityVector(binding.Position);
                position.y = transform.position.y - 1.7f;
                GameObject bandit = CreateBandit(i, position);
                FacePlayer(bandit.transform, transform.position);
                _bandits.Add(bandit);

                string participant = "forest-bandit-" + (i + 1);
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
                    _characters.Bind(
                        id,
                        new CharacterBinding("encounter-member", "kentridge-forest-bandits/" + (i + 1))),
                    "bind forest bandit encounter identity");
                RequireEncounterSuccess(
                    _encounters.Join(ForestBanditEncounterId, binding.Participant, out _),
                    "join forest bandit encounter membership");
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
                if (Physics.Raycast(
                        origin,
                        Vector3.down,
                        out hit,
                        160f,
                        ~0,
                        QueryTriggerInteraction.Ignore) &&
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
                    _characters.UpdateKinematics(
                        _banditCharacterIds[i],
                        ToKinematics(bandit.transform),
                        out _),
                    "synchronize Kentridge forest bandit character");
            }
        }

        private void ReportProximityActivation()
        {
            if (_combat.IsActive || CombatResolved) return;
            if (!_characters.TryResolve(
                    new CharacterBinding("combat-participant", PlayerParticipant.Value),
                    out CharacterId player))
                throw new InvalidOperationException(
                    "Kentridge combat player is not bound to gameplay character authority.");
            RequireEncounterSuccess(
                _encounters.Join(
                    ForestBanditEncounterId,
                    new EncounterParticipant(player, EncounterParticipantOwnership.Persistent, "player"),
                    out _),
                "join Kentridge player encounter membership");
            RequireEncounterSuccess(
                _encounters.Activate(
                    new EncounterActivationRequest(
                        ForestBanditEncounterId,
                        "player-proximity",
                        _realization.RealizationId),
                    out _),
                "activate Kentridge forest encounter");
            if (!_encounters.TryTakeCombatRequest(out EncounterCombatRequest request))
                throw new InvalidOperationException(
                    "Kentridge forest encounter activated without its required Combat request.");
            BeginBanditCombat(request);
        }

        private void BeginBanditCombat(EncounterCombatRequest request)
        {
            if (_combat.IsActive || CombatResolved) return;
            if (request == null || request.EncounterId != ForestBanditEncounterId)
                throw new InvalidOperationException(
                    "Kentridge received an unexpected Encounter Combat request.");

            var participants = new CombatParticipant[request.Participants.Count];
            CombatParticipantId playerCombatId = default;
            int next = 0;
            for (int i = 0; i < request.Participants.Count; i++)
            {
                EncounterParticipant member = request.Participants[i];
                if (!_characters.TryGet(member.CharacterId, out _))
                    throw new InvalidOperationException(
                        "Encounter Combat member no longer exists: " + member.CharacterId + ".");
                EnsureVitalityRegistered(member.CharacterId);
                CombatTeam team = member.Role == "player" ? CombatTeam.Player : CombatTeam.Enemy;
                CombatParticipant participant = CombatParticipant.FromCharacter(member.CharacterId, team);
                participants[next++] = participant;
                if (team == CombatTeam.Player)
                    playerCombatId = participant.Id;
            }
            if (!playerCombatId.IsValid)
                throw new InvalidOperationException(
                    "Kentridge Encounter Combat request has no player participant.");

            _combat.BeginCombat(new CombatEncounterRequest(request.EncounterId.Value, participants));
            _combatContext = _inputContexts.Push(InputContextId.Combat);
            _combatInput = new CombatInputController(_combat, _inputReader, LocalPlayer, playerCombatId);
            _battleDriver = new CombatAiBattleDriver(_combat, AutonomousBattleSeed);
            _battleActionAccumulator = 0f;
        }

        private void EnsureVitalityRegistered(CharacterId characterId)
        {
            if (_vitality.TryGet(characterId, out _)) return;
            if (!_vitality.Register(VitalitySnapshot.Alive(characterId, InitialCombatVitality)))
                throw new InvalidOperationException(
                    "Failed to register combat vitality for character '" + characterId + "'.");
        }

        private void SettleCompletedCombat()
        {
            if (_combat == null || _combat.IsActive || CombatResolved) return;
            if (!_combat.WinningTeam.HasValue)
                throw new InvalidOperationException(
                    "Kentridge combat completed without a terminal team outcome.");
            if (_battleDriver != null && _battleDriver.HasPendingAction)
                throw new InvalidOperationException(
                    _battleDriver.Diagnostic("Kentridge combat completed with pending AI work."));

            EncounterResolution encounterResolution;
            if (_combat.WinningTeam.Value == CombatTeam.Player)
            {
                for (int i = 0; i < _banditCharacterIds.Length; i++)
                    MarkDefeated(_banditCharacterIds[i], "mark defeated forest bandit");
                encounterResolution = new EncounterResolution(
                    EncounterResolutionResult.Completed,
                    "combat-victory");
            }
            else
            {
                if (!_characters.TryResolve(
                        new CharacterBinding("combat-participant", PlayerParticipant.Value),
                        out CharacterId player))
                    throw new InvalidOperationException(
                        "Kentridge combat player is not bound to gameplay character authority.");
                MarkDefeated(player, "mark defeated Kentridge player");
                encounterResolution = new EncounterResolution(
                    EncounterResolutionResult.Failed,
                    "combat-defeat");
            }

            RequireEncounterSuccess(
                _encounters.ApplyCombatResolved(
                    ForestBanditEncounterId,
                    encounterResolution,
                    out _),
                "resolve Kentridge forest encounter from Combat");
            RequireEncounterSuccess(
                _encounters.Cleanup(ForestBanditEncounterId, out _),
                "clean Kentridge forest encounter");
            ApplyEncounterCleanupFacts();

            _combatInput = null;
            ReleaseCombatContext();
            Debug.Log(
                "[KentridgeCombat] battle-complete seed=" + AutonomousBattleSeed +
                " winner=" + _combat.WinningTeam.Value +
                " actions=" + _combat.ActionCount +
                " turns=" + _combat.TurnNumber +
                " pending=" + HasPendingCombatWork);
        }

        private void ApplyEncounterCleanupFacts()
        {
            IReadOnlyList<EncounterFact> facts = _encounters.DrainFacts();
            for (int i = 0; i < facts.Count; i++)
            {
                EncounterFact fact = facts[i];
                if (fact.Kind != EncounterFactKind.CleanupCharacter || !fact.CharacterId.IsValid) continue;
                _vitality.Remove(fact.CharacterId);
                CharacterRegistryFailure failure = _characters.Remove(fact.CharacterId);
                if (failure != CharacterRegistryFailure.None &&
                    failure != CharacterRegistryFailure.UnknownCharacterId)
                    RequireSuccess(failure, "remove encounter-owned Kentridge character");
                for (int banditIndex = 0; banditIndex < _banditCharacterIds.Length; banditIndex++)
                {
                    if (_banditCharacterIds[banditIndex] != fact.CharacterId) continue;
                    if (banditIndex < _bandits.Count && _bandits[banditIndex] != null)
                        Destroy(_bandits[banditIndex]);
                    _banditCharacterIds[banditIndex] = default;
                    break;
                }
            }
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

        private void RequireComposed()
        {
            if (!_composed || _disposed)
                throw new InvalidOperationException(
                    "Kentridge forest session extension is not part of an active composed session.");
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

        private static Vector3 ToUnityVector(CharacterVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);

        private static void RequireSuccess(CharacterRegistryFailure failure, string operation)
        {
            if (failure != CharacterRegistryFailure.None)
                throw new InvalidOperationException(operation + " failed: " + failure + ".");
        }

        private static void RequireEncounterSuccess(EncounterMutationFailure failure, string operation)
        {
            if (failure != EncounterMutationFailure.None)
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
                AddPrimitive(
                    root.transform,
                    PrimitiveType.Capsule,
                    "Emergency Body",
                    new Vector3(0f, 0.95f, 0f),
                    new Vector3(0.68f, 0.82f, 0.54f),
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

            AddPrimitive(
                root.transform,
                PrimitiveType.Sphere,
                "Hood",
                new Vector3(0f, 1.70f, 0.01f),
                new Vector3(0.50f, 0.42f, 0.48f),
                coat * 0.72f);
            AddPrimitive(
                root.transform,
                PrimitiveType.Cube,
                "Belt",
                new Vector3(0f, 0.91f, 0f),
                new Vector3(0.70f, 0.09f, 0.30f),
                leather);
            AddPrimitive(
                    root.transform,
                    PrimitiveType.Cube,
                    "Shoulder Strap",
                    new Vector3(-0.12f, 1.18f, 0.15f),
                    new Vector3(0.10f, 0.78f, 0.07f),
                    leather)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            AddPrimitive(
                root.transform,
                PrimitiveType.Cube,
                "Pouch",
                new Vector3(-0.31f, 0.79f, 0.12f),
                new Vector3(0.20f, 0.24f, 0.12f),
                leather);
            GameObject sword = AddPrimitive(
                root.transform,
                PrimitiveType.Cube,
                "Sword",
                new Vector3(0.48f, 0.82f, 0.11f),
                new Vector3(0.07f, 0.86f, 0.09f),
                new Color(0.55f, 0.58f, 0.60f));
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            AddPrimitive(
                sword.transform,
                PrimitiveType.Cube,
                "Guard",
                new Vector3(0f, 0.36f, 0f),
                new Vector3(0.30f, 0.06f, 0.12f),
                leather);
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
            Dispose();
        }

        private sealed class ForestEncounterStep : ISessionUpdateStep
        {
            private readonly KentridgeForestBanditEncounter _owner;

            public ForestEncounterStep(KentridgeForestBanditEncounter owner) => _owner = owner;
            public SessionUpdatePhase Phase => SessionUpdatePhase.Encounter;
            public int Order => 0;
            public string SemanticId => "kentridge.forest-encounter";
            public void Tick(int elapsedMilliseconds) => _owner.TickEncounterPhase(elapsedMilliseconds);
        }

        private sealed class ForestCombatStep : ISessionUpdateStep
        {
            private readonly KentridgeForestBanditEncounter _owner;

            public ForestCombatStep(KentridgeForestBanditEncounter owner) => _owner = owner;
            public SessionUpdatePhase Phase => SessionUpdatePhase.Combat;
            public int Order => 0;
            public string SemanticId => "kentridge.forest-combat";
            public void Tick(int elapsedMilliseconds) => _owner.TickCombatPhase(elapsedMilliseconds);
        }
    }
}
