# 21 Gameplay audio integration & semantic cue presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Audio.Api` / `Game.Audio.Runtime`
**Execution rule:** gameplay publishes semantic meaning/state; Audio maps it to local assets/playback. Audio absence/failure cannot alter gameplay authority.

## API / cue model

- [ ] **T21-001 — Inventory current audio ownership.** Find cutscene AudioSources, scene-local playback, gameplay sound calls, clip ids in gameplay code, looping state and volume settings.
- [ ] **T21-002 — Establish asmdefs.** Audio.Api contains no `AudioClip`, `AudioSource`, GameObject or gameplay Runtime dependency; Audio.Runtime owns Unity playback/assets.
- [ ] **T21-003 — Define semantic `AudioCueRef`.** Stable cue identity meaningful to presentation mapping, not a clip/resource path.
- [ ] **T21-004 — Define cue event/request origin.** CharacterId, WorldObjectId or semantic world point/context only when needed; preserve stable one-shot event identity for dedupe.
- [ ] **T21-005 — Define sustained audio-state descriptor.** Current semantic state that can be reconstructed after reconnect/restore without replaying history.
- [ ] **T21-006 — Define mapping/configuration failure behavior.** Unknown cue is diagnostic/presentation failure only and never blocks domain state changes.

## Runtime / integration

- [ ] **T21-010 — Implement cue-to-asset mapping service.** Configuration resolves semantic cue refs to Unity playback setup locally.
- [ ] **T21-011 — Subscribe to confirmed gameplay semantic events.** Damage/interaction/encounter/etc. adapters live presentation-side; gameplay modules do not call Audio Runtime.
- [ ] **T21-012 — Integrate cutscene cue playback.** Existing cutscene sound cues use the same playback service and do not double-play through legacy AudioSources.
- [ ] **T21-013 — Resolve semantic origins to presentation transforms.** Use client presentation binding; missing transform degrades gracefully without changing authority.
- [ ] **T21-014 — Implement one-shot dedupe.** Authoritative confirmation and optional local predicted anticipation cannot play the same cue twice.
- [ ] **T21-015 — Reconstruct sustained audio from current state.** Reconnect/restore starts/stops loops based on current semantic descriptors and never replays old one-shots.
- [ ] **T21-016 — Bind user volume/preferences.** Consume system 23 preference seam without making Application responsible for playback.
- [ ] **T21-017 — Remove scene-local substitute playback where production semantic cue exists.** Preserve purely decorative ambient ownership where it is genuinely scene presentation.

## Verification

- [ ] **T21-020 — Cue mapping tests.** Known/unknown cue behavior, configuration validation and semantic origin mapping.
- [ ] **T21-021 — Dedupe/prediction test.** Local anticipation + authoritative event results in one audible semantic effect.
- [ ] **T21-022 — Reconnect test.** Sustained state reconstructs; historical damage/interaction/cutscene one-shots do not replay.
- [ ] **T21-023 — Cutscene/gameplay shared-service test.** Both paths map/play through one Audio runtime without duplicate ownership.
- [ ] **T21-024 — Headless regression.** Gameplay/session tests pass with Audio absent/uninitialized.
- [ ] **T21-025 — Module-local built-player audible validation through shared harness.** Assert semantic cue diagnostics/milestones rather than fragile timing-only sleeps.

## Cleanup / close

- [ ] **T21-030 — Search gameplay APIs for clip/playback identity.** Remove `AudioClip`/resource names/play commands from domain contracts.
- [ ] **T21-031 — Search scene-local AudioSources for duplicate semantic playback.** Consolidate where they represent the same gameplay/cutscene cue.
- [ ] **T21-032 — Close with isolation proof.** Disabling Audio changes presentation only; authoritative gameplay state/results are identical.
