using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Plays arbitrary animation clips directly on a character Animator through Playables.
    /// The driver deliberately owns no clip catalogue, state machine, or placeholder-specific
    /// knowledge so the same runtime seam can drive temporary and generated Humanoid visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationPlayer : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;

        private PlayableGraph graph;
        private AnimationClip currentClip;

        public Animator Animator => animator;

        public AnimationClip CurrentClip => currentClip;

        public bool IsPlaying => graph.IsValid() && graph.IsPlaying();

        private void Awake()
        {
            ResolveAnimatorIfNeeded();
        }

        public void SetAnimator(Animator value)
        {
            if (animator == value)
            {
                return;
            }

            Stop();
            animator = value;
        }

        public bool Play(AnimationClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            ResolveAnimatorIfNeeded();
            if (animator == null)
            {
                return false;
            }

            Stop();

            graph = PlayableGraph.Create($"{name}: Character Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(graph, "Character Animation", animator);
            output.SetSourcePlayable(clipPlayable);

            currentClip = clip;
            graph.Play();
            return true;
        }

        public void Stop()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }

            currentClip = null;
        }

        private void ResolveAnimatorIfNeeded()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
