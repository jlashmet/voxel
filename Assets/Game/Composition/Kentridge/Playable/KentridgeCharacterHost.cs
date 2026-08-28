using System;
using System.Collections.Generic;
using Game.Composition.Campaign;
using Game.Composition.Kentridge.Api;
using Game.Composition.WorldBuilderWorldGen;
using Game.Cutscenes.Api;
using MountingForce.WorldGen;
using UnityEngine;
using VoxelEngine.Characters.Runtime;
using VoxelEngine.Showcase;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Game-owned playable character composition for Kentridge. Scene code supplies scenario input,
    /// spawn/camera choices and the world; this host owns reusable motor, cutscene actor, model,
    /// animation and visual-root policy and implements the campaign actor boundary.
    /// </summary>
    public sealed class KentridgeCharacterHost : IKentridgeCampaignActorHost, IDisposable
    {
        private const float DecimetresToMetres = 0.1f;
        public const string MadelineResourcePath = "Characters/Madeline/Madeline";
        public const string MadelineModelGuid = "f593982524b374c80b946d9e4670471d";

        private readonly CharacterMotor _motor;
        private readonly Dictionary<NpcRef, NpcActor> _npcs = new Dictionary<NpcRef, NpcActor>();
        private readonly PlayerActor _player;

        public KentridgeCharacterHost(float walkSpeed)
        {
            _motor = new CharacterMotor { WalkSpeed = walkSpeed };
            _player = new PlayerActor(_motor);
        }

        public Vector3 PlayerPosition
        {
            get => _motor.Position;
            set => _motor.Position = value;
        }

        public Vector3 PlayerVelocity
        {
            get => _motor.Velocity;
            set => _motor.Velocity = value;
        }

        public Vector3 PlayerEyePosition => _motor.EyePosition;
        public float PlayerEyeHeight => _motor.EyeHeight;
        public Vector3 PlayerFacing => _player.Facing;

        public void SetPlayerCutsceneBodyVisible(bool visible) => _player.SetCutsceneBodyVisible(visible);

        public void StepPlayer(ShowcaseWorld world, Vector3 wish, bool sprint, bool jumpHeld, float dt) =>
            _motor.Step(world, wish, sprint, jumpHeld, dt);

        public void Tick(float dt)
        {
            _player.Tick(dt);
            foreach (NpcActor actor in _npcs.Values) actor.Tick(dt);
        }

        public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
        {
            foreach (NpcActor actor in _npcs.Values) actor.Dispose();
            _npcs.Clear();
            for (int i = 0; i < placements.Count; i++)
            {
                ResolvedNpcWorldPlacement placement = placements[i];
                Int3 point = placement.Position.Position;
                string identity = placement.Npc.ToString();
                _npcs.Add(
                    placement.Npc,
                    new NpcActor(
                        identity,
                        new CutsceneInt3(point.X, point.Y, point.Z),
                        CharacterPresentation.Create(identity)));
            }
        }

        public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
        {
            bool found = _npcs.TryGetValue(npc, out NpcActor value);
            actor = value;
            return found;
        }

        public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
        {
            actor = playerSlot == 0 ? _player : null;
            return actor != null;
        }

        public bool TryGetNpcPosition(NpcRef npc, out Vector3 position)
        {
            if (_npcs.TryGetValue(npc, out NpcActor actor))
            {
                position = ToMetres(actor.Position);
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>Test/diagnostic seam proving the resolved identity uses the production model.</summary>
        public bool IsUsingProductionMadeline(NpcRef npc)
        {
            return _npcs.TryGetValue(npc, out NpcActor actor) && actor.UsesProductionMadeline;
        }

        public void Dispose()
        {
            _player.Dispose();
            foreach (NpcActor actor in _npcs.Values) actor.Dispose();
            _npcs.Clear();
        }

        private sealed class PlayerActor : ICutsceneActorRuntime
        {
            private readonly CharacterMotor _motor;
            private readonly GameObject _root;
            private readonly CharacterAnimationPolicy _animation;
            private PlayerMoveOperation _move;

            public Vector3 Facing { get; private set; } = Vector3.forward;
            public CutsceneInt3 Position => ToCutscene(_motor.Position);

            public PlayerActor(CharacterMotor motor)
            {
                _motor = motor ?? throw new ArgumentNullException(nameof(motor));
                _root = CharacterPresentation.Create("Weldon");
                _animation = _root.GetComponentInChildren<CharacterAnimationPolicy>();
                _root.SetActive(false);
            }

            public void PlaceAt(CutsceneStagePoint destination)
            {
                _move = null;
                SetPosition(ToMetres(destination.Position));
                _motor.Velocity = Vector3.zero;
                SetFacing(destination.Forward);
                SetCutsceneBodyVisible(true);
                _animation?.SetLocomotion(CharacterLocomotionState.Idle);
            }

            public ICutsceneOperation MoveTo(CutsceneStagePoint destination, int durationHintMilliseconds)
            {
                _move = new PlayerMoveOperation(this, _motor.Position, ToMetres(destination.Position), durationHintMilliseconds * 0.001f);
                return _move;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition)
            {
                Vector3 direction = ToMetres(targetPosition) - _motor.Position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f)
                {
                    Facing = direction.normalized;
                    ApplyBodyFacing();
                }
                return CompletedCutsceneOperation.Instance;
            }

            public void Tick(float dt)
            {
                _move?.Tick(dt);
                if (_move != null && _move.IsComplete) _move = null;
                _animation?.Tick();
            }

            public void SetCutsceneBodyVisible(bool visible)
            {
                if (_root == null) return;
                _root.SetActive(visible);
                if (visible)
                {
                    _root.transform.position = _motor.Position;
                    ApplyBodyFacing();
                }
            }

            public void Dispose()
            {
                if (_root != null) UnityEngine.Object.Destroy(_root);
            }

            private void SetPosition(Vector3 position)
            {
                _motor.Position = position;
                _motor.Velocity = Vector3.zero;
                if (_root != null) _root.transform.position = position;
            }

            private void SetFacing(CutsceneInt3 facing)
            {
                Vector3 direction = new Vector3(facing.X, facing.Y, facing.Z);
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f)
                {
                    Facing = direction.normalized;
                    ApplyBodyFacing();
                }
            }

            private void ApplyBodyFacing()
            {
                if (_root == null || Facing.sqrMagnitude < 1e-6f) return;
                Vector3 flat = new Vector3(Facing.x, 0f, Facing.z).normalized;
                if (flat.sqrMagnitude > 1e-6f) _root.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            }

            private sealed class PlayerMoveOperation : ICutsceneOperation
            {
                private readonly PlayerActor _actor;
                private readonly Vector3 _start;
                private readonly Vector3 _destination;
                private readonly float _duration;
                private float _elapsed;
                public bool IsComplete { get; private set; }

                public PlayerMoveOperation(PlayerActor actor, Vector3 start, Vector3 destination, float duration)
                {
                    _actor = actor;
                    _start = start;
                    _destination = destination;
                    _duration = Mathf.Max(0f, duration);
                    Vector3 direction = _destination - _start;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 1e-6f)
                    {
                        _actor.Facing = direction.normalized;
                        _actor.ApplyBodyFacing();
                    }
                    if (_duration == 0f)
                    {
                        _actor.SetPosition(_destination);
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                    else _actor._animation?.SetLocomotion(CharacterLocomotionState.Walk);
                }

                public void Tick(float dt)
                {
                    if (IsComplete) return;
                    _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, dt));
                    float t = _duration <= 0f ? 1f : _elapsed / _duration;
                    _actor.SetPosition(Vector3.Lerp(_start, _destination, t));
                    if (_elapsed >= _duration)
                    {
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                }
            }
        }

        private sealed class NpcActor : ICutsceneActorRuntime
        {
            private readonly GameObject _root;
            private readonly CharacterAnimationPolicy _animation;
            private CutsceneInt3 _position;
            private NpcMoveOperation _move;

            public CutsceneInt3 Position => _position;
            public bool UsesProductionMadeline { get; }

            public NpcActor(string identity, CutsceneInt3 position, GameObject root)
            {
                _root = root ?? throw new ArgumentNullException(nameof(root));
                UsesProductionMadeline = CharacterPresentation.IsMadeline(identity) && root.name == "Madeline";
                SetPosition(position);
                _animation = _root.GetComponentInChildren<CharacterAnimationPolicy>();
                _animation?.SetLocomotion(CharacterLocomotionState.Idle);
            }

            public void PlaceAt(CutsceneStagePoint destination)
            {
                _move = null;
                SetPosition(destination.Position);
                SetFacing(destination.Forward);
                _animation?.SetLocomotion(CharacterLocomotionState.Idle);
            }

            public ICutsceneOperation MoveTo(CutsceneStagePoint destination, int durationHintMilliseconds)
            {
                _move = new NpcMoveOperation(this, ToMetres(_position), ToMetres(destination.Position), durationHintMilliseconds * 0.001f);
                return _move;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition)
            {
                Vector3 direction = ToMetres(targetPosition) - ToMetres(_position);
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f) _root.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                return CompletedCutsceneOperation.Instance;
            }

            public void Tick(float dt)
            {
                _move?.Tick(dt);
                if (_move != null && _move.IsComplete) _move = null;
                _animation?.Tick();
            }

            public void Dispose() => UnityEngine.Object.Destroy(_root);

            private void SetPosition(CutsceneInt3 position)
            {
                _position = position;
                _root.transform.position = ToMetres(position);
            }

            private void SetPosition(Vector3 position)
            {
                _position = ToCutscene(position);
                _root.transform.position = position;
            }

            private void SetFacing(CutsceneInt3 facing)
            {
                Vector3 direction = new Vector3(facing.X, facing.Y, facing.Z);
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f) _root.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            private sealed class NpcMoveOperation : ICutsceneOperation
            {
                private readonly NpcActor _actor;
                private readonly Vector3 _start;
                private readonly Vector3 _destination;
                private readonly float _duration;
                private float _elapsed;
                public bool IsComplete { get; private set; }

                public NpcMoveOperation(NpcActor actor, Vector3 start, Vector3 destination, float duration)
                {
                    _actor = actor;
                    _start = start;
                    _destination = destination;
                    _duration = Mathf.Max(0f, duration);
                    Vector3 direction = _destination - _start;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 1e-6f) _actor._root.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    if (_duration == 0f)
                    {
                        _actor.SetPosition(_destination);
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                    else _actor._animation?.SetLocomotion(CharacterLocomotionState.Walk);
                }

                public void Tick(float dt)
                {
                    if (IsComplete) return;
                    _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, dt));
                    float t = _duration <= 0f ? 1f : _elapsed / _duration;
                    _actor.SetPosition(Vector3.Lerp(_start, _destination, t));
                    if (_elapsed >= _duration)
                    {
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                }
            }
        }

        private static class CharacterPresentation
        {
            private const string MalePrefab = "Characters/placeholder_male";
            private const string FemalePrefab = "Characters/placeholder_female";

            public static bool IsMadeline(string identity) =>
                !string.IsNullOrEmpty(identity) && identity.IndexOf("madeline", StringComparison.OrdinalIgnoreCase) >= 0;

            public static GameObject Create(string identity)
            {
                bool madeline = IsMadeline(identity);
                string path = madeline
                    ? MadelineResourcePath
                    : ((Hash(identity ?? string.Empty) & 1u) == 0u ? MalePrefab : FemalePrefab);
                GameObject prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError("Character prefab missing at Resources/" + path);
                    GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    fallback.name = identity;
                    fallback.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                    return fallback;
                }

                GameObject body = UnityEngine.Object.Instantiate(prefab);
                body.name = madeline ? "Madeline" : identity;
                Animator animator = body.GetComponentInChildren<Animator>();
                if (animator != null) animator.applyRootMotion = false;
                return body;
            }

            private static uint Hash(string value)
            {
                uint h = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    h ^= value[i];
                    h *= 16777619u;
                }
                return h;
            }
        }

        private static Vector3 ToMetres(CutsceneInt3 point) =>
            new Vector3(point.X * DecimetresToMetres, point.Y * DecimetresToMetres, point.Z * DecimetresToMetres);

        private static CutsceneInt3 ToCutscene(Vector3 metres) =>
            new CutsceneInt3(
                Mathf.RoundToInt(metres.x / DecimetresToMetres),
                Mathf.RoundToInt(metres.y / DecimetresToMetres),
                Mathf.RoundToInt(metres.z / DecimetresToMetres));
    }
}
