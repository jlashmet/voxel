# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Authoritative evidence
- No captures/annotations exist; the acceptance contract is `issue.json` plus the original Mounting Force source.
- Pin legacy evidence to `jlashmet/mounting-force` commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` (`agent/original-world-content-inventory`). Tree SHA `456d9596...` is not a commit and is not provenance by itself.
- The retained `References/MountingForce/contracts/kentridge-opening-cutscene-contract.yaml` remains authoritative for the existing 31-line pub opening.
- `Art/kentridge-awon-house.tmx` references missing `kentridge-awon-house-back-room.txt`; repository integration-gap policy therefore requires the explicit `Dialogue coming soon.` placeholder rather than invented Awon dialogue.
- The resumed branch's source-trace notes are not trustworthy enough to implement from: direct reads at the pinned commit show `Art/kentridge-see-medrare.txt` has 4 entries (not 2) and `Art/medrare-to-church.txt` has 3 entries (not 1), while the cited `Code/MedrareFirstSpell.m` path does not exist at that commit.
- Before any source change, re-locate the actual pinned map/choreography files and re-derive the opening state chain, exact dialogue payload/order, movement/attack/timing/camera/fade cues, and boundary to later Medrare content. Exact first-spell text/count must also be rechecked rather than inherited from stale notes.

## Discriminators / selected fix
1. **General cutscene machinery missing** — currently unlikely. Existing shared cutscene steps cover waits, movement, dialogue, camera/control cues, and transitions; add shared behavior only if a proven pinned source cue cannot be represented.
2. **Existing feature progression/content is source-faithful** — rejected. The resumed implementation and tests were authored against incorrect source counts/provenance and must be audited before promotion.
3. **Awon dialogue should be reconstructed from story summaries** — rejected. The referenced text payload is absent; preserve only source-backed state/choreography and use the repository-standard placeholder.
4. **Later Medrare content belongs here** — keep rejected unless the corrected pinned state-chain audit proves it is immediately connected to this opening feature.

## Implementation / regression
- First establish a corrected source inventory from the pinned legacy commit: map event gates/flags, exact dialogue arrays, and actual choreography source path(s).
- Update Kentridge opening content and campaign progression only after that inventory is proven. Preserve the existing pub opening and Awon placeholder semantics.
- Bind staging semantically through existing Kentridge/Game cutscene APIs; do not hard-code legacy map coordinates in runtime code.
- Update behavioral regressions to assert exact dialogue identity/order, source-backed progression/gating, key staging semantics, one-shot/re-entry, and persisted completed-state behavior.
- Inspect all changed/shared paths for blast radius and steady-state cost. No per-frame discovery, hierarchy scans, polling, or unrelated scene work.
- Before CI, re-read every acceptance criterion, finish every task, and move only this assignment `open -> pending` with required metadata.
- Submit exactly one final targeted exact-SHA CI request via `ci-test/fixes/agent-1`; require focused regression plus built-player Kentridge scene validation. Do not edit `.github/test-request.json` on the feature branch.

## Blast radius / cost
Expected changes stay within Kentridge opening content/composition, focused campaign progression, focused tests, and this assignment metadata. Any generic cutscene primitive change requires a pinned source cue that existing APIs cannot express and a regression covering other consumers. Runtime acceptance requires no new steady-state allocations/search loops beyond the existing cutscene/event execution path.
