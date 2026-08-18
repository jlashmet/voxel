# voxel

A destructible and buildable multiplayer voxel world, built in Unity.

`AGENTS.md` targets a different agent tool and does not apply to Claude Code. Ignore it — this
file, the specs it references, and the constitution are authoritative. In particular its
validation loop, which assumes Unity cannot be run locally and drives everything through
push-triggered CI, is not the workflow here: use `tools/unity-run.sh` as described below.

## Active feature

<!-- SPECKIT START -->
**Plan**: [specs/002-world-feature-authoring/plan.md](specs/002-world-feature-authoring/plan.md)

Supporting artifacts:

- [spec.md](specs/002-world-feature-authoring/spec.md) — requirements and success criteria
- [research.md](specs/002-world-feature-authoring/research.md) — Phase 0 decisions
- [data-model.md](specs/002-world-feature-authoring/data-model.md) — structures and invariants
- [contracts/](specs/002-world-feature-authoring/contracts/) — catalogue format, shape program, module interfaces
- [quickstart.md](specs/002-world-feature-authoring/quickstart.md) — orientation

**Foundation**: [specs/001-destructible-voxel-engine/plan.md](specs/001-destructible-voxel-engine/plan.md) — the engine this builds on, with its own spec, research, data model, contracts, and architecture notes.
<!-- SPECKIT END -->

**Numeric budgets**: [device-matrix.md](specs/001-destructible-voxel-engine/device-matrix.md) is authoritative for every frame, memory, latency, and bandwidth target. Do not invent numbers elsewhere.

**Constitution**: [.specify/memory/constitution.md](.specify/memory/constitution.md) — six non-negotiable principles. The standing constraints below are their feature-level expression.

## Standing constraints

These are architectural commitments, not preferences. Violating one is a defect.

- **Determinism**: no authoritative state may derive from GPU output or floating-point arithmetic. Cross-client agreement is integer Burst jobs on the CPU.
- **Single source of truth**: visual and collision representations derive from the same authoritative voxel cells. Collision uses discrete occupancy; curvature is derived presentation and never gameplay truth.
- **Server authority**: client prediction is presentation, never truth.
- **Tiering boundary**: device class affects presentation parameters only — never interest radius, tick rate, collision, world state, or any `Core` job.
- **Platform scope**: PC, console, and **high-end mobile only**. Mid-tier and low-tier phones are out of scope.
- **No `com.unity.entities`**: Burst, Collections, and Jobs only. The world is not entity-shaped.
- **No Netcode for GameObjects**: world replication is custom over Unity Transport.

## Running Unity

**Never invoke the Unity binary directly. Always use `tools/unity-run.sh`.**

Unguarded headless runs froze this machine repeatedly. The cause each time was a second
Unity editor started against a project copy — real graphics device, hundreds of megabytes of
ComputeBuffer — while the developer's own editor was open. Unified memory, two editors, no
limit anywhere in Unity.

The wrapper refuses to start when another editor is running or when the machine is short on
memory, and it kills the process tree if it crosses a memory ceiling or a time limit. Its
defaults (6 GB, 20 minutes, 4 GB free required) are ceilings, not targets.

Two further rules that no script can enforce:

- **Ask before running Unity at all if the developer's editor might be open.** The wrapper
  will refuse, but the polite order is to ask first.
- **Batchmode play-mode tests do not exercise the editor lifecycle.** They run once, in a
  fresh domain. They cannot see `ExecuteAlways` work, repeated `OnEnable`, repeated
  `ScriptableRendererFeature.Create`, or anything that happens on a domain reload. Every
  memory failure in this project so far lived in exactly that blind spot and passed a green
  suite. Editor-lifecycle behaviour needs EditMode tests that loop the lifecycle — see
  `Assets/Tests/EditMode/RenderResourceLifetimeTests.cs`.

## Superseded

`📘 Decoupled Voxel Rendering & Multiplayer Synchron.txt` (CTBS + CGVAVS v1.0) is retained for reference only and was reviewed and rejected. See `architecture-notes.md` §9. Do not use its performance claims or schedule estimates.
