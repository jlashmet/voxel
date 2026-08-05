# Project Constitution

**Created**: 2026-08-04
**Applies to**: all features in this repository.

These principles are non-negotiable. A change that violates one is a defect regardless of what it enables. Amending a principle requires an explicit, separate constitution update — never a local exception inside a feature.

Each principle exists because a specific, identified failure mode is otherwise likely and difficult to detect after the fact.

---

## I. Determinism is integer and CPU-side

**No authoritative state MUST derive from GPU output or floating-point arithmetic.**

All computation participating in cross-client agreement — edit expansion, connectivity, support propagation, collision — MUST be integer arithmetic in Burst-compiled jobs on the CPU, seeded where randomness is needed.

*Why*: replication transmits causes rather than effects, so every client re-derives the same world independently. GPU compute results are not bit-identical across vendors, drivers, or occupancy-dependent scheduling, and float arithmetic is not reproducible across platforms. The client population spans PC, console, and mobile GPUs. The failure mode is slow, silent divergence between hardware classes that no single client can detect.

*Enforcement*: `Assets/VoxelEngine/Core/` forbids `float` and `double` by analyzer rule. Cross-hardware parity tests run continuously, not at milestones.

## II. One source of truth for geometry

**Visual and collision representations MUST derive from the same data, through the same traversal.**

*Why*: divergence between what a player sees and what stops a bullet is the defect players notice most and trust least. Two representations will drift; the only reliable prevention is structural — one DDA traversal with two callers.

*Enforcement*: no collision query may consult GPU state. No rendering path may hold authoritative state. Any proposal introducing a second geometric representation requires an amendment.

## III. The server is authoritative; prediction is presentation

**Client-side prediction MUST NEVER be a source of truth.**

Predicted state lives in an explicitly separate, visually distinct layer until the server confirms it. Rejection is a discard, never a merge, and MUST always carry a reason to the player.

*Why*: a mutable world plus client authority is an unbounded cheat surface. Presenting provisional state as settled is also dishonest UX — players tolerate "pending" far better than state that silently reverses.

*Enforcement*: exactly one code path from a client message to a state mutation, and it passes through validation. Speculative state is structurally separate from the authoritative grid.

## IV. Device class affects presentation only

**Tiering MUST NOT alter outcomes.**

Device class may adjust memory budgets, detail radius, render scale, probe density, and visual-only effects. It MUST NOT alter world state, collision, hit resolution, tick rate, reconciliation, interest radius, or any deterministic job.

*Why*: crossplay means players on different hardware compete directly. The specific trap is coupling interest-management radius to draw distance — a natural-seeming optimisation that silently disadvantages an entire platform.

*Enforcement*: the tier budget type structurally omits every simulation parameter. Cross-device outcome parity is a continuous test, not a release check.

## V. Bounded resources by construction

**Memory and storage MUST be bounded by configuration, not by world size or session length.**

Pools are fixed-capacity with eviction rather than growth. Logs compact. Player allocation is budgeted.

*Why*: a world larger than memory and a session that only accumulates alteration will both exhaust their host unless bounded structurally. Growth defects surface only in long sessions, which is the most expensive place to find them.

*Enforcement*: allocation policy is settled before an allocator is considered complete. Long-session memory flatness is a tested criterion.

## VI. Quantitative targets before optimisation work

**A performance criterion MUST carry a number before work is scheduled against it.**

*Why*: "fast", "smooth", and "within budget" cannot be passed or failed, and a spike measuring against an undefined target produces a measurement rather than a decision.

*Enforcement*: success criteria reference a named budget document. Milestone exits state thresholds.

---

## Amendment

Amendments require: the failure mode the change permits, why it is acceptable, and what replaces the removed protection. Recorded here with a date, never as a per-feature exception.
