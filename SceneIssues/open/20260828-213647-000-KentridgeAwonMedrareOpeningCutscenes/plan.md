# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Authoritative evidence
- No captures/annotations exist; `issue.json` plus pinned Mounting Force commit `9491acd9efc3ad7413a13fd28f1686ed473b5672` are authoritative.
- `SceneIssues/feature-readme.md` is absent on feature/master; canonical `SceneIssues/README.md` governs.
- Preserve the existing source-backed Logan pub opening.
- `Art/kentridge-awon-house.tmx` defines `kentridge-awon-house-back-room`: talk to Awon, play once, text `kentridge-awon-house-back-room.txt`. That root-level payload exists at the pinned commit with exact 22-line Awon/Weldon/Steven/Madeline/Logan dialogue.
- `Art/kentridge.tmx` defines separate `kentridge-see-medrare` and `kentridge-medrare-join` outdoor triggers, both gated directly by Awon completion. The sighting is a play-once spatial trigger; its text payload is absent. The join is play-once + require-step and `sceneJoinParty=Medrare`; its text payload is absent.
- `Code/KentridgeMedrareJoin.m` exactly marks the scene started, sets zoom `0.5`, waits `1.5s`, moves Medrare toward the player over `2s`, then invokes dialogue id `5000`.
- `Art/medrare-house-lower.tmx` defines `medrare-first-spell`: play-once + require-step after Awon, `addSpell=Flame,RPGPlayer`; and `medrare-to-church`: play-once after first-spell. Their text payloads are absent. Later `medrare-join` gated by `meet-king` is out of scope.

## Discriminators
1. Earlier Michael/William/zombie reconstruction is faithful — **rejected**.
2. Missing Medrare text should be reconstructed — **rejected**; absent payloads remain UNKNOWN.
3. The outdoor Medrare events are one beat — **rejected**; source defines separate spatial triggers with no dependency between them beyond Awon.
4. Existing runtime already covers all progression state — **rejected**. The branch added party/spell effect specs but `CampaignRuntime` does not implement them, and its new site-proximity call is API-inconsistent.

## Selected fix
- Keep exact 22-line Awon dialogue/speakers and source-backed one-shot gate.
- Preserve separate Medrare sighting and join events; use semantic rebuilt-world triggers without inventing missing dialogue or extra dependency.
- Preserve join choreography exactly enough to prove zoom `0.5`, 1.5s wait, 2s approach, dialogue id `5000`, then durable Medrare membership.
- Preserve one-time Flame grant and church-after-first-spell gate.
- Add the narrow generic campaign state needed for joined members/spells plus capture/restore of completed cutscenes/progression, preventing replay after continuation.
- Fix the `SiteEntered` / `SiteProximityEntered` mismatch. Remove shared changes that only support rejected content.

## Regression / gates
- Tests: exact Awon text/order/speakers; Logan preserved; pre-Awon rejection; distinct sighting/join; join choreography; one-time Medrare join; one-time Flame grant; church gate; replay suppression; snapshot/restore continuation.
- Review assignment-only diff and event-driven cost: no polling/update scans, new steady-state work, unapproved packages/assets, or unrelated SceneIssues.
- Run workflow gates; open→pending with metadata; one exact-SHA CI request on `ci-test/fixes/agent-1`; inspect focused regression and built Kentridge harness.
- Only after both green: pending→closed, `status=fixed`, `resolvedUtc`; merge current master into feature and non-force push exact head to master, retrying if master advances.
