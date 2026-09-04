using Game.Audio.Api;
using Game.Audio.Runtime;
using Game.Characters.Api;
using Game.Cutscenes.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Client-local Kentridge audio composition. One Audio runtime owns authored cutscene cues,
    /// confirmed gameplay semantic events, and current sustained ambience without receiving gameplay authority.
    /// </summary>
    public sealed class KentridgeGameplayAudioIntegration : MonoBehaviour, ICutsceneSoundCueRuntime
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DoorOpenCutsceneCue = "door.open";
        private AudioPresentationRuntime _audio;
        private ICharacterRegistry _characters;

        public AudioPresentationRuntime Presentation => _audio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterInstaller()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitialScene() => Install(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !string.Equals(scene.name, SceneName, System.StringComparison.Ordinal)) return;
            GameObject root = GameObject.Find("Kentridge Player Camera");
            if (root == null)
            {
                GameObject[] roots = scene.GetRootGameObjects();
                if (roots.Length > 0) root = roots[0];
            }
            if (root != null && root.GetComponent<KentridgeGameplayAudioIntegration>() == null)
                root.AddComponent<KentridgeGameplayAudioIntegration>();
        }

        private void OnEnable() => EnsureAudio();

        private void EnsureAudio()
        {
            if (_audio != null) return;
            _audio = AudioProductionBootstrap.Create(gameObject);
            _audio.ReconcileSustained(new[]
            {
                new SustainedAudioState(
                    new SustainedAudioKey("kentridge:world-ambience"),
                    AudioSemanticCues.WorldAmbience,
                    AudioSemanticOrigin.Global,
                    true)
            });
        }

        public ICutsceneOperation Execute(CutsceneCueId cue)
        {
            EnsureAudio();
            AudioCueRef semanticCue = string.Equals(cue.Value, DoorOpenCutsceneCue, System.StringComparison.Ordinal)
                ? AudioSemanticCues.DoorOpened
                : AudioSemanticCues.CutsceneGeneric;
            AudioDispatchResult result = _audio.DispatchOneShot(new AudioOneShotRequest(
                new AudioEventId("kentridge:cutscene:" + cue.Value),
                semanticCue,
                AudioSemanticOrigin.Global));
            Debug.Log(
                "[KentridgeAudio] cutscene-cue=" + cue.Value +
                " semantic=" + semanticCue.Value +
                " status=" + result.Status);
            return CompletedCutsceneOperation.Instance;
        }

        private void Update()
        {
            if (_characters != null) return;
            KentridgeCharacterRegistryAnchor anchor = GetComponent<KentridgeCharacterRegistryAnchor>();
            if (anchor == null) anchor = FindFirstObjectByType<KentridgeCharacterRegistryAnchor>();
            if (anchor == null || anchor.Characters == null) return;
            _characters = anchor.Characters;
            _characters.Changed += OnCharacterChanged;
        }

        private void OnCharacterChanged(CharacterEvent change)
        {
            if (change.Kind != CharacterEventKind.Defeated) return;
            EnsureAudio();
            AudioDispatchResult result = _audio.DispatchOneShot(new AudioOneShotRequest(
                new AudioEventId("character:defeated:" + change.Sequence),
                AudioSemanticCues.CharacterDefeated,
                AudioSemanticOrigin.ForCharacter(change.CharacterId)));
            Debug.Log(
                "[KentridgeAudio] gameplay-cue=" + AudioSemanticCues.CharacterDefeated.Value +
                " character=" + change.CharacterId.Value +
                " status=" + result.Status);
        }

        private void OnDisable()
        {
            if (_characters != null) _characters.Changed -= OnCharacterChanged;
            _characters = null;
            _audio?.Dispose();
            _audio = null;
        }
    }
}
