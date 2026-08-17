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
      appearance/front.png
      appearance/back.png
      appearance/left.png
      appearance/right.png
      details/face.png
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

Machine/runtime configuration is separate from asset data. Current profiles:

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

Appearance is declared independently from both `assetType` and generator backend:

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

rigid-multiview      weapon/accessory only; uses object-local surface orientation,
                      rigid-specific multipart foreground masking, and rejects armatures

preserve-generator   any asset type; keep the generator's existing materials/UVs
```

A multiview strategy requires complete front/back/left/right appearance references. That requirement is validated before backend bootstrap or geometry generation. Invalid asset-type/strategy combinations are rejected while loading `BuildSpec`.

The strategy layer deliberately separates **routing/mechanics** from **art-quality acceptance**. Garment and rigid multiview now have independent projection policy. Rigid references also keep substantial disconnected islands while filtering tiny speckles. Visibility/depth reasoning, semantic garment fit, seam quality, and stronger object-orientation semantics remain separate quality work.

## Rigid canonicalization

Weapons and rigid accessories may opt into a generic preparation contract:

```json
{
  "rigid": {
    "blender": "/Applications/Blender.app/Contents/MacOS/Blender",
    "canonicalAxis": "z",
    "targetLength": 1.2,
    "anchorFraction": [0.5, 0.5, 0.1]
  }
}
```

`canonicalAxis` rotates the generated mesh's detected longest bounds axis onto the requested local axis. `targetLength` uniformly scales that longest extent to a physical size. `anchorFraction` translates a normalized bounds point to the origin; for a weapon this is the grip anchor and for an accessory it is the mount anchor. All three are optional so existing assets remain unchanged.

Preparation writes a `*.rigid-contract.json` sidecar recording source/final axis, length, bounds, and anchor. The normal rigid verifier consumes that contract and checks the FBX round-trip. The spec/command contract is covered by fast CI; the Blender round-trip gate remains pending on the shared self-hosted runner.

## Production profiles

`runtime/production.py` owns standard post-build behavior; appearance is delegated to the selected appearance strategy.

### Character

```text
generate geometry
  -> align/transfer canonical rig
  -> character-multiview appearance
  -> skeleton + skin-weight/deformation verifier
  -> animation verifier
  -> bind/lookdev preview
  -> Idle preview
```

Character-specific identity work such as face detail must become a configurable character stage before Madeline is fully migrated.

### Clothing

```text
generate garment
  -> align/transfer canonical rig
  -> garment-multiview OR preserve-generator
  -> skeleton + skin-weight/deformation verifier
  -> lookdev preview
```

`garment-multiview` does not inherit the character T-pose outer-arm redirect. The skinned verifier now requires at least 99% weight coverage per skinned mesh, but that strengthened Blender gate is not checked complete until the self-hosted smoke executes. Body-relative fit/poke-through and seam quality still remain.

### Weapon

```text
generate rigid mesh
  -> optional axis/length/grip canonicalization
  -> rigid-multiview OR preserve-generator
  -> rigid contract + finite-bounds/no-armature verifier
  -> lookdev preview
