# Device Matrix & Quantitative Targets

**Created**: 2026-08-04
**Authoritative for**: all numeric budgets referenced by `spec.md` Success Criteria and by `plan.md` milestone exits.
**Status**: Targets set. Measured columns are filled by the M0 spike (T008–T011).

Closes analysis finding **U1** (no quantitative target existed anywhere in the artifact set) and **D1** (the critical path began with an unmade product decision).

---

## Supported device classes

Three tiers. Mid-tier and low-tier mobile are **out of scope** (`spec.md` Out of Scope).

| Tier | Definition | Graphics API |
|---|---|---|
| **PC** | Discrete GPU, 8 GB VRAM or more | Vulkan 1.2 / DX12 |
| **Console** | Current-generation consoles | Platform-native |
| **Mobile-HE** | Flagship phones released within ~3 years, with a modern tile-based GPU supporting Vulkan 1.1+ or Metal 3 | Vulkan 1.1+ / Metal 3 |

**Minimum-spec reference device**: a recent flagship phone, to be named before T008 begins. The class is now specified tightly enough that the exact model choice no longer gates the architecture — any device meeting the Mobile-HE definition is a valid M0 target.

---

## Frame and tick budgets

| Budget | PC | Console | Mobile-HE |
|---|---|---|---|
| Target frame rate | 60 fps | 60 fps | 60 fps |
| **Frame budget** | 16.6 ms | 16.6 ms | 16.6 ms |
| Voxel rendering share of frame | ≤ 6 ms | ≤ 7 ms | ≤ 9 ms |
| Streaming work on main thread | ≤ 0.5 ms | ≤ 0.5 ms | ≤ 0.5 ms |
| Acceptable frame overrun rate | 0 attributable to streaming (SC-004) | same | same |

**Simulation tick rate: 30 Hz, all tiers.** Tick rate is simulation, not presentation, so it does **not** tier (C-006).

| Latency budget | Target |
|---|---|
| World-update latency, server → client visible (SC-001) | ≤ 150 ms at p95 |
| Local alteration feedback (speculative, FR-008) | ≤ 1 frame |
| Reconciliation rollback window | 500 ms (15 ticks) |
| Region event log hot retention | 2 s (60 ticks), then compaction eligible |

---

## Memory budgets

| Budget | PC | Console | Mobile-HE |
|---|---|---|---|
| **Brick pool** | 2.0 GB | 1.0 GB | 384 MB |
| Approx. unique mixed bricks (2,112 B each) | ~947 K | ~508 K | ~190 K |
| Region pointer tables | 192 MB | 128 MB | 48 MB |
| Debris and transient | 128 MB | 96 MB | 32 MB |
| **Total world-attributable** | ~2.3 GB | ~1.2 GB | ~464 MB |

**SC-005 check**: total world-attributable memory must be flat over a two-hour session — no upward trend beyond ±2%.

---

## Detail radius and LOD transitions

| Parameter | PC | Console | Mobile-HE |
|---|---|---|---|
| Full-detail radius (mip 0) | 400 m | 350 m | 200 m |
| Mip 2–3 transition | 400–1200 m | 350–1000 m | 200–600 m |
| Implicit far-field (mip 5+) | beyond 1200 m | beyond 1000 m | beyond 600 m |
| Max view distance | 10 km | 10 km | 6 km |
| Region load radius | 500 m | 450 m | 300 m |
| Region unload radius | 650 m | 600 m | 420 m |
| Voxel render scale | 1.0 | 1.0 | 0.75 + upscale |
| Solid extraction build budget | 0.20 ms/frame | 0.20 ms/frame | 0.20 ms/frame |
| Irradiance probe spacing | 2 m | 2 m | 4 m |
| Max visual-only debris bodies | 2000 | 1500 | 400 |

**Load/unload hysteresis gap is ≥ 25% of load radius on every tier.** This is a correctness requirement, not a tuning preference — see `contracts/module-interfaces.md`.

---

## Bandwidth budgets

Per player, at 64 concurrent players under sustained heavy destruction.

