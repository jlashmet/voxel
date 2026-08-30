# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, architecture compatibility, ecology policy, hidden-space topology, or presentation ownership. Close only after production consumers, focused regressions, built runtime evidence, cost/blast-radius checks, and exact-SHA CI are all green.

## Current state — 2026-08-30

`fixes/agent-7` merged current `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` via two-parent merge `46b4e473ab6497d836053a072f3fe7050156756b`. Current workflow keeps unfinished work in `open/`, so the prior obsolete `pending/` state was reconciled back to `open/` without changing acceptance.

The merge preserves both sides of the only production-code overlap: agent-7 reservation validation/injected `SettlementPlan` + snapshot support and master's per-program foundation sinking. Master’s new `KentridgePlotSurfaceSceneIssueRegressionTests` is included.

Implemented acceptance seams remain:
- engine-free deterministic 3D reservations, bounded snapshots, diagnostics and precedence;
- Kentridge settlement, macro-road/handoff, production structure, vegetation and hidden-space consumers;
- composition-configured road-clearance yielding plus an independent non-Kentridge reuse fixture;
- presentation-only gallery inspection/overlay with deliberate rejected candidate evidence.

## Validation hypotheses / discriminator

1. **Likely:** the reconciled implementation compiles and focused reservation/Kentridge/vegetation/hidden-space regressions remain green, including the incoming foundation-surface regression.
2. **Alternative:** master’s foundation-depth change exposes an integration defect in the injected production structure path or scene evidence.

Discriminator: run the smallest affected exact-SHA regression/static set, then the required built `WorldbuildingGalleryShowcase` and `KentridgePlayableSlice` validations. Any product failure is fixed before another CI request; repeated identical acceptance failure twice requires a minimal repro/root-cause isolation.

## Remaining gates

1. Check current scene highlight/classifier and ProjectValidator requirements.
2. Run focused reservation/Kentridge/vegetation/hidden-space plus incoming foundation regression on the reconciled exact SHA.
3. Run built-player gallery and Kentridge production-scene validation; visually inspect reservation overlay and physical content.
4. Record `SPATIAL_RESERVATION_COST`, allocation/memory evidence, device/generation/streaming budget impact and assignment-only blast radius.
5. Verify `ci-test/fixes/agent-7` is idle, then use it once for the final exact-SHA targeted request.
6. After every acceptance gate is green, complete metadata, move `open/` directly to `closed/`, merge any newer master, revalidate affected work, and non-force promote the exact feature head.

## Cost / ownership guardrails

No global registry, Unity Physics authority, per-claim authoritative GameObjects/colliders, duplicate road solver, or duplicated ecology/hidden-space policy. Keep one shared source snapshot with bounded views and preserve existing deterministic/device budgets.