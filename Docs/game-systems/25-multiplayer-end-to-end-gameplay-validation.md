# 25. Multiplayer end-to-end gameplay validation

## Status

Approved design direction.

## Purpose

Prove that multiple real built-player processes converge on one authoritative semantic game through the production application, party/session, Unity Transport, gameplay replication, reconnect, and teardown paths.

This layer complements rather than replaces the repository's existing deterministic protocol, loopback, reconciliation, convergence, catch-up, and transport tests.

## Defining rule

**Multiplayer end-to-end tests prove that multiple real built-player processes converge on one authoritative semantic game through the production session, transport, replication, and reconnect paths.**

## Existing foundation

The repository already has focused networking tests for:

- protocol serialization and round trips,
- real Unity Transport loopback hosts,
- authenticated authoritative accept/reject behavior,
- two-client authoritative world convergence,
- player-state prediction and reconciliation,
- event-driven replication,
- region catch-up, repair, resync, and convergence.

Those tests deliberately run in one NUnit process and often construct server/client runtimes directly. They should remain the fast deterministic layer.

System 25 owns the missing cross-process proof: separate production player processes with separate Unity runtimes, memory, input/presentation state, transport connections, replication state, and local writable data.

## Validation layers

Keep three distinct proof layers:

1. **Fast deterministic networking tests** — protocol, prediction/reconciliation, loopback, repair/catch-up, authoritative behavior.
2. **System 25 multi-process multiplayer E2E** — multiple built application processes sharing one authoritative gameplay session.
3. **System 24 production-composed built-player vertical slice** — production application/world/presentation integration in the representative Kentridge slice.

System 25 must not become a replacement for deterministic network tests, and System 24 must not become a four-client multiplayer matrix.

## Process topology

The minimum meaningful topology is one authoritative gameplay instance and two distinct client/player processes.

The authority may be a dedicated/headless process or the authoritative portion of a host-player topology according to the production hosting design. Tests must not redefine server ownership merely for convenience.

Every required participant must be an actual built application process using the production networking/runtime assemblies. No client may share C# service instances, process memory, replication state, or input/presentation state with another participant.

## Production entry path

Built clients should enter multiplayer through the production application/session seams:

1. host/create or join is requested through the frontend/application flow,
2. System 07 resolves party/session formation through the configured join-provider seam,
3. transport connection opens,
4. the connection authenticates as a durable `PartyMemberId`,
5. a stable `PlayerSlot` and controlled `CharacterId` are assigned/restored,
6. gameplay replication synchronizes current authoritative state,
7. the member reaches the explicit `GameplayReady` barrier.

Tests must not bypass this by opening a hidden test socket, directly authenticating a runtime object, or manually spawning controlled characters.

A deterministic local/direct join provider is acceptable for CI because it exercises the production System 07 abstraction without requiring public matchmaking, Internet discovery, NAT traversal, relay, or platform services.

## Reuse the shared built-player harness

Extend the repository's shared standalone-player/built-player validation infrastructure with generic multi-process coordination rather than creating a parallel multiplayer harness.

The coordinator may own:

- launching several instances of one exact-SHA build,
- assigning process roles such as authority, player A, and player B,
- providing role-specific startup configuration,
- waiting for semantic milestones,
- routing deterministic player intent through production input/application seams,
- stopping/restarting a process to test interruption,
- collecting role-tagged logs and diagnostics.

It must not know campaign-specific gameplay rules such as which enemy to defeat, which item to collect, or which objective to complete. Those remain scenario/content instructions.

## Exact-SHA requirement

Every process participating in one validation run must execute the same exact production build SHA.

The harness should record build SHA and process role in the artifact/log bundle. A required process failing to launch invalidates the run; the test must not silently degrade to fewer participants and report green.

## Isolated local state

Each client process needs independent writable state for local preferences, logs, temporary files, and other process-local data.

Two local clients must not accidentally pass because they share files that separate real players would never share. Authoritative persisted session data remains owned by the authority according to System 16.

## Semantic diagnostic observation

The central oracle is authoritative semantic state, not visual coincidence and not agreement between two potentially wrong clients.

Where practical, expose a read-only diagnostic snapshot from each process containing only semantic facts needed for validation, for example:

- `GameSessionId`,
- session lifecycle/readiness,
- authoritative/applied revision,
- `PartyMemberId -> PlayerSlot -> CharacterId` bindings,
- member presence/recovery state,
- selected gameplay facts required by the active scenario.

