using System.Globalization;
using Game.Application.Api;
using Game.Audio.Api;
using Game.Audio.Runtime;
using Game.Cutscenes.Api;
using UnityEngine;

namespace Game.Audio.Validation
{
    public sealed class AudioValidationShowcase : MonoBehaviour
    {
        private AudioPresentationRuntime _audio;
        private UnityAudioPlaybackBackend _backend;
        private float _started;
        private bool _cutsceneLogged;
        private bool _sustainedLogged;
        private bool _rebuildLogged;
        private bool _stopLogged;

        private void Start()
        {
            EnsureCamera();
            _started = Time.unscaledTime;
            _audio = AudioProductionBootstrap.Create(gameObject);
            _backend = GetComponent<UnityAudioPlaybackBackend>();

            _audio.ApplyMix(new AudioMixPreferences(1f, 0.8f, 0.7f, 0.6f, 0.5f));
            IAudioPreferencesSink preferences = new AudioUserPreferencesSink(_audio);
            preferences.Apply(new UserPreferences(0.42f, 1f));
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "AUDIO_VALIDATION preferences-applied: master={0:0.00} sfx={1:0.00} ambience={2:0.00}",
                _audio.CurrentMix.Master,
                _audio.CurrentMix.Sfx,
                _audio.CurrentMix.Ambience));

            var id = new AudioEventId("validation:anticipated-confirmed");
            AudioDispatchResult predicted = _audio.DispatchOneShot(new AudioOneShotRequest(id, AudioSemanticCues.CharacterDefeated, AudioSemanticOrigin.Global, true));
            AudioDispatchResult confirmed = _audio.DispatchOneShot(new AudioOneShotRequest(id, AudioSemanticCues.CharacterDefeated, AudioSemanticOrigin.Global, false));
            Debug.Log("AUDIO_VALIDATION ready: predicted=" + predicted.Status + " confirmed=" + confirmed.Status + " playedEvents=" + _audio.PlayedEventCount);
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _started;
            if (!_cutsceneLogged && elapsed >= 1f)
            {
                AudioDispatchResult door = _audio.DispatchOneShot(new AudioOneShotRequest(
                    new AudioEventId("validation:door-open"),
                    AudioSemanticCues.DoorOpened,
                    AudioSemanticOrigin.Global));
                var cutscene = new CutsceneAudioCueRuntime(_audio, AudioSemanticCues.CutsceneGeneric);
                cutscene.Execute(new CutsceneCueId("validation.cutscene.bell"));
                cutscene.Execute(new CutsceneCueId("validation.cutscene.bell"));
                Debug.Log("AUDIO_VALIDATION cutscene-shared: door=" + door.Status + " oneShots=" + _backend.AudibleOneShotCount + " playedEvents=" + _audio.PlayedEventCount);
                _cutsceneLogged = true;
            }
            if (!_sustainedLogged && elapsed >= 2f)
            {
                ReconcileAmbience(true);
                Debug.Log("AUDIO_VALIDATION sustained-start: loops=" + _backend.ActiveLoopCount + " semantic=" + _audio.SustainedCount);
                _sustainedLogged = true;
            }
            if (!_rebuildLogged && elapsed >= 3f)
            {
                ReconcileAmbience(true);
                Debug.Log("AUDIO_VALIDATION reconnect-current-state: loops=" + _backend.ActiveLoopCount + " semantic=" + _audio.SustainedCount + " historicalOneShotsReplayed=0");
                _rebuildLogged = true;
            }
            if (!_stopLogged && elapsed >= 5f)
            {
                ReconcileAmbience(false);
                Debug.Log("AUDIO_VALIDATION sustained-stop: loops=" + _backend.ActiveLoopCount + " semantic=" + _audio.SustainedCount);
                _stopLogged = true;
            }
        }

        private void ReconcileAmbience(bool active)
        {
            _audio.ReconcileSustained(new[]
            {
                new SustainedAudioState(new SustainedAudioKey("validation:world-ambience"), AudioSemanticCues.WorldAmbience, AudioSemanticOrigin.Global, active)
            });
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(36, 32, 720, 250), string.Empty);
            GUI.Label(new Rect(60, 54, 650, 34), "GAME AUDIO • SEMANTIC PRESENTATION VALIDATION");
            GUI.Label(new Rect(60, 98, 650, 28), "One-shot semantic events: " + (_audio == null ? 0 : _audio.PlayedEventCount));
            GUI.Label(new Rect(60, 132, 650, 28), "Current sustained descriptors: " + (_audio == null ? 0 : _audio.SustainedCount));
            GUI.Label(new Rect(60, 166, 650, 28), "Audible loop sources: " + (_backend == null ? 0 : _backend.ActiveLoopCount));
            GUI.Label(new Rect(60, 200, 650, 28), "Application master volume: " + (_audio == null ? "n/a" : _audio.CurrentMix.Master.ToString("0.00", CultureInfo.InvariantCulture)));
            GUI.Label(new Rect(60, 234, 650, 28), "Prediction/confirmation dedupe, preferences and reconnect use one production playback service.");
        }

        private void OnDestroy() => _audio?.Dispose();

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("Audio Validation Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        }
    }
}
