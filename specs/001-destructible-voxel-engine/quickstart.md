# Quickstart — Destructible & Buildable Multiplayer Voxel World

**Created**: 2026-08-04
**Audience**: a developer picking up implementation from this plan.

---

## Read in this order

0. [`.specify/memory/constitution.md`](../../.specify/memory/constitution.md) — six non-negotiable principles. Read first; everything else assumes them.
1. [spec.md](./spec.md) — what is being built and how it will be judged
2. [device-matrix.md](./device-matrix.md) — every numeric target. Authoritative; do not invent budgets elsewhere.
3. [research.md](./research.md) — the eleven decisions and why alternatives were eliminated
4. [architecture-notes.md](./architecture-notes.md) — the technical reasoning in depth, plus the review of the superseded proposal
5. [data-model.md](./data-model.md) — structures and invariants
6. [contracts/](./contracts/) — the seams
7. [plan.md](./plan.md) — milestones

If reading only one thing, read `architecture-notes.md` §§2–7. Everything else follows from the brickmap and event-sourcing choices made there.

---

## The five facts that explain the design

1. **An edit is one byte written into a pooled GPU buffer.** No mesh rebuild, no geometry upload. This is why full destructibility is affordable, and it is the reason meshing was rejected.
2. **Allocation follows surface area, not volume.** Empty bricks cost nothing; uniform bricks cost nothing marginal. Solid rock underground is free at any quantity. This is why a km-scale world fits a capped memory budget.
3. **Replication transmits the cause, not the effect.** "Explosion at P, radius R, seed S" is ~32 bytes and expands deterministically to thousands of voxel writes on every client. This is why a grenade costs what a gunshot costs, and why 64 players fit a mobile connection.
4. **One occupancy mip hierarchy serves five consumers** — raymarch skipping, streaming LOD, far-field replication, connectivity, support propagation. Built once, consumed everywhere.
5. **Determinism lives in integer Burst jobs on the CPU.** Never GPU, never float. The client population spans PC, console, and mobile GPUs, so drift between hardware classes is not hypothetical.

---

## Environment

- Unity with URP (R-003)
- Packages: `com.unity.burst`, `com.unity.collections`, `com.unity.jobs`, `com.unity.transport`
- **Not** `com.unity.entities` (R-002), **not** Netcode for GameObjects (R-001)
- A compute-shader-capable target on every device class. On mobile this means Vulkan 1.1+ or Metal 3 on a recent flagship — mid-tier and low-tier phones are out of scope.
- Two machines of different hardware classes, one of them Mobile-HE, for the parity harness. Needed from M1, not later.

---

## Start here

**M0 first**: the mobile raymarch spike. A throwaway brickmap raymarch at target resolution on a Mobile-HE device, plus the implicit-only contingency alongside it. Pass condition is **≤ 9 ms voxel rendering at 0.75 render scale**, and no thermal throttle over 20 minutes.

This used to be the project's top risk. Narrowing mobile scope to recent flagship phones largely retired it — the target hardware reliably provides Vulkan 1.1+ or Metal 3 with compute throughput closer to console than to the mass-market floor. Treat M0 as expected-pass verification rather than an open question, and note that the likelier failure is now sustained thermal behaviour, not raw capability.

Still do it first: a measurement that fails is far cheaper now than after M4. But it no longer blocks Phase 2 storage work, which has no dependency on it.

If it does fail, render mobile entirely through the implicit/mip path at all distances — lower fidelity, same data, same collision. It is **not** to add a mesh-based mobile path: that reintroduces the per-edit rebuild cost the whole architecture exists to avoid, and would require a second collision and LOD pipeline.

---

## Things that cannot be retrofitted

Get these right the first time; each is a rewrite if deferred.

| Thing | Why |
|---|---|
| `RegionEventLog.tickIndex` | Reconciliation replays inputs against world state *at each tick*. Adding the index later means rewriting the log and everything reading it. |
| Hysteresis between load and unload radii | Not a tuning parameter. Without it a player standing on a boundary thrashes regions every frame. |
| Uniform-brick collapse on `SetVoxel` | Skip it and memory leaks slowly in a way that only shows up in long sessions. |
| Integer-only determinism in `Core` | One float in an expansion path and clients diverge silently between hardware classes. |
| `DeviceTierBudget` excluding `interestRadius` | Enforce structurally, by the type. Tying update range to draw range disadvantages mobile players and is an easy accidental coupling. |
| `Core` having no `UnityEngine` dependency | It is what makes the parity harness possible. |

---

## How to know it works

Two tests carry most of the weight:

- **SC-003** — two clients on different hardware, 10,000 alteration events, byte-identical world state. This is the determinism guarantee made concrete, and it should exist as a harness from M1.
- **SC-013** — a player on the lowest device class and one on the highest perform the same action against the same world and obtain the same outcome, 100% of trials. This is C-006 made concrete.

If both hold continuously, the architecture's central claims are being honoured. If either drifts, stop and find out why before adding features.

---

## Superseded material

`../../📘 Decoupled Voxel Rendering & Multiplayer Synchron.txt` (CTBS + CGVAVS v1.0) is retained for reference only. Its central mechanism — a shared "confidence map" driving both network reconciliation and render detail — was reviewed and rejected; see `architecture-notes.md` §9 for the findings and for the material that was retained from it. Do not use its performance claims or its schedule estimates for planning.
