# 21 Gameplay audio integration & semantic cue presentation — implementation plan

**Target module:** `Assets/Game/Audio/Api` / `Runtime` (`Game.Audio.Api`, `Game.Audio.Runtime`).

## Acceptance / observed baseline

Audio is client-local presentation only: gameplay/cutscenes publish semantic identity/current state, Audio maps that to Unity playback, and missing/failed audio never changes authority. Baseline audit on master `aa61895f28d70f35c67d07db6a4fa93beee635eb` found no `Assets/Game/Audio` owner and no indexed production `AudioSource` ownership. `KentridgePlayableSlice.SlicePresentation.PlaySound` is a no-op even though `KentridgeOpeningCutscene` authors `CutsceneStep.Sound(door.open)`. System 23 remains open; its `IUserPreferencesStore`/audio-preference tasks are not implemented, so T21-016 is an external prerequisite and must not be replaced by Audio-owned settings authority.

## Architecture / hypotheses

1. **Selected:** engine-neutral `AudioCueRef`, stable one-shot event identity, semantic origin, and sustained-state descriptors in `Game.Audio.Api`; Unity clip/source mapping, mix, dedupe, origin resolution and current-state loop reconciliation live in `Game.Audio.Runtime`. Confirmed gameplay adapters subscribe to public semantic events such as `IVitalityService.Defeated`; cutscene cues adapt through the same `IAudioPresentation` service.
2. **Rejected:** gameplay modules or scenes choose clips/call `AudioSource`, or Audio owns gameplay/settings state. That leaks presentation identity into authority and makes reconnect/prediction duplication unavoidable.

**Discriminating proof:** EditMode regressions cover mapping failures, predicted+confirmed dedupe, confirmed Vitality events, reconnect/current-state reconstruction, shared cutscene/gameplay service and headless gameplay with Audio absent. `Assets/Game/Audio/Validation/AudioValidation.unity` is the module-owned built-player surface and emits semantic milestones while real Unity `AudioSource` playback runs.

## Ownership / blast radius

`Game.Audio.Api` has no Unity or gameplay Runtime dependency. `Game.Audio.Runtime` owns Unity playback only. Kentridge composition may attach presentation adapters but does not transfer gameplay authority. Current sustained audio is reconstructible from descriptors; one-shot history is deliberately not reconstructed. Unknown cues/origins/backend failures are diagnostic presentation failures.

The runtime default catalog uses locally generated Unity `AudioClip` objects only as self-contained integration assets; semantic identity remains independent of clip/resource paths and authored assets can replace bindings without gameplay changes.

## Current state / remaining gates

Core API/runtime, mapping, dedupe, confirmed Vitality adapter, Unity backend, current-state loop reconciliation, headless tests and module validation scene are staged. Remaining required work: route the actual Kentridge `door.open` cutscene cue through the shared production Audio service, consume system 23 preferences once its API lands, then exact-SHA automatic module + standalone SceneIssue validation, final audits, closure, and PR + auto-merge.
