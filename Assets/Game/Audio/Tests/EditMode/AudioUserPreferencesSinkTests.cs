using System;
using Game.Application.Api;
using Game.Audio.Api;
using Game.Audio.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Audio.Tests
{
    public sealed class AudioUserPreferencesSinkTests
    {
        [Test]
        public void Apply_MapsCanonicalMasterVolumeAndPreservesAudioOwnedBusMix()
        {
            var backend = new RecordingBackend();
            var runtime = new AudioPresentationRuntime(
                new AudioCueCatalog(Array.Empty<AudioCueAssetBinding>()),
                backend);
            runtime.ApplyMix(new AudioMixPreferences(1f, 0.8f, 0.7f, 0.6f, 0.5f));
            IAudioPreferencesSink sink = new AudioUserPreferencesSink(runtime);

            sink.Apply(new UserPreferences(0.35f, 1.2f));

            Assert.That(runtime.CurrentMix.Master, Is.EqualTo(0.35f));
            Assert.That(runtime.CurrentMix.Sfx, Is.EqualTo(0.8f));
            Assert.That(runtime.CurrentMix.Music, Is.EqualTo(0.7f));
            Assert.That(runtime.CurrentMix.Ambience, Is.EqualTo(0.6f));
            Assert.That(runtime.CurrentMix.Voice, Is.EqualTo(0.5f));
            Assert.That(backend.AppliedMix.Master, Is.EqualTo(0.35f));

            sink.Apply(new UserPreferences(0.72f, 1f));

            Assert.That(runtime.CurrentMix.Master, Is.EqualTo(0.72f));
            Assert.That(backend.AppliedMix.Master, Is.EqualTo(0.72f));
            Assert.That(runtime.CurrentMix.Sfx, Is.EqualTo(0.8f), "Application owns the master preference only; Audio retains its bus mix.");
        }

        private sealed class RecordingBackend : IAudioPlaybackBackend
        {
            public AudioMixPreferences AppliedMix { get; private set; } = AudioMixPreferences.Default;
            public void PlayOneShot(AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain) { }
            public void StartSustained(SustainedAudioKey key, AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain) { }
            public void StopSustained(SustainedAudioKey key) { }
            public void ApplyMix(AudioMixPreferences preferences) => AppliedMix = preferences;
        }
    }
}
