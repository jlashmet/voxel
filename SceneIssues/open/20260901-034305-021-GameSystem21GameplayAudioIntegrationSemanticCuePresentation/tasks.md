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
- [ ] **T21-016 — Bind user volume/preferences.** BLOCKED after current-master refresh: `origin/master` is `0d70e2e78a8a03c52403a4863d89543f25580053`; System23 remains open and T23-007 (`IUserPreferencesStore`) plus T23-017 (persist/apply audio settings) are still unchecked, and repository search still finds no canonical `IUserPreferencesStore`. The latest master advance is texture-only and introduces no alternate settings authority. Do not invent settings authority in Audio.
- [x] **T21-017 — Remove scene-local substitute playback where production semantic cue exists.** Kentridge's previous no-op cutscene sound handoff now delegates to its single Audio presentation owner; no duplicate indexed legacy AudioSources were found.

## Verification
- [x] **T21-020 — Cue mapping tests.** Exact run `33882411311` passed all 6 `Game.Audio.Tests.EditMode` tests, including known/unknown configuration and semantic origin behavior.
- [x] **T21-021 — Dedupe/prediction test.** Exact requested regression passed 1/1; Audio player logged `predicted=Played confirmed=DuplicateSuppressed playedEvents=1`.
- [x] **T21-022 — Reconnect test.** Exact Audio player logged one current ambience loop after repeated reconciliation and `historicalOneShotsReplayed=0`, then cleanly stopped the loop.
- [x] **T21-023 — Cutscene/gameplay shared-service test.** EditMode suite passed and module/production players exercised the shared runtime; production Kentridge logged `door.open semantic=world.door.opened status=Played`.
- [x] **T21-024 — Headless regression.** Exact `Game.Audio.Tests.EditMode` passed 6/6 including authoritative Vitality behavior with Audio absent; Kentridge composition tests passed 3/3 and playable-slice EditMode passed 1/1.
- [x] **T21-025 — Module-local built-player audible validation through shared harness.** Exact Audio player ran 10s and emitted every required semantic milestone with four durable captures; Kentridge module player and canonical Kentridge integration also passed. Master-synced compatibility request `cb3c694eb3f24219d423aae64513b2dc01e9a77e`, run `33888505678`, passed on attempt 2 after an attempt-1 native Unity crash was retried as infrastructure; attempt-2 artifact `9944039071`, digest `sha256:4e95af4f83769fe8bdb9cdb921c2ca1094a36d719c54133055024849575ba7bf`.

## Cleanup / close
- [x] **T21-030 — Search gameplay APIs for clip/playback identity.** Current-master audit found no pre-existing `AudioClip`/`AudioSource` gameplay API ownership; feature diff confines Unity clip/source types to `Game.Audio.Runtime`/validation.
- [x] **T21-031 — Search scene-local AudioSources for duplicate semantic playback.** Current-master audit found no indexed legacy `AudioSource`; production Kentridge now has one shared Audio owner for cutscene/gameplay semantics.
- [ ] **T21-032 — Close with isolation proof.** Production SHA `25e9d072ee2b6f923a17260dcc9a9a81361d25df` is exact-SHA green in run `33888505678`. Current master has since advanced with isolated GameSystem22 VFX and texture-only work, but closure remains blocked until T21-016 can bind the real System23 preference seam; then merge then-current master and exact-SHA revalidate that integrated production head before closure.
