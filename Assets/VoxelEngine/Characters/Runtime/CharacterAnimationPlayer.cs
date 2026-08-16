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

        [SerializeField]
        private CharacterVisualResolver visualResolver;

        private PlayableGraph graph;
        private AnimationClip currentClip;
        private bool resolverSubscribed;

        public Animator Animator => animator;

        public CharacterVisualResolver VisualResolver => visualResolver;

        public AnimationClip CurrentClip => currentClip;

        public bool IsPlaying => graph.IsValid() && graph.IsPlaying();

        private void Awake()
        {
            ResolveVisualResolverIfNeeded();
            SubscribeToResolver();
            ResolveAnimatorIfNeeded();
        }

        private void OnEnable()
        {
            ResolveVisualResolverIfNeeded();
            SubscribeToResolver();

            if (visualResolver != null && visualResolver.CurrentVisual != null)
            {
                SetVisual(visualResolver.CurrentVisual);
            }
            else
            {
                ResolveAnimatorIfNeeded();
            }
        }

        public void SetVisualResolver(CharacterVisualResolver value)
        {
            if (visualResolver == value)
            {
                return;
            }

            UnsubscribeFromResolver();
            Stop();
            animator = null;
            visualResolver = value;
            SubscribeToResolver();
            ResolveAnimatorIfNeeded();
        }

        public void SetVisual(GameObject visual)
        {
            SetAnimator(visual != null
                ? visual.GetComponentInChildren<Animator>(true)
                : null);
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

        private void ResolveVisualResolverIfNeeded()
        {
            if (visualResolver == null)
            {
                visualResolver = GetComponent<CharacterVisualResolver>();
            }
        }

        private void ResolveAnimatorIfNeeded()
        {
            if (animator != null)
            {
                return;
            }

            if (visualResolver != null && visualResolver.CurrentVisual != null)
            {
                animator = visualResolver.CurrentVisual.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    return;
                }
            }

            animator = GetComponentInChildren<Animator>(true);
        }

        private void SubscribeToResolver()
        {
            if (resolverSubscribed || visualResolver == null || !isActiveAndEnabled)
            {
                return;
            }

            visualResolver.VisualChanged += HandleVisualChanged;
            resolverSubscribed = true;
        }

        private void UnsubscribeFromResolver()
        {
            if (!resolverSubscribed || visualResolver == null)
            {
                resolverSubscribed = false;
                return;
            }

            visualResolver.VisualChanged -= HandleVisualChanged;
            resolverSubscribed = false;
        }

        private void HandleVisualChanged(GameObject visual)
        {
            SetVisual(visual);
        }

        private void OnDisable()
        {
            UnsubscribeFromResolver();
            Stop();
        }

        private void OnDestroy()
        {
            UnsubscribeFromResolver();
            Stop();
        }
    }
}
