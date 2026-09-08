# System26 exact-SHA evidence — r26

- Exact request: `601401b3ae3c48b8b2dfb6c773c03d3863efe9b2`
- Exact source: `ef7e514ce612bb75b2b1e9d8ffeb4f307f3b9530`
- Workflow run: `34195214333`
- Job: `101961328720` (`single`)
- Result: `success`
- Artifact: `10044648349` (`single-test-34195214333`)
- Artifact digest: `sha256:fdb1d1ca45b7689e3ad04f82968b9363bc597bef9a3907c2d1e5df96c16c8a55`

## Exact-source proof

The CI request resolved `SOURCE_SHA=ef7e514ce612bb75b2b1e9d8ffeb4f307f3b9530`, and the automatic repository-derived module-validation step completed successfully. The selected plan included the owned campaign/Kentridge/Story/WorldBuilder EditMode assemblies, the dedicated `KentridgeFullRunCampaignValidation` player, the WorldBuilder `TopDownPhysicalWorld` player, and the canonical shipped `KentridgePlayableSlice` integration player.

## Campaign/full-run player

The dedicated full-run player reached the shipped production route and emitted:

- `KENTRIDGE_FULL_RUN_MILESTONE physical-world-ready settlements=3 npcs=11`
- opening, Rorik, Moordell, Rossdam and Logan semantic milestones
- `KENTRIDGE_FULL_RUN_OUTCOME disposition=Success outcome=main-campaign-complete revision=1`
- `KENTRIDGE_FULL_RUN_APPLICATION_OUTCOME PASS`
- `KENTRIDGE_FULL_RUN_VALIDATION PASS`

This preserves the previously proven exactly-once System15/frontend behavior on the final r26 source.

## WorldBuilder T26-059 player

The owned production-path validation emitted:

- `WORLDBUILDER_MACRO_PHYSICAL_CATALOGUE PASS definitions=46 placements=850 roads=20 moordell=4 rossdam=4 water=1 relaxations=0`
- `WORLDBUILDER_MACRO_PHYSICAL_READY settlements=6 routes=20 buildings=16 targets=3 streamRadius=102.4 renderRadius=51.2`
- `WORLDBUILDER_MACRO_PHYSICAL_RENDER target=moordell PASS coverage=True`
- `WORLDBUILDER_MACRO_PHYSICAL_RENDER target=rossdam PASS coverage=True`
- `WORLDBUILDER_MACRO_PHYSICAL_RENDER target=water PASS coverage=True`
- `WORLDBUILDER_MACRO_PHYSICAL_VALIDATION PASS`

This proves the residency-safety-margin correction removes the prior false third-region coverage blocker without replacing the semantic generated-region + published-near-surface-coverage gate with a correctness sleep.

## Shipped integration

The normal `Assets/Scenes/KentridgePlayableSlice.unity` standalone player emitted:

- `KENTRIDGE_AUTHORED_FULL_RUN_READY settlements=3 npcs=11`

and completed its canonical player target successfully.

## Rendered-evidence classification

The screenshots were inspected directly. The normal Kentridge presentation remains `prototype/blockout quality`; the focused WorldBuilder validation is a utilitarian macro-world validation view. These captures are not claimed as production-quality art. System26's binding acceptance is authored campaign/progression, System15/frontend outcome, persistence/multiplayer semantics, and production-path built-player behavior—not an art-finish acceptance criterion—so the screenshots are used only as runtime/render-path evidence here. A future visual-finish issue must not cite this record as AAA-art acceptance.

## Remaining blocker

T26-043 remains externally blocked by System25. At current System25 source `6401622dab168f474aa2abc19eb8828e36cbaa07`, its authoritative checklist still leaves the real authority/client topology, baseline convergence, shared progression, reconnect/rehost and final separate-process evidence incomplete. System26 must not substitute an alternate multiplayer harness.
