# Tasks — Kentridge Awon + Medrare Opening Cutscenes

- [x] Read `AGENTS.md` and target issue; confirm no capture frames/annotations exist.
- [x] Inspect current `fixes/agent-1` / `origin/master` divergence; confirm incoming master work is unrelated and defer the required final master merge until after green exact-SHA CI.
- [x] Trace authoritative legacy sequence and pin source evidence to commit `9491acd9efc3ad7413a13fd28f1686ed473b5672`; correct prior tree-SHA/nonexistent-path provenance.
- [x] Confirm Awon's referenced text payload is missing and repository policy requires `Dialogue coming soon.` rather than invented dialogue.
- [x] Prove source order/state chain: `pub -> Awon -> kentridge-see-medrare -> MedrareFirstSpell -> medrare-to-church`; prove later `kentridge-medrare-join` is out of scope.
- [x] Recover exact source text for `kentridge-see-medrare` (2 lines), `medrare-first-spell` (23 lines), and `medrare-to-church` (1 line).
- [x] Recover bespoke `MedrareFirstSpell.m` choreography (zoom, delays, approach, attack/hit sound, fade sequence) and map each meaningful cue to current reusable cutscene APIs.
- [ ] Correct Kentridge opening content to include `kentridge-see-medrare`, exact first-spell dialogue/choreography, and `medrare-to-church`; preserve Awon placeholder/state semantics.
- [ ] Correct campaign progression rules to gate each source event on the immediately preceding completion flag and keep every event one-shot/re-entry safe.
- [ ] Wire the playable slice through production story/site events without per-frame discovery or hard-coded legacy coordinates; add a generic shared primitive only if a proven source cue cannot be represented.
- [ ] Update behavioral regressions for exact dialogue identity/order, sequence/gating, key choreography, one-shot/re-entry, and persisted completed-state boundaries.
- [ ] Review blast radius and steady-state/runtime cost; verify unrelated campaign/story consumers remain valid.
- [ ] Re-read every acceptance criterion and validate the implementation against the pinned source evidence.
- [ ] Move only this assignment `open -> pending` on `fixes/agent-1`, set required pending metadata, and capture the exact feature SHA for CI.
- [ ] Run one final targeted CI request from that exact feature SHA on `ci-test/fixes/agent-1`, including focused PlayMode regression and built-player Kentridge replay/startup gate.
- [ ] Inspect exact-SHA CI logs/artifacts and confirm every requested test plus built application scene validation is green.
- [ ] Complete pending metadata after green CI; move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`, and finish every remaining checkbox.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, resolve only in-scope conflicts, then non-force push that exact feature head to `origin/master`; fetch/merge/retry if master advances.
