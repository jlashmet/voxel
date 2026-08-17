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
- [ ] Make reference-set ingestion convention-driven instead of requiring ad hoc image-copy/validation shell code.
- [ ] Move generator environment/bootstrap selection into named backend profiles instead of production scripts.
- [ ] Give every asset type an explicit appearance strategy rather than sharing character-specific assumptions.
- [ ] Migrate existing bespoke production scripts onto the generic producer only after their special behavior has a declared extension point.

## Asset library layout

Target convention:

```text
tools/character-factory/production-assets/
  characters/
    madeline/
      asset.json
      views/front.png
      views/back.png
      views/left.png
      views/right.png
      details/face.png           # optional character identity detail
  clothing/
    cleric-robe/
      asset.json
      views/...
  weapons/
    sun-staff/
      asset.json
      views/...
  accessories/
    sun-charm/
      asset.json
      views/...
```

The directory name is organizational. `asset.json` remains authoritative and its `assetType` controls the pipeline.

## Production profiles

The new `runtime/production.py` owns standard post-build behavior.

### Character

Current standard profile:

```text
generate geometry
  -> align/transfer canonical rig
  -> Hunyuan four-view appearance projection when all four views exist
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
  -> preserve generator appearance
  -> skinning/deformation verifier
  -> lookdev preview
```

Do not route garments through the character projector yet. The current multiview code contains T-pose body heuristics. A garment appearance profile needs visibility, body-fit, and seam behavior appropriate for clothing.

### Weapon

Current standard profile:

```text
generate rigid mesh
  -> rigid preparation
  -> preserve generator appearance
  -> rigid finite-bounds/no-armature verifier
  -> lookdev preview
```

Weapon production must later add grip-axis inference, grip location, scale normalization, and rigid multiview appearance/baking where needed.

### Accessory

Current standard profile mirrors rigid weapon production, with socket metadata controlled by the existing `runtimePart` contract.

## Phase 1 — Generic production orchestration

- [x] Add `character_factory.py produce <spec>`.
- [x] Add recursive `produce-batch <directory>` discovery.
- [x] Route standard verification and preview behavior by `assetType`.
- [x] Preserve the existing `build` command as the low-level generator/preparation primitive.
- [x] Record production-stage decisions and commands in `manifest.json`.
- [x] Restore the prepared character FBX if character appearance projection fails, instead of losing the successful geometry/rig result.
- [ ] Run the Character Factory unit suite and production smoke CI on the generation-framework branch.

## Phase 2 — Reference-set contract

- [ ] Introduce a reusable reference-ingestion stage that validates image decode, dimensions, orientation metadata, and deterministic re-encoding.
- [ ] Support canonical `front/back/left/right` discovery from a reference directory.
- [ ] Support optional named detail references such as `face`, `hands`, `ornament`, `material`, or `fit` without hard-coding character names.
- [ ] Separate **geometry references** from **appearance references** so preprocessing for reconstruction does not destroy texture/identity information.
- [ ] Produce a reference audit in every production artifact.
- [ ] Reject missing/ambiguous required views before expensive generation starts.

## Phase 3 — Backend profiles

- [ ] Add named generator profiles such as `hunyuan-quality-macos` and `triposr-smoke-macos`.
- [ ] Move cache roots, model revisions, Python environments, model downloads, and bootstrap checks out of character-specific scripts.
- [ ] Allow a production asset to request a profile plus only asset-specific overrides such as seed/resolution.
- [ ] Keep manifests explicit about the resolved backend/model/revision used for reproducibility.

## Phase 4 — Appearance profiles

- [ ] `character-multiview`: finish the current projection repair with bounded/visibility-aware sampling.
- [ ] `garment-multiview`: project garment references without character T-pose arm heuristics; account for body-relative fit and occlusion.
- [ ] `rigid-multiview`: texture weapons/props using object-centric view selection, seam handling, and grip-independent coordinates.
- [ ] `preserve-generator`: retain generated UV/material output where the backend already supplies useful appearance.
- [ ] Add a common interface so appearance strategy is selected from the production profile, not a bespoke script.

## Phase 5 — Type-specific validation

- [ ] Character: projection quality, skeleton, weights, animation deformation, identity proof.
- [ ] Clothing: skeleton compatibility, deformation, body fit/poke-through, hidden-body-region metadata, seam quality.
- [x] Weapon/accessory: mesh present, no unexpected armature, finite/non-degenerate bounds.
- [ ] Weapon: grip axis/location and plausible scale.
- [ ] Accessory: socket metadata and plausible local transform.
- [ ] Prevent Unity staging when the production profile fails.

## Phase 6 — Migrate existing assets

- [ ] Finish Madeline projection repair on `agent/madeline-projection-repair` first.
- [ ] Move Madeline reference normalization, body-only preprocessing, and face identity into reusable/configurable stages.
- [ ] Replace `production/madeline/build.sh` with an `asset.json` plus only genuinely asset-specific preprocessing configuration.
- [ ] Migrate the Sunlit Cleric character build to `produce`.
- [ ] Migrate the Cleric robe to the clothing production profile.
- [ ] Migrate the Sun Staff to the weapon profile, preserving its ornament+procedural-shaft composition as a declared weapon composition stage.
- [ ] Migrate the sun charm/accessories.

## Phase 7 — Scale to many assets

- [ ] Build a production asset catalogue/index with IDs, types, references, dependencies, and latest successful artifact hashes.
- [ ] Support filtered batches by type, ID, tag, or changed reference/spec.
- [ ] Cache expensive geometry generation independently from appearance/verification stages.
- [ ] Re-run only downstream stages when references or configuration affecting those stages change.
- [ ] Add CI smoke fixtures for at least one character, garment, weapon, and accessory.
- [ ] Add production workflows that publish artifacts/proofs without requiring character-specific workflow files.

## Current status

The low-level Character Factory was already more generic than the existing production scripts: `BuildSpec` and runtime routing already distinguish character, clothing, weapon, and accessory. The new generation-framework branch adds the missing common production layer. The next high-leverage work is the reference-set contract and named backend profiles; those two changes remove most of the shell-script duplication that currently makes adding a new character or item expensive.
