using System;
using System.Collections.Generic;
using Game.Audio.Api;
using UnityEngine;

namespace Game.Audio.Runtime
{
    public sealed class UnityAudioPlaybackBackend : MonoBehaviour, IAudioPlaybackBackend
    {
        private sealed class OneShot
        {
            public AudioSource Source;
            public AudioBusKind Bus;
            public float BaseVolume;
            public float DestroyAt;
        }
        private sealed class Loop
        {
            public AudioSource Source;
            public AudioBusKind Bus;
            public float BaseVolume;
        }

        private readonly List<OneShot> _oneShots = new List<OneShot>();
        private readonly Dictionary<SustainedAudioKey, Loop> _loops = new Dictionary<SustainedAudioKey, Loop>();
        private AudioMixPreferences _mix = AudioMixPreferences.Default;

        public int AudibleOneShotCount => _oneShots.Count;
        public int ActiveLoopCount => _loops.Count;

        public void PlayOneShot(AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            GameObject host = new GameObject("Audio OneShot • " + binding.Cue.Value);
            host.transform.SetParent(transform, false);
            if (hasPosition) host.transform.position = position;
            AudioSource source = host.AddComponent<AudioSource>();
            Configure(source, binding, hasPosition, loop: false, gain);
            source.Play();
            _oneShots.Add(new OneShot { Source = source, Bus = binding.Bus, BaseVolume = binding.BaseVolume, DestroyAt = Time.unscaledTime + Mathf.Max(0.1f, binding.Clip.length + 0.1f) });
        }

        public void StartSustained(SustainedAudioKey key, AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain)
        {
            if (_loops.ContainsKey(key)) return;
            GameObject host = new GameObject("Audio Sustained • " + key.Value);
            host.transform.SetParent(transform, false);
            if (hasPosition) host.transform.position = position;
            AudioSource source = host.AddComponent<AudioSource>();
            Configure(source, binding, hasPosition, loop: true, gain);
            source.Play();
            _loops.Add(key, new Loop { Source = source, Bus = binding.Bus, BaseVolume = binding.BaseVolume });
        }

        public void StopSustained(SustainedAudioKey key)
        {
            if (!_loops.TryGetValue(key, out Loop loop)) return;
            _loops.Remove(key);
            if (loop.Source != null) Destroy(loop.Source.gameObject);
        }

        public void ApplyMix(AudioMixPreferences preferences)
        {
            _mix = preferences;
            for (int i = 0; i < _oneShots.Count; i++)
                if (_oneShots[i].Source != null) _oneShots[i].Source.volume = _oneShots[i].BaseVolume * _mix.GainFor(_oneShots[i].Bus);
            foreach (Loop loop in _loops.Values)
                if (loop.Source != null) loop.Source.volume = loop.BaseVolume * _mix.GainFor(loop.Bus);
        }

        private void Update()
        {
            for (int i = _oneShots.Count - 1; i >= 0; i--)
            {
                OneShot shot = _oneShots[i];
                if (shot.Source != null && Time.unscaledTime < shot.DestroyAt) continue;
                if (shot.Source != null) Destroy(shot.Source.gameObject);
                _oneShots.RemoveAt(i);
            }
        }

        private static void Configure(AudioSource source, AudioCueAssetBinding binding, bool spatial, bool loop, float gain)
        {
            source.playOnAwake = false;
            source.clip = binding.Clip;
            source.loop = loop;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 35f;
            source.dopplerLevel = 0f;
            source.volume = Mathf.Clamp01(gain);
        }
    }
}