This surface is observation only. It must not expose privileged mutation methods such as setting health, completing objectives, giving items, teleporting, or directly changing authority.

Tests advance the scenario through real gameplay/application commands.

## Authoritative convergence

The primary assertion shape is:

`authoritative semantic state at revision N -> client A converges -> client B converges`

Client-to-client equality by itself is insufficient because both clients could share the same incorrect state.

Assertions should prefer stable semantic identities and state over Unity object identity, frame counts, GameObject names, or transport connection IDs.

## Contention / exactly-once proof

At least one core smoke scenario should include competing client intent over one authoritative resource.

A strong example is a world-loot pickup race:

1. one world item is available,
2. client A and client B both request pickup through the real interaction/inventory path,
3. the authoritative transaction resolves exactly one winner,
4. the world item is removed exactly once,
5. exactly one inventory gains exactly one item,
6. both clients converge on the authoritative result.

This proves client intent, authentication, identity, server authority, world-object interaction, inventory transaction safety, replication, and cross-client convergence without creating test-only authority.

## Combat/vitality convergence

Include a compact authoritative combat/vitality proof:

1. a real client action causes an authoritative combat consequence,
2. the server resolves damage/defeat through the production gameplay systems,
3. both clients converge on the same Character/Vitality state.

System 25 does not require pixel-identical HUD or VFX output; Systems 17 and 22 own presentation semantics. The E2E assertion is that the actual client applications receive the same authoritative gameplay truth.

## Reconnect is mandatory

Reconnect is a core cross-process acceptance case because it exercises transport, durable session identity, gameplay continuity, resynchronization, and presentation readiness together.

A representative test should:

1. establish player B as a known `PartyMemberId`, `PlayerSlot`, and `CharacterId`,
2. interrupt or terminate player B without issuing semantic `LeaveGame`,
3. verify the authority reports interruption/recovery semantics rather than immediate departure,
4. preserve the same member, slot, and character while B is absent,
5. change authoritative gameplay state affecting B or shared world/progression while disconnected,
6. restart/reconnect B on a new transport connection,
7. restore the same `PartyMemberId`, `PlayerSlot`, and `CharacterId`,
8. synchronize the current authoritative state,
9. enable gameplay input only after `GameplayReady`.

The transport connection identity may change; the durable gameplay/session identities must not.

## Current-state recovery, not historical replay

A reconnecting or late-joining client reconstructs present truth.

It should receive current vitality, inventory, objective/progression, WorldObject, encounter/combat, and other required current state without semantically replaying old damage, pickup, quest-completion, explosion, defeat, audio, or VFX events simply to reconstruct that state.

Detailed duplicate-event mechanics remain primarily covered by deterministic lower-level tests; the multi-process suite proves the assembled recovery path follows the same contract.

## Explicit leave vs interruption

Test deliberate leave separately from unexpected connection loss.

Unexpected process/network loss should enter interruption/reconnect semantics and preserve durable identity during the configured continuity window.

`Leave Game` should travel through the semantic System 23/System 07/System 08 path and produce deliberate departure semantics according to session policy rather than waiting for reconnect timeout or merely closing the socket.

## Join in progress

Join-in-progress belongs in the extended/slower suite rather than necessarily every PR smoke.

A representative test starts gameplay with player A, changes authoritative world/gameplay state, then joins player B through the production join-provider/session path. B must receive current party/session and gameplay state, obtain its correct stable identities, reach `GameplayReady`, and converge without requiring historical event replay or interrupting player A.

## Player count

Do not hard-code a four-player assumption into the generic harness.

For ordinary multiplayer-sensitive PR validation, two clients are the minimum valuable topology because they expose remote replication, ownership/contention, identity, and reconnect behavior.

A slower scheduled/release validation should use the configured supported player capacity and prove that all required participants can form, launch, synchronize, remain distinct, and recover according to policy.

## Network impairment scope

Do not turn System 25 into a general WAN simulator.

Prediction, reconciliation, repair, resync, catch-up, packet semantics, and deterministic protocol behavior already belong primarily to focused lower-level tests.

Cross-process E2E should emphasize failures only this layer can prove, especially process death, disconnect/restart, real application re-entry, and topology/lifecycle boundaries.

If the production transport later exposes a clean deterministic impairment seam, a small number of controlled E2E impairment cases may use it. Do not build a second network emulator solely for this suite.

