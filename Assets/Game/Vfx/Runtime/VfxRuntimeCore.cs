using System;
using System.Collections.Generic;
using Game.Combat.Api;
using Game.Outcomes.Api;
using Game.Vfx.Api;
using Game.Vitality.Api;
using Game.WorldObjects.Api;
using VoxelEngine.Edits.Api;

namespace Game.Vfx.Runtime
{
    public static class GameplayVfxCues
    {
        public static readonly VfxCueRef Hit = new VfxCueRef("combat.hit");
        public static readonly VfxCueRef Defeat = new VfxCueRef("combat.defeat");
        public static readonly VfxCueRef DefeatedTreatment = new VfxCueRef("combat.defeated");
        public static readonly VfxCueRef Interaction = new VfxCueRef("interaction.success");
        public static readonly VfxCueRef CombatResolved = new VfxCueRef("combat.resolved");
        public static readonly VfxCueRef OutcomeResolved = new VfxCueRef("outcome.resolved");
        public static readonly VfxCueRef VoxelDestruction = new VfxCueRef("world.voxel-destruction");
    }

    public enum VfxEffectStyle : byte { Impact = 0, DefeatBurst = 1, DefeatedAura = 2, InteractionPulse = 3, ResolutionBurst = 4, Debris = 5 }

    public readonly struct VfxEffectProfile
    {
        public VfxEffectStyle Style { get; }
        public float LifetimeSeconds { get; }
        public float Scale { get; }
        public int ParticleCount { get; }
        public bool Persistent { get; }
        public VfxEffectProfile(VfxEffectStyle style, float lifetimeSeconds, float scale, int particleCount, bool persistent = false)
        {
            if (lifetimeSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
            if (scale <= 0f) throw new ArgumentOutOfRangeException(nameof(scale));
            if (particleCount <= 0) throw new ArgumentOutOfRangeException(nameof(particleCount));
            Style = style; LifetimeSeconds = lifetimeSeconds; Scale = scale; ParticleCount = particleCount; Persistent = persistent;
        }
    }

    public sealed class VfxCueCatalog
    {
        private readonly Dictionary<VfxCueRef, VfxEffectProfile> _profiles = new Dictionary<VfxCueRef, VfxEffectProfile>();
        public VfxCueCatalog Add(VfxCueRef cue, VfxEffectProfile profile) { _profiles[cue] = profile; return this; }
        public bool TryResolve(VfxCueRef cue, out VfxEffectProfile profile) => _profiles.TryGetValue(cue, out profile);
        public static VfxCueCatalog CreateDefault() => new VfxCueCatalog()
            .Add(GameplayVfxCues.Hit, new VfxEffectProfile(VfxEffectStyle.Impact, 0.8f, 1f, 28))
            .Add(GameplayVfxCues.Defeat, new VfxEffectProfile(VfxEffectStyle.DefeatBurst, 1.5f, 1.5f, 64))
            .Add(GameplayVfxCues.DefeatedTreatment, new VfxEffectProfile(VfxEffectStyle.DefeatedAura, 1.2f, 1.2f, 20, true))
            .Add(GameplayVfxCues.Interaction, new VfxEffectProfile(VfxEffectStyle.InteractionPulse, 1f, 1f, 32))
            .Add(GameplayVfxCues.CombatResolved, new VfxEffectProfile(VfxEffectStyle.ResolutionBurst, 1.6f, 1.8f, 72))
            .Add(GameplayVfxCues.OutcomeResolved, new VfxEffectProfile(VfxEffectStyle.ResolutionBurst, 1.6f, 1.8f, 72))
            .Add(GameplayVfxCues.VoxelDestruction, new VfxEffectProfile(VfxEffectStyle.Debris, 1.4f, 1.3f, 46));
    }

    public interface IVfxEffectBackend
    {
        void PlayOneShot(VfxEffectProfile profile, VfxWorldPoint point);
        void ApplyPersistent(VfxTreatmentId treatmentId, VfxEffectProfile profile, VfxWorldPoint point);
        void RemovePersistent(VfxTreatmentId treatmentId);
    }

    public sealed class VfxCueCoordinator : IVfxCueSink, IVfxTreatmentSink
    {
        private readonly VfxCueCatalog _catalog;
        private readonly IVfxPresentationBindingResolver _bindings;
        private readonly IVfxEffectBackend _backend;
        private readonly IVfxDiagnosticsSink _diagnostics;
        private readonly HashSet<VfxEventId> _played = new HashSet<VfxEventId>();
        private readonly HashSet<VfxTreatmentId> _activeTreatments = new HashSet<VfxTreatmentId>();

        public VfxCueCoordinator(VfxCueCatalog catalog, IVfxPresentationBindingResolver bindings, IVfxEffectBackend backend, IVfxDiagnosticsSink diagnostics = null)
        { _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); _backend = backend ?? throw new ArgumentNullException(nameof(backend)); _diagnostics = diagnostics; }

        public int PlayedIdentityCount => _played.Count;
        public int ActiveTreatmentCount => _activeTreatments.Count;

        public VfxSubmitResult Submit(VfxCueRequest request)
        {
            if (!request.Cue.IsValid || !request.EventId.IsValid)
            { Report(VfxDiagnosticCode.InvalidRequest, request.Cue, request.EventId, "Invalid semantic VFX request."); return VfxSubmitResult.Invalid; }
            if (_played.Contains(request.EventId)) return VfxSubmitResult.Deduplicated;
            if (!_catalog.TryResolve(request.Cue, out VfxEffectProfile profile))
            { Report(VfxDiagnosticCode.MissingCueMapping, request.Cue, request.EventId, "No local Unity effect mapping exists for semantic cue."); return VfxSubmitResult.MissingMapping; }
            if (!_bindings.TryResolve(request.Origin, out VfxWorldPoint point))
            { Report(VfxDiagnosticCode.MissingOriginBinding, request.Cue, request.EventId, "Semantic origin has no current presentation binding."); return VfxSubmitResult.MissingBinding; }
            _backend.PlayOneShot(profile, point);
            _played.Add(request.EventId);
            return VfxSubmitResult.Played;
        }

        public void Reconcile(IReadOnlyList<VfxPersistentTreatmentDescriptor> currentTreatments)
        {
            var desired = new HashSet<VfxTreatmentId>();
            if (currentTreatments != null)
            {
                for (int i = 0; i < currentTreatments.Count; i++)
                {
                    VfxPersistentTreatmentDescriptor treatment = currentTreatments[i];
                    desired.Add(treatment.TreatmentId);
                    if (!_catalog.TryResolve(treatment.Cue, out VfxEffectProfile profile))
                    { Report(VfxDiagnosticCode.MissingCueMapping, treatment.Cue, default, "No local mapping exists for persistent semantic treatment."); continue; }
                    if (!_bindings.TryResolve(treatment.Origin, out VfxWorldPoint point))
                    { Report(VfxDiagnosticCode.MissingOriginBinding, treatment.Cue, default, "Persistent semantic origin has no current presentation binding."); continue; }
                    _backend.ApplyPersistent(treatment.TreatmentId, profile, point);
                    _activeTreatments.Add(treatment.TreatmentId);
                }
            }
            if (_activeTreatments.Count == 0) return;
            var remove = new List<VfxTreatmentId>();
            foreach (VfxTreatmentId id in _activeTreatments) if (!desired.Contains(id)) remove.Add(id);
            for (int i = 0; i < remove.Count; i++) { _backend.RemovePersistent(remove[i]); _activeTreatments.Remove(remove[i]); }
        }

        private void Report(VfxDiagnosticCode code, VfxCueRef cue, VfxEventId eventId, string message) => _diagnostics?.Report(new VfxDiagnostic(code, cue, eventId, message));
    }

