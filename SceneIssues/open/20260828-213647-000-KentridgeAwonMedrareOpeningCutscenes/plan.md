# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Authoritative evidence
- No captures/annotations exist; the acceptance contract is `issue.json` plus the original Mounting Force source.
- Pin legacy evidence to `jlashmet/mounting-force` commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` (`agent/original-world-content-inventory`). Earlier notes incorrectly treated tree SHA `456d9596...` as a commit; do not use that as provenance.
- The retained `References/MountingForce/contracts/kentridge-opening-cutscene-contract.yaml` remains authoritative for the existing 31-line pub opening.
- `Art/kentridge-awon-house.tmx` gates Awon on `pub=1`, sets `Awon=1`, and references missing `kentridge-awon-house-back-room.txt`. `References/MountingForce/INTEGRATION_GAPS.md` therefore requires the explicit `Dialogue coming soon.` placeholder rather than invented Awon dialogue.
- `Art/kentridge.tmx` then gates `kentridge-see-medrare` on `Awon=1`, sets `see-medrare=1`, and `Art/kentridge-see-medrare.txt` supplies one exact Medrare line.
- `Art/medrare-house-lower.tmx` gates `MedrareFirstSpell` on `see-medrare=1`, sets `first-spell=1`, then gates `medrare-to-church` directly on `first-spell=1` and sets `church=1`; therefore `medrare-to-church` is part of the immediately connected opening slice.
- `Art/medrare-first-spell.txt` supplies the exact 17-line Medrare dialogue. `MedrareFirstSpell.m` supplies bespoke staging: control-lock/scene start, camera zoom 0.5, 1.5 s opening delay, 18-line dialogue block boundary, Medrare approaches the player for 1 s, attacks/plays `stab.caf`, waits 1.5 s, shows two lines, then a black-layer 2 s delay + 2 s fade-in + 2 s delay + three lines + 2 s fade-out.
- `Art/medrare-to-church.txt` supplies three exact Logan lines. Later `kentridge-medrare-join` / `Code/KentridgeMedrareJoin.m` is a distinct later sequence and is explicitly out of scope for this opening feature.

## Discriminators / selected fix
1. **General cutscene machinery missing** — rejected. Existing shared cutscene steps already cover waits, movement, dialogue, camera/control cues, and transitions; add shared behavior only if a source cue cannot be represented.
2. **Existing feature progression is source-faithful** — rejected. It skips `kentridge-see-medrare`, uses non-source first-spell dialogue, and omits the directly connected `medrare-to-church` event.
3. **Awon dialogue should be reconstructed from story summaries** — rejected. The referenced text payload is absent; preserve state/choreography that is source-backed and use the repository-standard placeholder.
4. **Later Medrare join belongs here** — rejected. Its event/text is downstream and separate from `see-medrare -> first-spell -> church`.

## Implementation / regression
- Preserve the existing pub opening; author the source-backed chain `pub -> Awon -> see-medrare -> first-spell -> church` with one-shot positive-completion prerequisites.
- Replace the inherited first-spell dialogue/choreography with the exact 17-line text and source-backed `MedrareFirstSpell.m` staging. Add exact see-Medrare and church text.
- Bind staging semantically through existing Kentridge/Game cutscene APIs; no hard-coded legacy map coordinates in runtime code.
- Update campaign rules and behavioral regressions to assert exact dialogue ordering, gating, key staging semantics, one-shot/re-entry, and persisted completed-state behavior.
- Before CI, inspect changed paths for blast radius/cost and move only this assignment `open -> pending` with required metadata.
- Submit one final targeted exact-SHA CI request only via `ci-test/fixes/agent-1`; require focused PlayMode regression plus built-player Kentridge scene validation.

## Blast radius / cost
Expected changes stay within Kentridge opening content/composition, the existing generic completion-condition primitive if still required, focused tests, and this assignment metadata. No renderer work, per-frame scene search, background polling, hierarchy scan, new steady-state allocation loop, or unrelated campaign content is needed. Any shared primitive change must be justified by a source cue and covered by regression.
