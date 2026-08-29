# Tasks — Kentridge Awon + Medrare Opening Cutscenes

- [x] Read `AGENTS.md` and target issue; confirm no capture frames/annotations exist.
- [x] Inspect current `fixes/agent-1` / `origin/master` divergence; confirm incoming master work is unrelated and defer the required final master merge until after green exact-SHA CI.
- [ ] Reconcile all legacy provenance against pinned commit `9491acd9efc3ad7413a13fd28f1686ed473b5672`; prior resumed-branch notes contain incorrect dialogue counts and at least one nonexistent choreography path.
- [x] Confirm Awon's referenced text payload is missing and repository policy requires `Dialogue coming soon.` rather than invented dialogue.
- [ ] Re-prove the exact source order/state chain from pinned map/source files and document only source-backed opening events; keep later Medrare join out of scope unless the pinned state chain proves otherwise.
- [ ] Recover and compare exact source text for all opening dialogue payloads. Known correction: `kentridge-see-medrare.txt` contains 4 entries (not 2), and `medrare-to-church.txt` contains 3 entries (not 1); verify first-spell count/text byte-for-byte before implementation.
- [ ] Locate the actual pinned choreography source(s) for the first-spell sequence; do not rely on the stale/nonexistent `Code/MedrareFirstSpell.m` path. Map each proven cue to current reusable cutscene APIs.
- [ ] Correct Kentridge opening content to include all source-backed see-Medrare, first-spell, and to-church dialogue/choreography; preserve Awon placeholder/state semantics.
- [ ] Correct campaign progression rules to gate each source event on the immediately preceding completion flag and keep every event one-shot/re-entry safe.
- [ ] Wire the playable slice through production story/site events without per-frame discovery or hard-coded legacy coordinates; add a generic shared primitive only if a proven source cue cannot be represented.
- [ ] Update behavioral regressions for exact dialogue identity/order, sequence/gating, key choreography, one-shot/re-entry, and persisted completed-state boundaries.
- [ ] Review blast radius and steady-state/runtime cost; verify unrelated campaign/story consumers remain valid.
- [ ] Re-read every acceptance criterion and validate the implementation against the corrected pinned source evidence.
- [ ] Move only this assignment `open -> pending` on `fixes/agent-1`, set required pending metadata, and capture the exact feature SHA for CI.
- [ ] Run one final targeted CI request from that exact feature SHA on `ci-test/fixes/agent-1`, including focused PlayMode regression and built-player Kentridge replay/startup gate.
- [ ] Inspect exact-SHA CI logs/artifacts and confirm every requested test plus built application scene validation is green.
- [ ] Complete pending metadata after green CI; move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`, and finish every remaining checkbox.
- [ ] Merge latest `origin/master` into `fixes/agent-1`, resolve only in-scope conflicts, then non-force push that exact feature head to `origin/master`; fetch/merge/retry if master advances.
