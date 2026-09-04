using System;
using System.Collections.Generic;
using Game.Audio.Api;
using Game.Cutscenes.Api;
using Game.Vitality.Api;
using UnityEngine;

namespace Game.Audio.Runtime
{
    public static class AudioSemanticCues
    {
        public static readonly AudioCueRef CutsceneGeneric = new AudioCueRef("cutscene.semantic");
        public static readonly AudioCueRef DoorOpened = new AudioCueRef("world.door.opened");
        public static readonly AudioCueRef CharacterDefeated = new AudioCueRef("gameplay.character.defeated");
        public static readonly AudioCueRef WorldAmbience = new AudioCueRef("world.ambience.current");
    }

    public sealed class AudioCueAssetBinding
    {
        public AudioCueRef Cue { get; }
        public AudioClip Clip { get; }
        public AudioBusKind Bus { get; }
        public bool Spatial { get; }
        public bool AllowLoop { get; }
        public float BaseVolume { get; }
        public AudioCueAssetBinding(AudioCueRef cue, AudioClip clip, AudioBusKind bus, bool spatial, bool allowLoop, float baseVolume = 1f)
        {
            if (!cue.IsValid) throw new ArgumentException("Cue is required.", nameof(cue));
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            if (float.IsNaN(baseVolume) || float.IsInfinity(baseVolume) || baseVolume < 0f) throw new ArgumentOutOfRangeException(nameof(baseVolume));
            Cue = cue; Bus = bus; Spatial = spatial; AllowLoop = allowLoop; BaseVolume = baseVolume;
        }
    }

    public sealed class AudioCueCatalog
    {
        private readonly Dictionary<AudioCueRef, AudioCueAssetBinding> _bindings = new Dictionary<AudioCueRef, AudioCueAssetBinding>();
        public AudioCueCatalog(IEnumerable<AudioCueAssetBinding> bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            foreach (AudioCueAssetBinding binding in bindings)
            {
                if (binding == null) throw new InvalidOperationException("Audio cue configuration contains a null binding.");
                if (_bindings.ContainsKey(binding.Cue)) throw new InvalidOperationException("Duplicate semantic audio cue mapping: " + binding.Cue.Value);
                _bindings.Add(binding.Cue, binding);
            }
        }
        public bool TryResolve(AudioCueRef cue, out AudioCueAssetBinding binding) => _bindings.TryGetValue(cue, out binding);
    }

    public interface IAudioOriginResolver
    {
        bool TryResolve(AudioSemanticOrigin origin, out Vector3 position);
    }

    public sealed class DefaultAudioOriginResolver : IAudioOriginResolver
    {
        public bool TryResolve(AudioSemanticOrigin origin, out Vector3 position)
        {
            if (origin.Kind == AudioOriginKind.Global) { position = Vector3.zero; return true; }
            if (origin.Kind == AudioOriginKind.WorldPoint)
            {
                position = new Vector3(origin.WorldPoint.XDecimetres * 0.1f, origin.WorldPoint.YDecimetres * 0.1f, origin.WorldPoint.ZDecimetres * 0.1f);
                return true;
            }
            position = default;
            return false;
        }
    }

    public interface IAudioPlaybackBackend
    {
        void PlayOneShot(AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain);
        void StartSustained(SustainedAudioKey key, AudioCueAssetBinding binding, Vector3 position, bool hasPosition, float gain);
        void StopSustained(SustainedAudioKey key);
        void ApplyMix(AudioMixPreferences preferences);
    }

    public sealed class AudioPresentationRuntime : IAudioPresentation, IDisposable
    {
        private readonly AudioCueCatalog _catalog;
        private readonly IAudioOriginResolver _origins;
        private readonly IAudioPlaybackBackend _backend;
        private readonly HashSet<AudioEventId> _playedEvents = new HashSet<AudioEventId>();
        private readonly Dictionary<SustainedAudioKey, SustainedAudioState> _sustained = new Dictionary<SustainedAudioKey, SustainedAudioState>();
        private AudioMixPreferences _mix = AudioMixPreferences.Default;
        private bool _disposed;
        public event Action<AudioDispatchResult> Diagnostic;
        public int PlayedEventCount => _playedEvents.Count;
        public int SustainedCount => _sustained.Count;

        public AudioPresentationRuntime(AudioCueCatalog catalog, IAudioPlaybackBackend backend, IAudioOriginResolver origins = null)
        { _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); _backend = backend ?? throw new ArgumentNullException(nameof(backend)); _origins = origins ?? new DefaultAudioOriginResolver(); _backend.ApplyMix(_mix); }

        public AudioDispatchResult DispatchOneShot(AudioOneShotRequest request)
        {
            ThrowIfDisposed();
            if (_playedEvents.Contains(request.EventId)) return Emit(AudioDispatchStatus.DuplicateSuppressed, request.EventId.Value);
            if (!_catalog.TryResolve(request.Cue, out AudioCueAssetBinding binding)) return Emit(AudioDispatchStatus.UnknownCue, request.Cue.Value);
            if (!TryOrigin(binding, request.Origin, out Vector3 position, out bool hasPosition)) return Emit(AudioDispatchStatus.OriginUnavailable, request.Cue.Value);
            try
            {
                _backend.PlayOneShot(binding, position, hasPosition, binding.BaseVolume * _mix.GainFor(binding.Bus));
                _playedEvents.Add(request.EventId);
                return Emit(AudioDispatchStatus.Played, request.Cue.Value);
            }
            catch (Exception ex) { return Emit(AudioDispatchStatus.BackendFailure, ex.Message); }
        }

