# 03 Gameplay character runtime — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Characters.Api` / `Game.Characters.Runtime`
**Execution rule:** Characters owns persistent gameplay character identity/lifecycle. Player, NPC, recruit, and enemy are compositions over the same runtime.

## API and module boundary

- [x] **T03-001 — Inventory existing actor representations.** Find player/NPC/enemy/recruit actor records, ids, transforms, lifecycle flags, scene registries, and campaign/world bindings; identify duplicates that represent the same character.
- [x] **T03-002 — Establish asmdefs.** Create/update `Characters.Api` and `Characters.Runtime`; Runtime may use world/collision APIs, while API remains engine-neutral and Runtime-internal types never cross module boundaries.
- [ ] **T03-003 — Define `CharacterId`.** Choose one stable semantic identity format and migration/conversion path for existing actor ids; define equality/serialization behavior.
- [ ] **T03-004 — Define character definition metadata.** Expose only demonstrated semantic role/traits needed by consumers; do not encode separate player/NPC/enemy class hierarchies.
- [ ] **T03-005 — Define authoritative character snapshot.** Include lifecycle and semantic kinematic/transform state required by gameplay/replication; exclude GameObject/Transform references.
- [ ] **T03-006 — Define registry/query interface and lifecycle events.** Add create/bind/remove/read contracts with deterministic failure reasons.

## Runtime and migration

- [ ] **T03-010 — Implement one authoritative registry.** Enforce unique `CharacterId`, deterministic lookup, creation, binding and removal.
- [ ] **T03-011 — Bind campaign/world-generated identities.** Map realized NPC/player/enemy semantic identities to stable CharacterIds without scene-object authority.
- [ ] **T03-012 — Migrate player actor ownership.** Move scene/bootstrap player records behind the character registry while retaining session/player identity ownership in system 07.
- [ ] **T03-013 — Migrate NPC/recruit ownership.** Represent persistent authored characters through the same lifecycle and registry.
- [ ] **T03-014 — Migrate enemy ownership.** Replace enemy-specific persistent actor state with Character composition; tactical behavior remains in AI/Combat.
- [ ] **T03-015 — Integrate movement/world queries.** Route authoritative movement/collision through existing world APIs while keeping voxel implementation details out of Characters.Api.
- [ ] **T03-016 — Add narrow consumer seams.** Support Vitality, AI, Encounters, Sessions, replication, persistence, inventory binding, and cutscene actor resolution through API contracts only.
- [ ] **T03-017 — Distinguish defeat from removal.** A defeated character remains a character until owning gameplay policy removes it.

## Verification

- [ ] **T03-020 — Shared-runtime composition test.** Create a player, NPC and enemy through the same registry and verify no type-specific authority path exists.
- [ ] **T03-021 — Identity uniqueness tests.** Cover duplicate ids, unknown ids, removal/recreate policy, and deterministic query ordering where exposed.
- [ ] **T03-022 — Persistence identity test.** Capture/restore or equivalent round-trip must preserve stable CharacterIds and bindings.
- [ ] **T03-023 — Headless movement/query test.** Exercise semantic movement/world-query integration without presentation GameObjects.
- [ ] **T03-024 — Independent non-Kentridge fixture.** Prove character creation/binding/lifecycle in a second composition.
- [ ] **T03-025 — Run module and dependent tests.** Include Vitality/AI/Encounter consumers as they exist; rely on automatic module selection.

## Cleanup / close

- [ ] **T03-030 — Remove duplicate actor registries/state.** Repository search for player/NPC/enemy ids and lifecycle stores that now bypass Characters.
- [ ] **T03-031 — Cross-module boundary audit.** No external asmdef may reference `Game.Characters.Runtime`; no presentation object is authoritative identity.
- [ ] **T03-032 — Close with reuse proof.** Confirm player/NPC/enemy compositions share one runtime and each downstream subsystem depends only on Characters.Api.