```

Weapon production still needs automatic grip/axis inference and visual seam/coverage gates; explicit canonicalization is now supported when the intended dimensions/anchor are known.

### Accessory

The rigid accessory path shares the same optional canonical-axis/target-length/mount-anchor contract and socket metadata from `runtimePart`. Two-view or single-view accessories normally use `preserve-generator` until a complete multiview set exists.

## Phase 1 — Generic production orchestration

- [x] Add `character_factory.py produce <spec>`.
- [x] Add recursive `produce-batch <directory>` discovery.
- [x] Route standard verification and preview behavior by `assetType`.
- [x] Preserve the existing `build` command as the low-level generator/preparation primitive.
- [x] Record production-stage decisions and commands in `manifest.json`.
- [x] Restore the prepared character FBX if character appearance projection fails, instead of losing the successful geometry/rig result.
- [x] Run focused production-contract CI; run #1 (`32051087040`) passed compile, routing tests, all four asset-type dry runs, and recursive discovery.
- [ ] Run the existing self-hosted MPS/Blender Character Factory smoke against the generation-framework branch before migration/merge.

## Phase 2 — Reference-set contract

- [ ] Complete reusable reference ingestion with deterministic normalization/re-encoding. PNG/JPEG header and dimension preflight is implemented; normalization/re-encoding remains.
- [x] Support canonical `front/back/left/right` discovery from a reference directory, with explicit per-view overrides.
- [x] Support optional named detail references such as `face`, `hands`, `ornament`, `material`, or `fit` without hard-coding character names.
- [x] Separate **geometry references** from **appearance references** so preprocessing for reconstruction does not destroy texture/identity information; appearance falls back to geometry when omitted.
- [x] Produce a `reference-audit.json` in every non-dry-run production artifact and record resolved reference paths in `manifest.json`.
- [x] Reject missing/ambiguous canonical views and unsupported/invalid image headers before expensive generation starts.
- [x] Run expanded reference-contract CI; run #8 (`32051547185`) passed the reference tests, all four production dry runs, and recursive discovery.

## Phase 3 — Backend profiles

- [x] Add named generator profiles `hunyuan-quality-macos`, `hunyuan-smoke-macos`, and `triposr-smoke-macos`.
- [x] Move cache roots, pinned source revisions, Python environments, model downloads, and bootstrap checks into generic profile/bootstrap code instead of character/weapon production scripts.
- [x] Allow a production asset to request a profile plus only asset-specific overrides such as seed/resolution; reject profile-owned machine-field overrides.
- [x] Keep manifests explicit about selected profile, resolved backend/model parameters, pinned source revision, and bootstrap command.
- [x] Add `profiles` discovery and `bootstrap-profile <name>` CLI commands.
- [x] Add automatic ready-state detection so already-materialized profile environments do not rerun expensive bootstrap work.
- [x] Validate the profile contract in focused CI; run #18 (`32061077951`) passed backend-profile tests and run #23 (`32061349355`) stayed green after the Sun Staff profile migration.

## Phase 4 — Appearance profiles

- [x] Add a common appearance-strategy interface selected by `asset.json` and recorded in both low-level and production manifests.
- [x] Reject incompatible asset-type/strategy combinations and incomplete multiview sets before expensive generation.
- [x] `preserve-generator`: retain generated UV/material output when the backend already supplies useful appearance.
- [x] `garment-multiview`: add a separate clothing route and projection policy with no character/T-pose outer-arm heuristic.
- [ ] Strengthen `garment-multiview` with body-relative fit, depth/occlusion, seam handling, and production visual gates.
- [x] `rigid-multiview`: add a separate weapon/accessory route with rigid-FBX validation and object-surface view selection.
- [x] Add rigid-specific foreground selection that preserves substantial disconnected components while rejecting isolated speckles; pure regression coverage passed in run #52 (`32063774082`).
- [ ] Prove multipart rigid masking and character/garment/rigid projection through Blender; self-hosted appearance run #5 (`32064496502`) is queued with no runner assigned.
- [ ] Strengthen `rigid-multiview` further with seam handling and view/orientation quality gates.
- [ ] `character-multiview`: finish the current Madeline projection repair with bounded/visibility-aware sampling and production visual gates.
- [x] Exercise all four strategies in focused CI; run #42 (`32063213794`) passed compile, appearance/backend/reference/routing tests, all four per-asset dry runs, and recursive batch production.

## Phase 5 — Type-specific validation

- [ ] Character: projection quality, skeleton, weights, animation deformation, identity proof.
- [ ] Clothing: skeleton compatibility, deformation, body fit/poke-through, hidden-body-region metadata, seam quality.
- [x] Add a 99% minimum per-mesh skin-weight coverage gate for character/clothing; Blender proof is still pending before treating this as production-accepted.
- [x] Weapon/accessory: mesh present, no unexpected armature, finite/non-degenerate bounds.
- [x] Add generic rigid canonical-axis/physical-length/grip-or-mount-anchor config plus fast spec/command tests; framework run #63 (`32064307955`) remained green with the contract.
- [ ] Prove rigid canonicalization and contract verification through FBX round-trip in Blender; included in queued appearance run #5 (`32064496502`).
- [ ] Weapon: automatically infer grip axis/location and plausible scale when not explicitly declared.
- [ ] Accessory: automatically infer a plausible local mount transform when not explicitly declared.
- [ ] Prevent Unity staging when the production profile fails.

## Phase 6 — Migrate existing assets

- [ ] Finish Madeline projection repair on `agent/madeline-projection-repair` first.
- [ ] Move Madeline reference normalization, body-only preprocessing, and face identity into reusable/configurable stages. Her generator environment and reference declaration are profile/contract-driven now, but cleanup/face operations remain bespoke.
- [ ] Replace `production/madeline/build.sh` with an `asset.json` plus only genuinely asset-specific preprocessing configuration.
- [x] Migrate the Sunlit Cleric character build to `produce` using `hunyuan-quality-macos`; the script now only creates the canonical donor and writes the asset spec.
- [ ] Migrate the Cleric robe to the clothing production profile. A generic `build_robe_macos.sh` entrypoint now derives robe views, creates `GarmentDonor`, and calls `produce` with `garment-multiview`; a real Hunyuan/Blender production proof is still required.
- [ ] Migrate the Sun Staff fully to the weapon production profile; its TripoSR environment/spec is profile-driven now, but ornament+procedural-shaft composition is still bespoke.
- [ ] Migrate the sun charm/accessories.

## Phase 7 — Scale to many assets

- [x] Build a production asset catalogue/index with type+ID keys, spec/reference SHA-256 fingerprints, generator profile/backend, appearance strategy, runtime slot/socket, and rigid canonicalization metadata.
- [x] Detect duplicate `assetType:id` identities while indexing.
- [x] Support filtered `produce-batch` by repeated `--type` and `--id`; run #68 (`32064672302`) passed catalogue generation and a filtered weapon batch.
- [ ] Add tag filtering and changed-reference/spec selection.
- [ ] Track latest successful artifact hashes/status in the catalogue.
- [ ] Cache expensive geometry generation independently from appearance/verification stages.
- [ ] Re-run only downstream stages when references or configuration affecting those stages change.
- [ ] Add CI smoke fixtures for at least one character, garment, weapon, and accessory.
- [ ] Add production workflows that publish artifacts/proofs without requiring character-specific workflow files.

## Current status

The framework now has a generic asset production runner, separate geometry/appearance/detail references, named backend profiles, explicit character/garment/rigid appearance strategies, rigid multipart-reference handling, optional rigid axis/length/grip-or-mount canonicalization, and a catalogue with filtered batch production. Fast CI is green through run #68 across all four asset types. The main outstanding integration gate is the self-hosted Blender appearance/canonicalization smoke, currently queued with no runner assigned. After that, the highest-value work is art-quality reasoning: finish Madeline visibility-aware projection, add garment body-fit/occlusion gates, add rigid seam/orientation quality, and convert the remaining bespoke Madeline/Sun Staff art operations into declared reusable stages.
