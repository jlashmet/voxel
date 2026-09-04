# 21 Gameplay audio integration & semantic cue presentation — implementation plan

**Target module:** `Assets/Game/Audio/Api` / `Runtime` (`Game.Audio.Api`, `Game.Audio.Runtime`).

## Acceptance / observed baseline

Audio is client-local presentation only: gameplay/cutscenes publish semantic identity/current state, Audio maps that to Unity playback, and missing/failed audio never changes authority. Baseline had no `Assets/Game/Audio` owner or indexed production `AudioSource`; Kentridge authored `door.open` but its presentation handoff was a no-op. User audio preferences belong to System23; Audio must not create competing settings authority.

## Architecture / approach

1. **Selected:** engine-neutral `AudioCueRef`, stable one-shot event identity, semantic origins and sustained-state descriptors in `Game.Audio.Api`; Unity mapping, playback, mix, dedupe, origin resolution and sustained reconciliation in `Game.Audio.Runtime`. Kentridge composition owns one `KentridgeGameplayAudioIntegration` shared by cutscene and confirmed gameplay cues.
2. **Rejected:** gameplay/scene code choosing clips or calling `AudioSource`, Audio persisting user settings, or separate cutscene/gameplay Audio runtimes. Those leak presentation policy into authority or duplicate playback ownership.

EditMode regressions cover mapping failures, dedupe, confirmed defeat events, sustained reconstruction, shared service use and headless gameplay. `Assets/Game/Audio/Validation/AudioValidation.unity` is the module-owned built-player surface. Production Kentridge routes authored `door.open` through `SlicePresentation -> ICutsceneSoundCueRuntime -> KentridgeGameplayAudioIntegration -> AudioPresentationRuntime`, mapping to `world.door.opened`.

## Ownership / blast radius

`Game.Audio.Api` has no Unity/gameplay Runtime dependency. `Game.Audio.Runtime` owns playback only. Gameplay authority remains in existing systems; Audio observes public semantic events. Sustained current state is reconstructible; one-shot history is not replayed. Unknown cue/origin/backend failures are presentation diagnostics only. Default generated clips are self-contained integration assets; semantic cue identity is independent of asset paths.

## Current state / remaining gates

Feature implementation previously passed exact run `33882411311`. Current master `d08612dfe2f4a99aff34897717569744565bc642` was merged into the feature at production SHA `25e9d072ee2b6f923a17260dcc9a9a81361d25df` with no Audio/Kentridge conflicts.

Compatibility exact request `cb3c694eb3f24219d423aae64513b2dc01e9a77e`, run `33888505678`, now passed on attempt 2. Attempt 1 suffered a native Unity `SIGSEGV`/exit 139 before persistent module results while standalone replay passed; this was treated as infrastructure and the same job was retried without replacing the request. Attempt 2 passed repository-derived module validation, standalone SceneIssue replay, artifact upload and final commit status.

**Remaining blocker:** System23 is still open on that same master. T23-007 (`IUserPreferencesStore`) and T23-017 (persist/apply audio settings) remain unchecked, so T21-016 cannot legally bind real user preferences yet. Keep this SceneIssue open. When System23 lands, merge then-current master, bind its real preference seam to `AudioPresentationRuntime.ApplyMix`, exact-SHA validate the integrated production head, complete T21-016/T21-032, then close and promote through PR + auto-merge.
