# 03 Gameplay character runtime — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Characters.Api` / `Game.Characters.Runtime`
**Execution rule:** Characters owns persistent gameplay character identity/lifecycle. Player, NPC, recruit, and enemy are compositions over the same runtime.

## API and module boundary

- [x] **T03-001 — Inventory existing actor representations.** Find player/NPC/enemy/recruit actor records, ids, transforms, lifecycle flags, scene registries, and campaign/world bindings; identify duplicates that represent the same character.
- [x] **T03-002 — Establish asmdefs.** Create/update `Characters.Api` and `Characters.Runtime`; Runtime may use world/collision APIs, while API remains engine-neutral and Runtime-internal types never cross module boundaries.
- [x] **T03-003 — Define `CharacterId`.** Choose one stable semantic identity format and migration/conversion path for existing actor ids; define equality/serialization behavior.
- [x] **T03-004 — Define character definition metadata.** Expose only demonstrated semantic role/traits needed by consumers; do not encode separate player/NPC/enemy class hierarchies.
- [x] **T03-005 — Define authoritative character snapshot.** Include lifecycle and semantic kinematic/transform state required by gameplay/replication; exclude GameObject/Transform references.
- [x] **T03-006 — Define registry/query interface and lifecycle events.** Add create/bind/remove/read contracts with deterministic failure reasons.

## Runtime and migration

- [x] **T03-010 — Implement one authoritative registry.** Enforce unique `CharacterId`, deterministic lookup, creation, binding and removal.
- [x] **T03-011 — Bind campaign/world-generated identities.** Map realized NPC/player/enemy semantic identities to stable CharacterIds without scene-object authority.
- [x] **T03-012 — Migrate player actor ownership.** Move scene/bootstrap player records behind the character registry while retaining session/player identity ownership in system 07.
- [x] **T03-013 — Migrate NPC/recruit ownership.** Represent persistent authored characters through the same lifecycle and registry.
- [x] **T03-014 — Migrate enemy ownership.** Replace enemy-specific persistent actor state with Character composition; tactical behavior remains in AI/Combat.
- [x] **T03-015 — Integrate movement/world queries.** Route authoritative movement/collision through existing world APIs while keeping voxel implementation details out of Characters.Api.
- [x] **T03-016 — Add narrow consumer seams.** Support Vitality, AI, Encounters, Sessions, replication, persistence, inventory binding, and cutscene actor resolution through API contracts only.
- [x] **T03-017 — Distinguish defeat from removal.** A defeated character remains a character until owning gameplay policy removes it.

## Verification

- [x] **T03-020 — Shared-runtime composition test.** `SharedRegistryComposesPlayerNpcAndEnemyWithoutTypeSpecificAuthority` proves one snapshot/runtime representation for player/NPC/enemy.
- [x] **T03-021 — Identity uniqueness tests.** Duplicate/unknown/retired ids, binding uniqueness and deterministic ordering are covered by focused EditMode tests.
- [x] **T03-022 — Persistence identity test.** `PersistenceRoundTripPreservesStableIdsBindingsStateAndTombstones` preserves stable ids, bindings, kinematics and removal tombstones.
- [x] **T03-023 — Headless movement/query test.** `HeadlessMovementResolverUpdatesAuthoritativeSnapshotWithoutGameObject` exercises semantic movement without presentation objects.
- [x] **T03-024 — Independent non-Kentridge fixture.** `IndependentNonKentridgeFixtureUsesSameBindingAndLifecycleContracts` proves reuse outside Kentridge.
- [x] **T03-025 — Run module and dependent tests.** Exact-SHA request `668267892702a3b8fbea9aac3908dd94015d3171`, run `33480997516`, passed focused tests, repository-derived automatic module validation, and standalone SceneIssue replay against feature SHA `4416e89f17a4f2c3377a6905ccddc5d5faad74da`.

## Cleanup / close

- [x] **T03-030 — Remove duplicate actor registries/state.** Audit confirms Kentridge scene collections now remain transient presentation/physics adapters; persistent player/NPC/enemy identity and lifecycle are created/bound/defeated through the shared registry. Session, Combat, Campaign and ambient-life state retain only their documented distinct ownership.
- [x] **T03-031 — Cross-module boundary audit.** Kentridge consumes `Game.Characters.Api` plus the Characters-owned `Game.Characters.Composition` construction seam; it no longer references `Game.Characters.Runtime`, and presentation GameObjects are not identity authority.
- [x] **T03-032 — Close with reuse proof.** Player/NPC/enemy share `ICharacterRegistry`; the non-Kentridge fixture proves reusable API contracts, and downstream Kentridge adapters operate on `Game.Characters.Api` types rather than Runtime implementation types.
