using UnityEngine;

namespace VoxelEngine.Characters.Runtime
{
    public enum CharacterLocomotionState
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        CrouchIdle = 3,
    }

    /// <summary>
    /// Small gameplay-facing policy layered over <see cref="CharacterAnimationPlayer"/>.
    /// It owns common locomotion selection and one-shot/return behavior, while the player
    /// remains responsible for Playables and retargeting across visual replacements.
    /// One-shot clips are intentionally arbitrary so placeholder Wave/Shrug and generated
    /// character Cast/Attack clips use the same runtime contract.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterAnimationPlayer))]
    public sealed class CharacterAnimationPolicy : MonoBehaviour
    {
        [SerializeField]
        private CharacterAnimationPlayer player;

        [Header("Locomotion")]
        [SerializeField]
        private AnimationClip idleClip;

        [SerializeField]
        private AnimationClip walkClip;

        [SerializeField]
        private AnimationClip runClip;

        [SerializeField]
        private AnimationClip crouchIdleClip;

        private CharacterLocomotionState locomotionState;
        private AnimationClip activeOneShot;

        public CharacterAnimationPlayer Player => player;

        public CharacterLocomotionState LocomotionState => locomotionState;

        public AnimationClip LocomotionClip => ResolveLocomotionClip(locomotionState);

        public AnimationClip ActiveOneShot => activeOneShot;

        private void Awake()
        {
            ResolvePlayerIfNeeded();
        }

        private void OnEnable()
        {
            ResolvePlayerIfNeeded();
            PlayLocomotionIfAvailable();
        }

        private void Update()
        {
            ResolvePlayerIfNeeded();
            if (player == null)
            {
                return;
            }

            if (activeOneShot != null)
            {
                if (player.CurrentClip != activeOneShot)
                {
                    TryPlay(activeOneShot);
                    return;
                }

                if (!player.IsPlaying)
                {
                    return;
                }

                if (player.CurrentTime + 0.0001d < activeOneShot.length)
                {
                    return;
                }

                activeOneShot = null;
                PlayLocomotionIfAvailable();
                return;
            }

            AnimationClip locomotion = ResolveLocomotionClip(locomotionState);
            if (locomotion != null && player.CurrentClip != locomotion)
            {
                TryPlay(locomotion);
            }
        }

        public void SetPlayer(CharacterAnimationPlayer value)
        {
            player = value;
            if (isActiveAndEnabled)
            {
                RefreshCurrentIntent();
            }
        }

        public void ConfigureLocomotion(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip crouchIdle = null)
        {
            idleClip = idle;
            walkClip = walk;
            runClip = run;
            crouchIdleClip = crouchIdle;

            if (isActiveAndEnabled && activeOneShot == null)
            {
                PlayLocomotionIfAvailable();
            }
        }

        public bool SetLocomotion(CharacterLocomotionState value)
        {
            locomotionState = value;
            AnimationClip locomotion = ResolveLocomotionClip(value);
            if (locomotion == null)
            {
                return false;
            }

            if (activeOneShot != null)
            {
                // Queue the new locomotion state without interrupting an action/emote.
                return true;
            }

            return TryPlay(locomotion);
        }

        public bool PlayOneShot(AnimationClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            activeOneShot = clip;
            return TryPlay(clip);
        }

        public bool CancelOneShot()
        {
            if (activeOneShot == null)
            {
                return false;
            }

            activeOneShot = null;
            return PlayLocomotionIfAvailable();
        }

        private void RefreshCurrentIntent()
        {
            if (activeOneShot != null)
            {
                TryPlay(activeOneShot);
                return;
            }

            PlayLocomotionIfAvailable();
        }

        private bool PlayLocomotionIfAvailable()
        {
            AnimationClip locomotion = ResolveLocomotionClip(locomotionState);
            return locomotion != null && TryPlay(locomotion);
        }

        private bool TryPlay(AnimationClip clip)
        {
            ResolvePlayerIfNeeded();
            if (player == null)
            {
                return false;
            }

            if (player.CurrentClip == clip && player.IsPlaying)
            {
                return true;
            }

            return player.Play(clip);
        }

        private AnimationClip ResolveLocomotionClip(CharacterLocomotionState value)
        {
            switch (value)
            {
                case CharacterLocomotionState.Idle:
                    return idleClip;
                case CharacterLocomotionState.Walk:
                    return walkClip;
                case CharacterLocomotionState.Run:
                    return runClip;
                case CharacterLocomotionState.CrouchIdle:
                    return crouchIdleClip != null ? crouchIdleClip : idleClip;
                default:
                    return idleClip;
            }
        }

        private void ResolvePlayerIfNeeded()
        {
            if (player == null)
            {
                player = GetComponent<CharacterAnimationPlayer>();
            }
        }
    }
}