        public AudioDispatchResult ReconcileSustained(IReadOnlyList<SustainedAudioState> currentState)
        {
            ThrowIfDisposed();
            if (currentState == null) throw new ArgumentNullException(nameof(currentState));
            var desired = new Dictionary<SustainedAudioKey, SustainedAudioState>();
            for (int i = 0; i < currentState.Count; i++)
            {
                SustainedAudioState state = currentState[i];
                if (!state.Active) continue;
                if (desired.ContainsKey(state.Key)) throw new InvalidOperationException("Duplicate sustained audio key: " + state.Key.Value);
                desired.Add(state.Key, state);
            }
            var stop = new List<SustainedAudioKey>();
            foreach (KeyValuePair<SustainedAudioKey, SustainedAudioState> pair in _sustained)
                if (!desired.TryGetValue(pair.Key, out SustainedAudioState next) || !pair.Value.Equals(next)) stop.Add(pair.Key);
            for (int i = 0; i < stop.Count; i++) { _backend.StopSustained(stop[i]); _sustained.Remove(stop[i]); }
            foreach (KeyValuePair<SustainedAudioKey, SustainedAudioState> pair in desired)
            {
                if (_sustained.ContainsKey(pair.Key)) continue;
                if (!_catalog.TryResolve(pair.Value.Cue, out AudioCueAssetBinding binding)) { Emit(AudioDispatchStatus.UnknownCue, pair.Value.Cue.Value); continue; }
                if (!binding.AllowLoop) { Emit(AudioDispatchStatus.BackendFailure, "Cue is not configured for sustained playback: " + pair.Value.Cue.Value); continue; }
                if (!TryOrigin(binding, pair.Value.Origin, out Vector3 position, out bool hasPosition)) { Emit(AudioDispatchStatus.OriginUnavailable, pair.Value.Cue.Value); continue; }
                try
                {
                    _backend.StartSustained(pair.Key, binding, position, hasPosition, binding.BaseVolume * _mix.GainFor(binding.Bus));
                    _sustained.Add(pair.Key, pair.Value);
                }
                catch (Exception ex) { Emit(AudioDispatchStatus.BackendFailure, ex.Message); }
            }
            return Emit(AudioDispatchStatus.Reconciled, "sustained=" + _sustained.Count);
        }

        public void ApplyMix(AudioMixPreferences preferences)
        { ThrowIfDisposed(); _mix = preferences; _backend.ApplyMix(preferences); }

        public void Dispose()
        {
            if (_disposed) return;
            foreach (SustainedAudioKey key in new List<SustainedAudioKey>(_sustained.Keys)) _backend.StopSustained(key);
            _sustained.Clear(); _playedEvents.Clear(); _disposed = true;
        }

        private bool TryOrigin(AudioCueAssetBinding binding, AudioSemanticOrigin origin, out Vector3 position, out bool hasPosition)
        {
            if (!binding.Spatial) { position = Vector3.zero; hasPosition = false; return true; }
            hasPosition = _origins.TryResolve(origin, out position);
            return hasPosition;
        }
        private AudioDispatchResult Emit(AudioDispatchStatus status, string diagnostic)
        { var result = new AudioDispatchResult(status, diagnostic); Diagnostic?.Invoke(result); return result; }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(AudioPresentationRuntime)); }
    }

    public sealed class CutsceneAudioCueRuntime : ICutsceneSoundCueRuntime
    {
        private readonly IAudioPresentation _audio;
        private readonly AudioCueRef _cue;
        public CutsceneAudioCueRuntime(IAudioPresentation audio, AudioCueRef cue)
        { _audio = audio ?? throw new ArgumentNullException(nameof(audio)); _cue = cue; }
        public ICutsceneOperation Execute(CutsceneCueId cue)
        {
            _audio.DispatchOneShot(new AudioOneShotRequest(new AudioEventId("cutscene:" + cue.Value), _cue, AudioSemanticOrigin.Global));
            return CompletedCutsceneOperation.Instance;
        }
    }

    public sealed class VitalityDefeatAudioAdapter : IDisposable
    {
        private readonly IVitalityService _vitality;
        private readonly IAudioPresentation _audio;
        private readonly AudioCueRef _cue;
        public VitalityDefeatAudioAdapter(IVitalityService vitality, IAudioPresentation audio, AudioCueRef cue)
        {
            _vitality = vitality ?? throw new ArgumentNullException(nameof(vitality));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _cue = cue;
            _vitality.Defeated += OnDefeated;
        }
        private void OnDefeated(DefeatEvent defeat)
        {
            _audio.DispatchOneShot(new AudioOneShotRequest(
                new AudioEventId("vitality:defeat:" + defeat.CharacterId.Value + ":" + defeat.State.Revision),
                _cue,
                AudioSemanticOrigin.Global));
        }
        public void Dispose() => _vitality.Defeated -= OnDefeated;
    }
}