    public sealed class SemanticVfxFeedbackAdapter : IDisposable, IWorldInteractionFactSink
    {
        private readonly IVfxCueSink _sink;
        private readonly IVitalityService _vitalityEvents;
        private readonly IGameOutcomeEvents _outcomeEvents;

        public SemanticVfxFeedbackAdapter(IVfxCueSink sink, IVitalityService vitalityEvents = null, IGameOutcomeEvents outcomeEvents = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _vitalityEvents = vitalityEvents;
            _outcomeEvents = outcomeEvents;
            if (_vitalityEvents != null) _vitalityEvents.Defeated += OnDefeated;
            if (_outcomeEvents != null) _outcomeEvents.OutcomeResolved += OnOutcomeResolved;
        }

        public void Dispose()
        {
            if (_vitalityEvents != null) _vitalityEvents.Defeated -= OnDefeated;
            if (_outcomeEvents != null) _outcomeEvents.OutcomeResolved -= OnOutcomeResolved;
        }

        public VfxSubmitResult OnDamageConfirmed(DamageResult result)
        {
            if (!result.Accepted || result.AppliedAmount <= 0 || result.DefeatOccurred || !result.State.CharacterId.IsValid) return VfxSubmitResult.Invalid;
            return _sink.Submit(new VfxCueRequest(GameplayVfxCues.Hit,
                new VfxEventId("damage:" + result.State.CharacterId.Value + ":" + result.State.Revision),
                VfxSemanticOrigin.Character(result.State.CharacterId), VfxCuePhase.Confirmed));
        }

