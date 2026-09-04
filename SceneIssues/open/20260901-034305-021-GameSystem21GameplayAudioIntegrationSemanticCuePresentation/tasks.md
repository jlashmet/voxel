# 21 Gameplay audio integration & semantic cue presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Audio.Api` / `Game.Audio.Runtime`
**Execution rule:** gameplay publishes semantic meaning/state; Audio maps it to local assets/playback. Audio absence/failure cannot alter gameplay authority.

## API / cue model
- [x] **T21-001 — Inventory current audio ownership.** Baseline has no Audio module/indexed `AudioSource`; Kentridge authored `door.open` was swallowed by its no-op `SlicePresentation.PlaySound`; system 23 preferences are not yet implemented.
- [x] **T21-002 — Establish asmdefs.** `Game.Audio.Api` is engine-neutral; Unity assets/sources live in `Game.Audio.Runtime`.
- [x] **T21-003 — Define semantic `AudioCueRef`.** Stable presentation cue identity is independent of clip/resource paths.
- [x] **T21-004 — Define cue event/request origin.** Stable `AudioEventId` plus CharacterId/world-object/world-point/global semantic origins.
- [x] **T21-005 — Define sustained audio-state descriptor.** `SustainedAudioState` + semantic key reconstruct current loop state without replay history.
- [x] **T21-006 — Define mapping/configuration failure behavior.** Unknown cue/origin/backend failures are diagnostic presentation results only.

## Runtime / integration
- [x] **T21-010 — Implement cue-to-asset mapping service.** Runtime-local catalog maps semantic cues to Unity clip/bus/spatial/loop configuration.
- [x] **T21-011 — Subscribe to confirmed gameplay semantic events.** Presentation-side `VitalityDefeatAudioAdapter` consumes confirmed `IVitalityService.Defeated`; Kentridge production composition consumes the public confirmed character-defeat event raised when combat settles.
- [x] **T21-012 — Integrate cutscene cue playback.** `KentridgePlayableSlice.SlicePresentation` delegates authored sound cues to the same `KentridgeGameplayAudioIntegration`/`AudioPresentationRuntime` that owns gameplay audio; `door.open` maps to semantic `world.door.opened`.
- [x] **T21-013 — Resolve semantic origins to presentation transforms.** Runtime resolver handles global/world-point origins and degrades unavailable presentation bindings without affecting authority.
- [x] **T21-014 — Implement one-shot dedupe.** Stable event ids suppress predicted+authoritative duplicate playback.
- [x] **T21-015 — Reconstruct sustained audio from current state.** Idempotent reconciliation starts/stops only current sustained descriptors.
- [ ] **T21-016 — Bind user volume/preferences.** BLOCKED: system 23 `IUserPreferencesStore`/audio preference seam is still absent on current master; do not invent settings authority in Audio.
- [x] **T21-017 — Remove scene-local substitute playback where production semantic cue exists.** Kentridge's previous no-op cutscene sound handoff now delegates to its single Audio presentation owner; no duplicate indexed legacy AudioSources were found.

## Verification
- [ ] **T21-020 — Cue mapping tests.** Known/unknown cue behavior, configuration validation and semantic origin mapping.
- [ ] **T21-021 — Dedupe/prediction test.** Local anticipation + authoritative event results in one audible semantic effect.
- [ ] **T21-022 — Reconnect test.** Sustained state reconstructs; historical damage/interaction/cutscene one-shots do not replay.
- [ ] **T21-023 — Cutscene/gameplay shared-service test.** Both paths map/play through one Audio runtime without duplicate ownership.
- [ ] **T21-024 — Headless regression.** Gameplay/session tests pass with Audio absent/uninitialized.
- [ ] **T21-025 — Module-local built-player audible validation through shared harness.** Assert semantic cue diagnostics/milestones rather than fragile timing-only sleeps.

## Cleanup / close
- [x] **T21-030 — Search gameplay APIs for clip/playback identity.** Current-master audit found no pre-existing `AudioClip`/`AudioSource` gameplay API ownership; feature diff confines Unity clip/source types to `Game.Audio.Runtime`/validation.
- [x] **T21-031 — Search scene-local AudioSources for duplicate semantic playback.** Current-master audit found no indexed legacy `AudioSource`; production Kentridge now has one shared Audio owner for cutscene/gameplay semantics.
- [ ] **T21-032 — Close with isolation proof.** Disabling Audio changes presentation only; authoritative gameplay state/results are identical.