| Budget | Wired / Wi-Fi | Mobile-HE on cellular |
|---|---|---|
| **Sustained downstream** | ≤ 256 KB/s | ≤ 96 KB/s |
| **Peak downstream (2 s window)** | ≤ 512 KB/s | ≤ 192 KB/s |
| Sustained upstream | ≤ 32 KB/s | ≤ 24 KB/s |
| EVENT channel share | ≥ 60% reserved | ≥ 70% reserved |
| BULK channel | remainder, yields to EVENT | remainder, yields to EVENT |

**SC-002 check**: a destruction event affecting ≥ 4000 voxels must transmit in ≤ 64 bytes — within 2× the cost of an ordinary player action.

**SC-014 check**: every participant stays within these figures for the whole session.

---

## Network resilience targets

Conditions the parity harness (T014) injects; SC-016 must hold under all of them.

| Condition | Wired / Wi-Fi | Mobile-HE |
|---|---|---|
| Packet loss | 1% | 5% |
| Latency | 40 ms RTT | 120 ms RTT |
| Jitter | ±10 ms | ±60 ms |
| Brief outage tolerance | 1 s | 3 s |

---

## World features (spec 002)

Feature generation participates in cross-client agreement, so **every number here is identical on
every tier**. Device class may change how a feature is drawn; it may not change whether it exists,
where it is, or what it is made of (Principle IV). A tiered placement budget would put a village
on a PC and not on a phone, which is the same class of defect as tiering interest radius.

| Parameter | PC | Console | Mobile-HE |
|---|---|---|---|
| Feature generation per region | 8 ms | 8 ms | 8 ms |
| Max primitives rasterised per region | 4096 | 4096 | 4096 |
| Max candidates scanned per region | 512 | 512 | 512 |
| Max primitives per instance | 512 | 512 | 512 |
| Max footprint per definition | 1280 voxels (128 m) | 1280 | 1280 |
| Placement cell edge | 640 voxels (64 m) | 640 | 640 |
| Catalogue size limit | 256 definitions | 256 | 256 |
| Stored state per touched instance | 64 B | 64 B | 64 B |

**The 8 ms generation budget is provisional.** Terrain generation alone measures ~45 ms per
region, so this number is a target rather than an observation, and spec 002 task T058 measures
against it before anything is built on the assumption. If measurement disagrees, this table
changes — not the code that reads it.

**Feature generation shares the streaming budget rather than adding to it.** It is spent inside
the region generation slice, not alongside it.

---

## Scale targets

| Parameter | Value |
|---|---|
| Concurrent players per instance | 64 (32 minimum viable) |
| World extent | 4 km × 4 km × 1 km |
| Voxel scale | 10 cm |
| Brick | 8³ voxels (0.8 m) |
| Region | 64³ bricks (51.2 m) |
| Regions in world | 78 × 78 × 20 ≈ 122,000 |
| Always-resident coarse mip, server | ≤ 256 B per region ≈ 31 MB total |

---

## M0 measurement targets

The spike (T008–T011) fills this table. **Pass condition: Mobile-HE voxel rendering ≤ 9 ms at 0.75 render scale.**

| Measurement | Target | Measured |
|---|---|---|
| Full-detail solid rendering, Mobile-HE, 0.75 scale | ≤ 9 ms | _pending_ |
| Implicit-only contingency, Mobile-HE | ≤ 5 ms | _pending_ |
| Full-detail solid rendering, PC, 1.0 scale | ≤ 6 ms | _pending_ |
| Brick pool 384 MB resident, Mobile-HE | no thermal throttle over 20 min | _pending_ |

**Go/no-go (T011)**: if full-detail exceeds 9 ms on Mobile-HE, reduce presentation
radius/resolution and use the implicit far field sooner—lower fidelity, same data, same collision.

---

## What may and may not tier

Restates `architecture-notes.md` §8.1 as the enforceable test matrix for SC-013.

**May tier**: brick pool capacity · detail radius and mip transitions · voxel render scale · irradiance probe spacing · visual-only debris count · max view distance.

**May not tier**: tick rate · world-update latency budget · interest radius · collision and hit resolution · world state · any `Core` integer job · reconciliation window.

`DeviceTierBudget` structurally omits every field in the second list (`contracts/module-interfaces.md`).