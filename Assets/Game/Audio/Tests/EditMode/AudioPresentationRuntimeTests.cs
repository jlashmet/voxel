using System.Collections.Generic;
using Game.Audio.Api;
using Game.Audio.Runtime;
using Game.Characters.Api;
using Game.Cutscenes.Api;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Audio.Tests
{
    public sealed class AudioPresentationRuntimeTests
    {
        private AudioClip _clip;
        [SetUp] public void SetUp() => _clip = AudioClip.Create("audio-test", 64, 1, 8000, false);
        [TearDown] public void TearDown() { if (_clip != null) Object.DestroyImmediate(_clip); }

        [Test]
        public void SemanticMapping_UnknownCueConfigurationAndOriginFailureArePresentationOnly()
        {
            Assert.Throws<System.InvalidOperationException>(() => new AudioCueCatalog(new[]
            {
                new AudioCueAssetBinding(Cue("duplicate"), _clip, AudioBusKind.Sfx, false, false),
                new AudioCueAssetBinding(Cue("duplicate"), _clip, AudioBusKind.Sfx, false, false)
            }));
            var backend = new RecordingBackend();
            var runtime = Runtime(backend, new RejectCharacterOrigin());
            AudioDispatchResult known = runtime.DispatchOneShot(new AudioOneShotRequest(new AudioEventId("known"), Cue("hit"), AudioSemanticOrigin.Global));
            AudioDispatchResult unknown = runtime.DispatchOneShot(new AudioOneShotRequest(new AudioEventId("unknown"), Cue("missing"), AudioSemanticOrigin.Global));
            AudioDispatchResult missingOrigin = runtime.DispatchOneShot(new AudioOneShotRequest(new AudioEventId("origin"), Cue("spatial"), AudioSemanticOrigin.ForCharacter(new CharacterId("character:test"))));
            Assert.That(known.Status, Is.EqualTo(AudioDispatchStatus.Played));
            Assert.That(unknown.Status, Is.EqualTo(AudioDispatchStatus.UnknownCue));
            Assert.That(missingOrigin.Status, Is.EqualTo(AudioDispatchStatus.OriginUnavailable));
            Assert.That(backend.OneShots, Is.EqualTo(1));
        }

        [Test]
        public void PredictedAndAuthoritativeDelivery_WithSameStableEventId_PlaysOnce()
        {
            var backend = new RecordingBackend();
            var runtime = Runtime(backend);
            var id = new AudioEventId("combat:42:impact");
            Assert.That(runtime.DispatchOneShot(new AudioOneShotRequest(id, Cue("hit"), AudioSemanticOrigin.Global, true)).Status, Is.EqualTo(AudioDispatchStatus.Played));
            Assert.That(runtime.DispatchOneShot(new AudioOneShotRequest(id, Cue("hit"), AudioSemanticOrigin.Global, false)).Status, Is.EqualTo(AudioDispatchStatus.DuplicateSuppressed));
            Assert.That(backend.OneShots, Is.EqualTo(1));
        }

        [Test]
        public void Reconnect_ReconstructsCurrentSustainedStateWithoutHistoricalOneShots()
        {
            var firstBackend = new RecordingBackend();
            var first = Runtime(firstBackend);
            first.DispatchOneShot(new AudioOneShotRequest(new AudioEventId("history:damage"), Cue("hit"), AudioSemanticOrigin.Global));
            var current = new[] { new SustainedAudioState(new SustainedAudioKey("world:ambience"), Cue("ambience"), AudioSemanticOrigin.Global, true) };
            first.ReconcileSustained(current);
            var rebuiltBackend = new RecordingBackend();
            var rebuilt = Runtime(rebuiltBackend);
            rebuilt.ReconcileSustained(current);
            rebuilt.ReconcileSustained(current);
            Assert.That(rebuiltBackend.OneShots, Is.Zero, "Reconnect consumes current sustained descriptors only; historical one-shots are not an input.");
            Assert.That(rebuiltBackend.StartedLoops, Is.EqualTo(1), "Repeated current-state reconciliation must be idempotent.");
        }

        [Test]
        public void CutsceneAndConfirmedGameplay_UseSameAudioPresentationAndDedupeOwner()
        {
            var backend = new RecordingBackend();
            var runtime = Runtime(backend);
            var cutscene = new CutsceneAudioCueRuntime(runtime, Cue("hit"));
            cutscene.Execute(new CutsceneCueId("opening.bell"));
            cutscene.Execute(new CutsceneCueId("opening.bell"));
            CharacterId target = new CharacterId("character:bandit");
            var vitality = new VitalityRegistry();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(target, 3)), Is.True);
            using (new VitalityDefeatAudioAdapter(vitality, runtime, Cue("hit")))
                vitality.ApplyDamage(new DamageRequest(target, 3));
            Assert.That(backend.OneShots, Is.EqualTo(2), "One cutscene cue and one confirmed gameplay defeat share the same playback backend; duplicate cutscene delivery is suppressed.");
        }

        [Test]
        public void MixPreferences_ApplyLocallyWithoutChangingSemanticState()
        {
            var backend = new RecordingBackend();
            var runtime = Runtime(backend);
            runtime.ReconcileSustained(new[] { new SustainedAudioState(new SustainedAudioKey("ambience"), Cue("ambience"), AudioSemanticOrigin.Global, true) });
            runtime.ApplyMix(new AudioMixPreferences(0.5f, 0.4f, 0.3f, 0.2f, 0.1f));
            Assert.That(runtime.SustainedCount, Is.EqualTo(1));
            Assert.That(backend.AppliedMix.Master, Is.EqualTo(0.5f));
            Assert.That(backend.AppliedMix.Ambience, Is.EqualTo(0.2f));
        }

        [Test]
        public void AudioAbsent_DoesNotChangeAuthoritativeVitalityResult()
        {
            CharacterId target = new CharacterId("character:headless");
            var vitality = new VitalityRegistry();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(target, 5)), Is.True);
            DamageResult result = vitality.ApplyDamage(new DamageRequest(target, 2));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.State.Current, Is.EqualTo(3));
        }

        private AudioPresentationRuntime Runtime(RecordingBackend backend, IAudioOriginResolver resolver = null)
        {
            return new AudioPresentationRuntime(new AudioCueCatalog(new[]
            {
                new AudioCueAssetBinding(Cue("hit"), _clip, AudioBusKind.Sfx, false, false),
                new AudioCueAssetBinding(Cue("spatial"), _clip, AudioBusKind.Sfx, true, false),
                new AudioCueAssetBinding(Cue("ambience"), _clip, AudioBusKind.Ambience, false, true)
            }), backend, resolver);
        }
        private static AudioCueRef Cue(string value) => new AudioCueRef(value);

        private sealed class RejectCharacterOrigin : IAudioOriginResolver
        { public bool TryResolve(AudioSemanticOrigin origin, out Vector3 position) { position = default; return origin.Kind != AudioOriginKind.Character; } }
        private sealed class RecordingBackend : IAudioPlaybackBackend
        {
            public int OneShots { get; private set; }
            public int StartedLoops { get; private set; }
            public AudioMixPreferences AppliedMix { get; private set; } = AudioMixPreferences.Default;
            public readonly HashSet<SustainedAudioKey> Loops = new HashSet<SustainedAudioKey>();
            public void PlayOneShot(AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain) => OneShots++;
            public void StartSustained(SustainedAudioKey key, AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain) { if (Loops.Add(key)) StartedLoops++; }
            public void StopSustained(SustainedAudioKey key) => Loops.Remove(key);
            public void ApplyMix(AudioMixPreferences preferences) => AppliedMix = preferences;
        }
    }
}