        private void OnDefeated(DefeatEvent evt)
        {
            if (!evt.CharacterId.IsValid) return;
            _sink.Submit(new VfxCueRequest(GameplayVfxCues.Defeat,
                new VfxEventId("defeat:" + evt.CharacterId.Value + ":" + evt.State.Revision),
                VfxSemanticOrigin.Character(evt.CharacterId), VfxCuePhase.Confirmed));
        }

        public void Publish(WorldInteractionFact fact)
        {
            if (fact.Sequence == 0 || !fact.ObjectId.IsValid) return;
            _sink.Submit(new VfxCueRequest(GameplayVfxCues.Interaction,
                new VfxEventId("interaction:" + fact.Sequence), VfxSemanticOrigin.WorldObject(fact.ObjectId), VfxCuePhase.Confirmed));
        }

        public VfxSubmitResult OnCombatResolved(CombatResolved resolved)
        {
            if (!resolved.SessionId.IsValid) return VfxSubmitResult.Invalid;
            return _sink.Submit(new VfxCueRequest(GameplayVfxCues.CombatResolved,
                new VfxEventId("combat-resolved:" + resolved.SessionId.Value), VfxSemanticOrigin.None(), VfxCuePhase.Confirmed));
        }

        private void OnOutcomeResolved(GameOutcomeResolved resolved)
        {
            if (!resolved.ResolutionId.IsValid) return;
            _sink.Submit(new VfxCueRequest(GameplayVfxCues.OutcomeResolved,
                new VfxEventId("outcome:" + resolved.ResolutionId.Value), VfxSemanticOrigin.None(), VfxCuePhase.Confirmed));
        }

        public VfxSubmitResult OnAlterationCommitted(in AlterationEvent alteration)
        {
            if (!alteration.ValidateWireFormat()) return VfxSubmitResult.Invalid;
            return _sink.Submit(new VfxCueRequest(GameplayVfxCues.VoxelDestruction,
                new VfxEventId("alteration:" + alteration.tick + ":" + alteration.playerId + ":" + alteration.sequence),
                VfxSemanticOrigin.WorldPoint(alteration.origin.x, alteration.origin.y, alteration.origin.z), VfxCuePhase.Confirmed));
        }
    }

    public static class VfxPersistentStateRebuilder
    {
        public static void RebuildFromVitality(IVitalityQuery vitality, IVfxTreatmentSink sink)
        {
            if (vitality == null) throw new ArgumentNullException(nameof(vitality));
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            IReadOnlyList<VitalitySnapshot> snapshots = vitality.GetAll();
            var current = new List<VfxPersistentTreatmentDescriptor>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                VitalitySnapshot state = snapshots[i];
                if (!state.IsDefeated) continue;
                current.Add(new VfxPersistentTreatmentDescriptor(
                    new VfxTreatmentId("defeated:" + state.CharacterId.Value), GameplayVfxCues.DefeatedTreatment,
                    VfxSemanticOrigin.Character(state.CharacterId)));
            }
            sink.Reconcile(current);
        }
    }
}
