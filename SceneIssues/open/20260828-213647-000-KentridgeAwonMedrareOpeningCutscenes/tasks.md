# Tasks — Kentridge Awon + Medrare Opening Cutscenes

- [x] Read `AGENTS.md` and target issue; confirm no capture frames/annotations exist.
- [ ] Reconcile feature branch with current `origin/master`, preserving only in-scope changes.
- [ ] Trace authoritative legacy sequence and exact dialogue for `kentridge-awon`, Medrare visit, `medrare-to-church`, and `medrare-first-spell`; record choreography/prerequisites/revisit semantics.
- [ ] Map every required legacy cue to reusable current cutscene/story/world APIs; add shared primitives only for proven gaps.
- [ ] Author the full applicable Logan → Awon → Medrare → church/first-spell progression with semantic generated-site/stage bindings.
- [ ] Wire the playable slice to production story/site events without per-frame discovery or hard-coded legacy coordinates.
- [ ] Add behavioral regressions for sequence/gating, exact dialogue identity, choreography, one-shot/re-entry, and save/load-safe state boundaries.
- [ ] Review blast radius and steady-state/runtime cost; verify unrelated campaign/story consumers remain valid.
- [ ] Update `plan.md` with discriminators, results, selected fix, source SHA, and remaining gates.
- [ ] Run one final targeted CI request from the exact feature SHA on `ci-test/fixes/agent-1`, including the built-player Kentridge replay gate.
- [ ] Inspect exact-SHA CI logs/artifacts and confirm every requested test plus built application scene validation is green.
- [ ] Complete pending metadata and move only this assignment `open` → `pending` on `fixes/agent-1`.
- [ ] After all workflow gates remain green, move `pending` → `closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, resolve only in-scope conflicts, push feature, then non-force push its exact head to `origin/master`; retry if master advances.
