# Character Factory Generation Framework Plan

This document is the source of truth for turning the existing Character Factory into a repeatable production system for many characters, clothing pieces, weapons, and accessories.

## Goal

A new asset should be primarily **data + reference images**, not a new shell script.

The common production lifecycle is:

```text
reference images
  -> BuildSpec
  -> asset-type generator pipeline
  -> asset-type appearance pipeline
  -> asset-type validation
  -> proof renders
  -> manifest
  -> optional Unity staging
```

`assetType` selects behavior. Character, clothing, weapon, and accessory remain separate products because their fitting, rigging, mounting, validation, and appearance rules are different.

## Core principles

- [x] Keep one low-level `build` contract driven by `BuildSpec` and `assetType`.
- [x] Keep separate character, clothing, weapon, and accessory preparation pipelines.
- [x] Add a generic production layer above `build` so each production asset does not reimplement validation/render/staging orchestration.
- [x] Add recursive production-spec discovery so a library of assets can be generated in batches.
- [x] Give rigid weapons/accessories a real automated validation gate.
- [x] Make reference-set ingestion convention-driven instead of requiring ad hoc path wiring; canonical views, geometry/appearance separation, named details, and image preflight are now generic. Deterministic re-encoding remains follow-up work.
- [x] Move generator environment/bootstrap selection into named backend profiles instead of production scripts.
- [x] Give every asset type an explicit appearance strategy instead of sharing character-specific assumptions.
- [ ] Migrate existing bespoke production scripts onto the generic producer only after their special behavior has a declared extension point.

## Asset library layout

Target convention:

```text
tools/character-factory/production-assets/
  characters/
    madeline/
      asset.json
      geometry/front.png
      geometry/back.png
      geometry/left.png
      geometry/right.png
      appearance/front.png       # optional; geometry is the fallback
      appearance/back.png
      appearance/left.png
      appearance/right.png
      details/face.png           # optional named identity/detail source
  clothing/
    cleric-robe/
      asset.json
      geometry/...
      appearance/...
  weapons/
    sun-staff/
      asset.json
      geometry/...
      details/ornament.png
  accessories/
    sun-charm/
      asset.json
      geometry/...
```

The directory name is organizational. `asset.json` remains authoritative and its `assetType` controls the pipeline. A reference block can discover canonical view names from a directory:

```json
{
  "references": {
    "geometry": { "directory": "geometry" },
    "appearance": { "directory": "appearance" },
    "details": { "face": "details/face.png" }
  }
}
```

Existing top-level `views` remain supported during migration. A spec may add `references.details` alongside legacy views, but it cannot define both legacy `views` and `references.geometry` because that would make the geometry source ambiguous.

## Generator backend profiles

Machine/runtime configuration is now separate from asset data. Current profiles:

```text
hunyuan-quality-macos
hunyuan-smoke-macos
triposr-smoke-macos
```

A profile owns backend selection, pinned source revision, managed Python environment, source checkout/weights where applicable, and its bootstrap script. Assets cannot override those environment-owned fields. They can override art/generation knobs such as seed, steps, octree/MC resolution, chunking, model/subfolder, and background handling.

Example:

```json
{
  "generator": {
    "profile": "hunyuan-quality-macos",
    "seed": 31827,
    "removeBackground": true
  }
}
```

Both `build` and `produce` bootstrap a missing profile automatically and skip bootstrap work when the exact managed runtime/model files are already ready. `bootstrap-profile <name>` exists for transitional preprocessing that needs the profile-managed Python before generation.

## Appearance strategies

Appearance is now declared independently from both `assetType` and generator backend:

```json
{
  "appearance": {
    "strategy": "garment-multiview"
  }
}
```

Registered strategies are:

```text
character-multiview  character only; current body/T-pose policy
                      includes the outer-arm side-view redirect

garment-multiview    clothing only; shares atlas/mask/UV mechanics but uses
                      local surface orientation without character arm heuristics

rigid-multiview      weapon/accessory only; uses object-local surface orientation
                      and rejects unexpected armatures

preserve-generator   any asset type; keep the generator's existing materials/UVs
```

