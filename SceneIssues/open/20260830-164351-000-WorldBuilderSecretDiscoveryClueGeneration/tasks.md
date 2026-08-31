# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`.
- [x] Inspect the SceneIssue directory for captures/marked regions; none are present.
- [x] Inspect existing secret topology contracts and `SecretPlanner`.
- [x] Discriminate root-cause hypotheses: secret-location selection already exists; clue planning is the missing layer.
- [ ] Verify whether the generic interactable/inspect/discovery prerequisite has landed on current master; record blocker if not.

## Semantic clue contracts

- [ ] Add stable clue identity and clue-kind/stage contracts in `Game.WorldBuilder.Api`.
- [ ] Express semantic source/target refs, authored content, required/optional behavior, and optional secret memory topic without scene-object coupling.
- [ ] Integrate clue specs with campaign authoring/blueprint composition without duplicating secret-location planning.
- [ ] Validate duplicate ids, invalid stage ordering, unknown secret refs, and malformed required clues at the appropriate boundary.

## Deterministic planning and discovery state

- [ ] Add `SecretCluePlanner` consuming authoritative resolved secret/site plans.
- [ ] Required clue with no legal resolved source fails explicitly; optional unresolved clue may be omitted only when authored optional.
- [ ] Same world seed + same secret/source candidates produces identical ordered clue plan.
- [ ] Multiple legal alternatives can vary deterministically across allowed seeds/styles without generation-order dependence.
- [ ] Emit inspectable resolution/debug records listing secret id, clue ids, stage/source, and validation outcome.
- [ ] Add secret-scoped discovery/memory state with undiscovered initial state and serializable progression semantics; keep updates event-driven.

## Behavioral regressions

- [ ] Required hidden-cache chain resolves environmental hint -> shelter/interior clue -> final inspectable/readable entrance clue.
- [ ] NPC/rumor variant resolves to a valid clue chain.
- [ ] Missing required source produces deterministic hard failure.
- [ ] Same-seed determinism regression.
- [ ] Alternate-seed variation regression where multiple equivalent sources exist.
- [ ] Discovery/memory save-load round trip regression.
- [ ] Regression proves clue planning consumes `ResolvedSecretPlan`/authoritative site resolution rather than selecting a second hidden location.

## Built-player validation

- [ ] Add/identify module-owned focused validation scene under the owning module, not a top-level gallery/showcase scene.
- [ ] Bind production clue plan to generic inspect/read/search/use interaction path; no scene-local bespoke discovery logic.
- [ ] In a built Player, traverse: notice first clue -> follow second -> inspect/read final clue -> discover hidden entrance/cache.
- [ ] Capture and inspect full-resolution screenshots proving clue readability/legibility and discovery without a global quest marker.
- [ ] Validate NPC/rumor variant in built Player if required by final acceptance fixture.

## Cost / blast radius / gates

- [ ] Confirm planning is bounded and deterministic and does not perform heavy world search every frame.
- [ ] Record clue counts, planning cost, persistent state size, and affected module boundaries.
- [ ] Run focused exact-SHA CI via `ci-test/fixes/agent-5`; never add `.github/test-request.json` to feature branch.
- [ ] Run any repository-derived module/player gates required by exact-SHA workflow.
- [ ] All required checkboxes and acceptance criteria green.
- [ ] Move SceneIssue directly `open -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master`, rerun/retry gates if SHA changes as required, then push exact feature head to `origin/master` non-force.
