using System;
using Game.Audio.Api;
using UnityEngine;

namespace Game.Audio.Runtime
{
    public static class AudioProductionBootstrap
    {
        public static AudioPresentationRuntime Create(GameObject compositionRoot, IAudioOriginResolver origins = null)
        {
            if (compositionRoot == null) throw new ArgumentNullException(nameof(compositionRoot));
            UnityAudioPlaybackBackend backend = compositionRoot.GetComponent<UnityAudioPlaybackBackend>() ?? compositionRoot.AddComponent<UnityAudioPlaybackBackend>();
            AudioClip cue = CreateTone("Semantic Cue", 660f, 0.18f, 0.24f);
            AudioClip ambience = CreateAmbientLoop();
            var catalog = new AudioCueCatalog(new[]
            {
                new AudioCueAssetBinding(AudioSemanticCues.CutsceneGeneric, cue, AudioBusKind.Sfx, false, false, 0.62f),
                new AudioCueAssetBinding(AudioSemanticCues.CharacterDefeated, cue, AudioBusKind.Sfx, false, false, 0.72f),
                new AudioCueAssetBinding(AudioSemanticCues.WorldAmbience, ambience, AudioBusKind.Ambience, false, true, 0.2f)
            });
            return new AudioPresentationRuntime(catalog, backend, origins);
        }

        private static AudioClip CreateTone(string name, float frequency, float seconds, float amplitude)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * seconds));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float fade = Mathf.Min(1f, i / (sampleRate * 0.01f), (samples - i - 1) / (sampleRate * 0.04f));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * Mathf.Max(0f, fade);
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateAmbientLoop()
        {
            const int sampleRate = 22050;
            const int samples = 22050;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                data[i] = 0.08f * Mathf.Sin(2f * Mathf.PI * 110f * t) + 0.035f * Mathf.Sin(2f * Mathf.PI * 165f * t);
            }
            AudioClip clip = AudioClip.Create("Semantic World Ambience", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