A multiview strategy requires complete front/back/left/right appearance references. That requirement is validated before backend bootstrap or geometry generation. Invalid asset-type/strategy combinations are rejected while loading `BuildSpec`.

The strategy layer deliberately separates **routing/mechanics** from **art-quality acceptance**. Garment and rigid multiview now have independent projection policy, but visibility/depth reasoning, semantic fit, seams, disconnected rigid components, and orientation normalization remain quality work below.

## Production profiles

`runtime/production.py` owns standard post-build behavior; appearance is delegated to the selected appearance strategy.

### Character

Current standard profile:

```text
generate geometry
  -> align/transfer canonical rig
  -> character-multiview appearance
  -> skinning verifier
  -> animation verifier
  -> bind/lookdev preview
  -> Idle preview
```

Character-specific identity work such as face detail must become a configurable character stage before Madeline is fully migrated.

### Clothing

Current standard profile:

```text
generate garment
  -> align/transfer canonical rig
  -> garment-multiview OR preserve-generator
  -> skinning/deformation verifier
  -> lookdev preview
```

`garment-multiview` no longer inherits the character T-pose outer-arm redirect. It still needs body-relative fit/occlusion and seam-quality gates before it is considered production-art complete.

### Weapon

Current standard profile:

```text
generate rigid mesh
  -> rigid preparation
  -> rigid-multiview OR preserve-generator
  -> rigid finite-bounds/no-armature verifier
  -> lookdev preview
```

Weapon production still needs grip-axis inference, grip location, scale normalization, stronger rigid-view orientation handling, and visual seam/coverage quality gates.

### Accessory

Current standard profile mirrors rigid weapon production, with socket metadata controlled by the existing `runtimePart` contract. Two-view or single-view accessories normally use `preserve-generator` until a complete multiview set exists.

## Phase 1 — Generic production orchestration

- [x] Add `character_factory.py produce <spec>`.
- [x] Add recursive `produce-batch <directory>` discovery.
- [x] Route standard verification and preview behavior by `assetType`.
- [x] Preserve the existing `build` command as the low-level generator/preparation primitive.
- [x] Record production-stage decisions and commands in `manifest.json`.
- [x] Restore the prepared character FBX if character appearance projection fails, instead of losing the successful geometry/rig result.
- [x] Run the focused production-contract CI on the generation-framework branch; run #1 (`32051087040`) passed compile, routing tests, all four asset-type dry runs, and recursive discovery.
- [ ] Run the existing self-hosted MPS/Blender Character Factory smoke against the generation-framework branch before migration/merge.

## Phase 2 — Reference-set contract

- [ ] Complete reusable reference ingestion with deterministic normalization/re-encoding. PNG/JPEG header and dimension preflight is implemented; normalization/re-encoding remains.
- [x] Support canonical `front/back/left/right` discovery from a reference directory, with explicit per-view overrides.
- [x] Support optional named detail references such as `face`, `hands`, `ornament`, `material`, or `fit` without hard-coding character names.
- [x] Separate **geometry references** from **appearance references** so preprocessing for reconstruction does not destroy texture/identity information; appearance falls back to geometry when omitted.
- [x] Produce a `reference-audit.json` in every non-dry-run production artifact and record resolved reference paths in `manifest.json`.
- [x] Reject missing/ambiguous canonical views and unsupported/invalid image headers before expensive generation starts.
- [x] Run the expanded reference-contract CI; run #8 (`32051547185`) passed the reference tests, all four production dry runs, and recursive discovery.

## Phase 3 — Backend profiles

- [x] Add named generator profiles `hunyuan-quality-macos`, `hunyuan-smoke-macos`, and `triposr-smoke-macos`.
- [x] Move cache roots, pinned source revisions, Python environments, model downloads, and bootstrap checks into generic profile/bootstrap code instead of character/weapon production scripts.
- [x] Allow a production asset to request a profile plus only asset-specific overrides such as seed/resolution; reject profile-owned machine-field overrides.
- [x] Keep manifests explicit about the selected profile, resolved backend/model parameters, pinned source revision, and bootstrap command for reproducibility.
- [x] Add `profiles` discovery and `bootstrap-profile <name>` CLI commands.
- [x] Add automatic ready-state detection so already-materialized profile environments do not rerun expensive bootstrap work.
- [x] Validate the profile contract in focused CI; run #18 (`32061077951`) passed backend-profile tests and all existing production-contract gates, and run #23 (`32061349355`) stayed green after the Sun Staff profile migration.

