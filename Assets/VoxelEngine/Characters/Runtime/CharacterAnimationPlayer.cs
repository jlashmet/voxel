using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Plays arbitrary animation clips directly on a character Animator through Playables.
    /// The driver deliberately owns no clip catalogue, state machine, or placeholder-specific
    /// knowledge so the same runtime seam can drive temporary and generated Humanoid visuals.
    /// Resolver-driven visual swaps preserve both the requested clip and its playback time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationPlayer : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private CharacterVisualResolver visualResolver;

        private PlayableGraph graph;
        private AnimationClipPlayable currentPlayable;
        private AnimationClip currentClip;
        private double retainedTime;
        private bool resolverSubscribed;

        public Animator Animator => animator;

        public CharacterVisualResolver VisualResolver => visualResolver;

        public AnimationClip CurrentClip => currentClip;

        public double CurrentTime => currentPlayable.IsValid()
            ? currentPlayable.GetTime()
            : retainedTime;

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

        /// <summary>
        /// Explicitly replaces the animation target and stops the currently playing clip.
        /// Resolver-owned visual changes use a separate rebind path that preserves animation
        /// intent across generated/fallback visual swaps.
        /// </summary>
        public void SetVisual(GameObject visual)
        {
            SetAnimator(FindAnimator(visual));
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
            currentClip = clip;
            retainedTime = 0d;
            StartCurrentClip(retainedTime);
            return true;
        }

        public void Stop()
        {
            DestroyGraph(false);
            currentClip = null;
            retainedTime = 0d;
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
                animator = FindAnimator(visualResolver.CurrentVisual);
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
            RebindAnimator(FindAnimator(visual));
        }

        private void RebindAnimator(Animator value)
        {
            if (animator == value)
            {
                return;
            }

            DestroyGraph(true);
            animator = value;

            if (animator != null && currentClip != null)
            {
                StartCurrentClip(retainedTime);
            }
        }

        private void StartCurrentClip(double startTime)
        {
            graph = PlayableGraph.Create($"{name}: Character Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            currentPlayable = AnimationClipPlayable.Create(graph, currentClip);
            currentPlayable.SetTime(startTime);
            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(graph, "Character Animation", animator);
            output.SetSourcePlayable(currentPlayable);

            graph.Play();
        }

        private void DestroyGraph(bool preserveTime)
        {
            if (preserveTime && currentPlayable.IsValid())
            {
                retainedTime = currentPlayable.GetTime();
            }

            if (graph.IsValid())
            {
                graph.Destroy();
            }

            currentPlayable = default;
        }

        private static Animator FindAnimator(GameObject visual)
        {
            return visual != null
                ? visual.GetComponentInChildren<Animator>(true)
                : null;
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
