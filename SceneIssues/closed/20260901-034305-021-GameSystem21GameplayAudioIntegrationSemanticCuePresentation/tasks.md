# 21 Gameplay audio integration & semantic cue presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Audio.Api` / `Game.Audio.Runtime`
**Execution rule:** gameplay publishes semantic meaning/state; Audio maps it to local assets/playback. Audio absence/failure cannot alter gameplay authority.

## API / cue model
- [x] **T21-001 — Inventory current audio ownership.** Baseline has no Audio module/indexed `AudioSource`; Kentridge authored `door.open` was swallowed by its no-op `SlicePresentation.PlaySound`; system 23 preferences were not yet implemented at baseline.
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
- [x] **T21-016 — Bind user volume/preferences.** System23's canonical `UserPreferences`/`IAudioPreferencesSink` seam is bound by `AudioUserPreferencesSink`, mapping Application-owned `MasterVolume` into Audio mix while preserving Audio-owned bus gains. The initial exact run exposed missing direct `Game.Input.Api` references in Audio test/validation consumers; those assembly dependencies were fixed, and final exact run `33925488384` passed the focused preference regression plus repository-derived module and built-player validation.
- [x] **T21-017 — Remove scene-local substitute playback where production semantic cue exists.** Kentridge's previous no-op cutscene sound handoff now delegates to its single Audio presentation owner; no duplicate indexed legacy AudioSources were found.

## Verification
- [x] **T21-020 — Cue mapping tests.** Exact run `33882411311` passed all 6 original `Game.Audio.Tests.EditMode` tests, including known/unknown configuration and semantic origin behavior.
- [x] **T21-021 — Dedupe/prediction test.** Exact requested regression passed 1/1; Audio player logged `predicted=Played confirmed=DuplicateSuppressed playedEvents=1`.
- [x] **T21-022 — Reconnect test.** Exact Audio player logged one current ambience loop after repeated reconciliation and `historicalOneShotsReplayed=0`, then cleanly stopped the loop.
- [x] **T21-023 — Cutscene/gameplay shared-service test.** EditMode suite passed and module/production players exercised the shared runtime; production Kentridge logged `door.open semantic=world.door.opened status=Played`.
- [x] **T21-024 — Headless regression.** Exact `Game.Audio.Tests.EditMode` passed 6/6 including authoritative Vitality behavior with Audio absent; Kentridge composition tests passed 3/3 and playable-slice EditMode passed 1/1.
- [x] **T21-025 — Module-local built-player audible validation through shared harness.** Audio module validation and canonical Kentridge integration passed previously; final exact run `33925488384` revalidated the preference-bound integrated head through repository-derived module validation and standalone SceneIssue player replay. Artifact `single-test-33925488384` id `9956772666`, digest `sha256:2c21c59d244ca504462972891689b73eebf88f17edd0bd94541309e133eaefd3`.

## Cleanup / close
- [x] **T21-030 — Search gameplay APIs for clip/playback identity.** Audit found no pre-existing `AudioClip`/`AudioSource` gameplay API ownership; feature diff confines Unity clip/source types to `Game.Audio.Runtime`/validation.
- [x] **T21-031 — Search scene-local AudioSources for duplicate semantic playback.** Audit found no indexed legacy `AudioSource`; production Kentridge now has one shared Audio owner for cutscene/gameplay semantics.
- [x] **T21-032 — Close with isolation proof.** Master `2749a5133319eb5cf5019d821bb00ee3e2fe1a4e` was integrated at `530120fe4c011061833096e79815aa20024970ff`, preserving System17 HUD/input/progression and GameSystem21 audio forwarding. Production `4aed13b073847de63657ffb0c46965f845884b64` passed exact request `41b752c37b43efb8a76219160720632e2a708e76`, run `33925488384`, including module validation, standalone SceneIssue replay, screenshots, artifact publication, and final success. Final master refresh remained unchanged.