## Phase 4 — Appearance profiles

- [x] Add a common appearance-strategy interface selected by `asset.json` and recorded in both low-level and production manifests.
- [x] Reject incompatible asset-type/strategy combinations and incomplete multiview sets before expensive generation.
- [x] `preserve-generator`: retain generated UV/material output when the backend already supplies useful appearance.
- [x] `garment-multiview`: add a separate clothing route and projection policy with no character/T-pose outer-arm heuristic.
- [ ] Strengthen `garment-multiview` with body-relative fit, depth/occlusion, seam handling, and production visual gates.
- [x] `rigid-multiview`: add a separate weapon/accessory route with rigid-FBX validation and object-surface view selection.
- [ ] Strengthen `rigid-multiview` with orientation normalization, disconnected-component-safe masking, seam handling, and grip-independent coordinates.
- [ ] `character-multiview`: finish the current Madeline projection repair with bounded/visibility-aware sampling and production visual gates.
- [x] Exercise all four strategies in focused CI; run #42 (`32063213794`) passed compile, appearance/backend/reference/routing tests, all four per-asset dry runs, and recursive batch production.

## Phase 5 — Type-specific validation

- [ ] Character: projection quality, skeleton, weights, animation deformation, identity proof.
- [ ] Clothing: skeleton compatibility, deformation, body fit/poke-through, hidden-body-region metadata, seam quality.
- [x] Weapon/accessory: mesh present, no unexpected armature, finite/non-degenerate bounds.
- [ ] Weapon: grip axis/location and plausible scale.
- [ ] Accessory: socket metadata and plausible local transform.
- [ ] Prevent Unity staging when the production profile fails.

## Phase 6 — Migrate existing assets

- [ ] Finish Madeline projection repair on `agent/madeline-projection-repair` first.
- [ ] Move Madeline reference normalization, body-only preprocessing, and face identity into reusable/configurable stages. Her generator environment and reference declaration are profile/contract-driven now, but the cleanup/face operations remain bespoke.
- [ ] Replace `production/madeline/build.sh` with an `asset.json` plus only genuinely asset-specific preprocessing configuration.
- [x] Migrate the Sunlit Cleric character build to `produce` using `hunyuan-quality-macos`; the script now only creates the canonical donor and writes the asset spec.
- [ ] Migrate the Cleric robe to the clothing production profile.
- [ ] Migrate the Sun Staff fully to the weapon production profile; its TripoSR environment/spec is profile-driven now, but ornament+procedural-shaft composition is still a bespoke stage.
- [ ] Migrate the sun charm/accessories.

## Phase 7 — Scale to many assets

- [ ] Build a production asset catalogue/index with IDs, types, references, dependencies, and latest successful artifact hashes.
- [ ] Support filtered batches by type, ID, tag, or changed reference/spec.
- [ ] Cache expensive geometry generation independently from appearance/verification stages.
- [ ] Re-run only downstream stages when references or configuration affecting those stages change.
- [ ] Add CI smoke fixtures for at least one character, garment, weapon, and accessory.
- [ ] Add production workflows that publish artifacts/proofs without requiring character-specific workflow files.

## Current status

The framework now has four scalable layers: a generic asset-type production runner, a geometry/appearance/detail reference contract, pinned backend profiles that own machine/model setup, and explicit appearance strategies for character, garment, rigid equipment, or generator-preserved materials. Focused CI is green across all four asset types and all four appearance routes. The next highest-leverage work is **quality and migration**, not another routing abstraction: finish Madeline's visibility-aware character projection, add garment fit/occlusion validation, improve rigid orientation/seams/grip metadata, and turn the remaining Madeline/Sun Staff art operations into declared reusable stages.
