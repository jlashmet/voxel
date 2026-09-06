# Switching/resource evidence discriminator

## Observed gap
Run `34000107687`, request `c72cb89cea7d8a25e10dc8e716eecb300e5702ab`, source `a67d64a8174104327a097f11183db772109d40e3`, artifact `9979710315` (SHA256 `ea8b83b779855c344269b26859f3b4ad8488b6040b702fafe4179724139f8d94`) reports `stress switches=44 owned=1 peakOwned=1 lastSwitchMs=0.4` in `SceneIssue/player-run.log`.

The previous `PropShowcase.UpdateCaptureAutomation` selects the whole stress set in one Update. Presenter dictionaries are cleared synchronously, but Unity objects/native meshes are retired separately. Hypotheses: real retirement is stable, or dictionaries hide deferred/leaked resources. The existing log cannot distinguish these; it also has no allocator measurements. This is required lifecycle/cost acceptance work, not a new performance feature.

## Instrumented production path
`SceneRuntime/Validation/PropShowcaseCaptureStress.cs` invokes the real public browser selection, without changing geometry, materials, authoritative storage or cleanup implementation. It repeats the same sampled set three times (99 selections for the current 529-entry catalogue), gives each selection two frame boundaries, and samples the same final entry after a settling interval. Voxel-backed endpoints additionally require published surface coverage. The original 66-second scenario remains bounded; incomplete cycles cannot emit the required completion marker.

Cycle zero warms the sampled paths. Two subsequent samples include actual owned transforms/renderers/colliders/lights/particle systems (including inactive objects), all loaded Unity mesh/material counts, used/reserved Unity allocator bytes, unforced-GC managed bytes, and renderer-reported resident geometry bytes. A count mismatch in owned components fails. The timing summary measures synchronous Select calls, including production realization/support/framing, not asynchronous mesh-publication latency. Initial browser setup also records startupMs.

## Interpretation and limitations
These three snapshots are not a claim of two-hour memory flatness. Unity allocator totals are process-wide, managed values include uncollected garbage, and resident geometry is not total GPU driver allocation. Do not sum overlapping domains. Global resource counts can reveal unparented native allocations but require inspection before attribution. Zero profiler totals produce `memoryAvailable=False` and are unavailable evidence, never a zero-memory success. No forced GC, altered rendering budget, relaxed tolerance, manual target registration or longer scenario hides a result.

Three PlayMode resource-accounting tests cover inactive/deferred object retirement, an unparented native Mesh despite unchanged owner counts, and rejection of a missing owner. Their bare Unity objects are nonvisual accounting fixtures, not substitute prop art or visual proof. The owned built-player scene still runs production realization/rendering.

## Validation status
Only source/diff review and exact blob-integrity checks were performed for this change; no Unity or C# compiler is available in this container. The new PlayMode tests and standalone resource probe are not yet executed. The prior 20 passing Python tests concern CI isolation only. Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28` / run `34003328146` remains queued on the older material-fix source and is unchanged. Fresh exact-source CI, usable measurements, visual review, all acceptance checkboxes and PR promotion remain outstanding.
