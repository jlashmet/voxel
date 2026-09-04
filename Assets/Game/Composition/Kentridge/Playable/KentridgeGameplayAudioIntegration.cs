using Game.Audio.Api;
using Game.Audio.Runtime;
using Game.Characters.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>Client-local Kentridge audio adapter. It observes confirmed public character events and owns no gameplay authority.</summary>
    public sealed class KentridgeGameplayAudioIntegration : MonoBehaviour
    {
        private const string SceneName = "KentridgePlayableSlice";
        private AudioPresentationRuntime _audio;
        private ICharacterRegistry _characters;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterInstaller()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, SceneName, System.StringComparison.Ordinal)) return;
            GameObject root = GameObject.Find("Kentridge Player Camera");
            if (root == null && scene.GetRootGameObjects().Length > 0) root = scene.GetRootGameObjects()[0];
            if (root != null && root.GetComponent<KentridgeGameplayAudioIntegration>() == null)
                root.AddComponent<KentridgeGameplayAudioIntegration>();
        }

        private void OnEnable()
        {
            _audio = AudioProductionBootstrap.Create(gameObject);
            _audio.ReconcileSustained(new[]
            {
                new SustainedAudioState(new SustainedAudioKey("kentridge:world-ambience"), AudioSemanticCues.WorldAmbience, AudioSemanticOrigin.Global, true)
            });
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
            _audio.DispatchOneShot(new AudioOneShotRequest(
                new AudioEventId("character:defeated:" + change.Sequence),
                AudioSemanticCues.CharacterDefeated,
                AudioSemanticOrigin.ForCharacter(change.CharacterId)));
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