## Persisted rehost / authority restart

A valuable extended/release case combines Systems 16, 14, 07, 08, and 06:

1. authority A runs a gameplay session,
2. authoritative state is persisted coherently,
3. authority A terminates completely,
4. authority B restores the same durable game through the normal session graph,
5. players reconnect using new transport identities,
6. durable party/gameplay identities and current authoritative state are preserved.

This is slower release/scheduled validation, not a required part of every PR smoke.

## CI tiers

### Fast multiplayer E2E smoke

Automatically select this validation for changes affecting multiplayer/session formation, gameplay replication, reconnect/continuity, networking, shared application-session seams, or their relevant dependencies.

A compact smoke should cover:

- two built player processes sharing one authoritative session,
- formation and `GameplayReady`,
- one authoritative shared mutation/contention case,
- cross-client convergence,
- one unexpected client interruption,
- authoritative state change while that player is absent,
- reconnect to the same semantic identities and current state,
- clean semantic leave/shutdown.

### Extended multiplayer validation

Run for scheduled/release/high-risk validation and include as appropriate:

- configured maximum supported player count,
- join in progress,
- longer mixed gameplay,
- repeated interruption/reconnect,
- persisted authority restart/rehost,
- additional contention/ownership scenarios.

Validation selection should follow repository ownership/diff-driven CI conventions rather than requiring agents to manually register individual tests.

## Failure artifacts

Failures must be diagnosable by process role and semantic milestone, not only by exit code.

Collect role-tagged logs and last-observed semantic diagnostics for authority and each player. Useful failure output includes expected/actual durable identities, session/recovery state, last applied client revision, current authority revision, and the last reached milestone.

## Acceptance / reuse proofs

### Two-player production session

- separate built application processes join one real authoritative session through System 07,
- each receives distinct durable member/slot/character identity,
- all required members satisfy the launch/readiness barrier,
- both reach `GameplayReady`.

### Shared authoritative mutation

- one client performs a real gameplay action,
- the authority resolves it,
- both clients converge on the authoritative semantic result.

### Contention

- two clients race over one authoritative resource/transaction,
- exactly one authoritative outcome is committed,
- both clients converge without duplication or loss.

### Combat/vitality

- a real client action produces an authoritative damage/defeat consequence,
- both clients observe the same resulting vitality/character state.

### Reconnect continuity

- an unexpected client interruption preserves `PartyMemberId`, `PlayerSlot`, and `CharacterId`,
- authoritative gameplay continues while the client is absent,
- the client returns on a new transport connection,
- current state is synchronized,
- the same durable identities regain control only after `GameplayReady`.

### Explicit leave

- semantic `Leave Game` produces deliberate departure semantics distinct from unexpected interruption/reconnect.

### Exact-build proof

- every required role runs the same exact production SHA,
- no missing role or failed launch can be hidden by a passing reduced-topology run.

## Explicitly out of scope

- replacing the existing deterministic network/unit/loopback suite,
- public matchmaking or Internet server discovery,
- NAT/relay/platform certification,
- voice/text chat testing,
- broad load/performance testing,
- packet fuzzing or a new WAN simulator,
- pixel-perfect cross-client visual comparison,
- a parallel multiplayer build/test harness,
- test-only gameplay mutation APIs,
- full campaign/session completion and pacing policy (System 26).

## Relationship to adjacent systems

- **System 24** asks whether the production game composes and plays in a representative built-player slice.
- **System 25** asks whether separate production player processes share that game correctly.
- **System 26** asks whether authored gameplay/session progression forms a coherent complete run.

## Architectural constraints

- Use real separate built-player processes and production networking/session/gameplay code.
- Reuse the shared built-player validation harness; extend it generically for process roles rather than creating a parallel multiplayer framework.
- Enter through production frontend/session/join seams rather than test sockets or direct runtime construction.
- Observe semantic state read-only; never mutate authority through diagnostics.
- Assert clients against authoritative semantic truth, not only against each other.
- Keep durable party/gameplay identities distinct from transport connection identity.
- Treat reconnect as current-state resynchronization, not historical event replay.
- Distinguish explicit leave from unexpected interruption.
- Require the same exact build SHA for every role.
- Keep high-cost maximum-player/rehost scenarios in slower validation tiers.

## Architectural principle

**A test may coordinate built players and observe their public semantic state, but it may not create a test-only multiplayer runtime or directly mutate game authority to advance the scenario.**
