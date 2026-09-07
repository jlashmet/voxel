# Experiment 009 — frustum coverage and readable physical-world evidence

## Exact source / transport
- Feature source: `7c9e0dac78331f51cd5ce310956ec799df21f00e`
- CI request commit: `7ff7c82648557e30664d4ed439f2fdd90c1e4696`
- Workflow run: `33263409994`
- Evidence artifact: `9717947090`

## Automated result
The focused production-path PlayMode acceptance and 60-second built-player workflow are green. The artifact contains all eight required macro PNGs. The built-player log reports validation `Time.timeScale` restored to 1 before CharacterMotor evidence, approximately 3.60 m of local motor motion and 4.67 m of macro-road motor motion, and `coverage=True` before every remote survey capture. No harness assertion/runtime failure prevented completion.

## Full-resolution visual inspection
Green workflow + file existence is not closure-quality proof:
- `macro-moordell.png`: only a clipped building edge is visible; the settlement is not a readable four-building physical blockout.
- `macro-rossdam.png`: no readable settlement blockout is visible.
- `macro-fairy-village.png`: no readable settlement blockout is visible.
- `macro-orc-village.png`: the settlement is not a clear four-building proof.
- `macro-rossdam-lake-detour.png`: only a small water sliver is readable and a large translucent rectangular rendering surface dominates part of the frame; this is not clean proof of the substantial carved basin/shoreline.
- `macro-macro-network-overview.png`: large unstreamed/empty terrain holes remain inside the elevated camera view.
- `macro-road-character-motor.png`: the traversed road exists, but surrounding streamed terrain is visibly incomplete.
- `macro-southern-ridge-pass.png`: the constrained road/ridge area is present, but it does not rescue the missing settlement/lake/network proof above.

## Hypothesis discrimination
- **The physical planner failed to generate the settlements/routes/geography.** Rejected by focused production tests, physical-plan counts, and portions of the images that do show roads/ridge/geometry.
- **`HasCompletePublishedNearSurfaceCoverage()` is a sufficient elevated-camera readiness gate.** Rejected. It reports `coverage=True` while significant portions of the actual camera frustum remain visually absent. It proves only the current near-surface neighborhood, not the whole evidence view.
- **Current elevated settlement framing proves generated blockouts.** Rejected. Focusing the tallest blockout is not enough when the camera is still far/high and distant chunks/buildings are outside fully published coverage or clipped by terrain/framing.
- **Change production world generation to satisfy screenshots.** Rejected. The evidence failure is in validation framing/readiness; production semantics remain frozen unless runtime evidence later contradicts the focused tests.

## Next isolated experiment
Change only validation/evidence plumbing:
1. Make each survey readiness check cover the actual evidence footprint/frustum needed by that capture, not only the resident near-surface predicate. Prefer existing renderer/streamer APIs; if none exist, require deterministic coverage at a bounded set of camera/focus/intermediate sample points before capture rather than weakening production streaming.
2. Tighten generic-settlement camera distance/height so at least the four deterministic generated blockouts are large and readable in frame, while deriving focus/extent from the production physical plan.
3. Choose a lake camera/focus pair from the resolved Rossdam basin extent/route geometry that clearly shows substantial water, shoreline, and constrained road without the translucent surface dominating the frame.
4. Keep ridge/pass evidence close enough for readable constrained-route proof.
5. Replace the too-wide macro overview with a bounded overview that shows a fully streamed connected route cluster; do not demand an impossible whole-world frustum from resident streaming.
6. Preserve real normal-time CharacterMotor motion and keep planner/catalogue/streamer/story/normal gameplay semantics unchanged.

## Success gate
Do not close on workflow green alone. The final exact-SHA artifact must be visually inspected at full resolution and show: readable physical blockouts for Moordell/Rossdam/Fairy Village/Orc Village, a continuous generated road with real motor traversal, a substantial clean Rossdam basin/shoreline plus constrained route, a readable southern ridge/pass response, and a connected route overview with no large unstreamed holes. Runtime cleanliness and cost telemetry must remain within existing budgets.