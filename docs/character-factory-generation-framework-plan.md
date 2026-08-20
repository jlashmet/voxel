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
- [x] Move canonical skeleton/donor selection into named rig profiles instead of per-asset GLB paths.
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

## Canonical rig profiles

Character and clothing asset data can also select a named canonical skeleton/donor contract:

```json
{
  "rig": {
    "profile": "canonical-humanoid-macos"
  }
}
```

`canonical-humanoid-macos` owns Blender selection, the canonical donor identity, the `Armature`, and whether the asset consumes `Body` or `GarmentDonor`. The donor cache key is the SHA-256 of the canonical-donor generator code, so editing the skeleton/donor definition automatically creates a new canonical revision instead of silently mutating old geometry-cache inputs. A prepared-geometry cache hit bypasses both canonical-rig bootstrap and generator-backend bootstrap. `rig-profiles` lists available profiles and `bootstrap-rig-profile <name>` materializes one explicitly when needed.

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

## Rigid canonicalization and composition

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

Rigid assets may also declare `rigid.composition.strategy = generated-detail-shaft`. That strategy reconstructs a named detail reference such as an ornament/pommel, assembles it with a procedural shaft, and then applies the same rigid canonicalization/verifier contract. Because the consumed detail affects mesh geometry, its bytes participate in the geometry fingerprint and catalogue change classification.

Preparation writes a `*.rigid-contract.json` sidecar recording source/final axis, length, bounds, and anchor. The normal rigid verifier consumes that contract and checks the FBX round-trip. Consolidated self-hosted run #726 (`32074947199`) proved the rigid canonicalization round-trip plus generated-detail weapon and accessory composition in Blender.

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

`garment-multiview` does not inherit the character T-pose outer-arm redirect. The skinned verifier requires at least 99% weight coverage per skinned mesh, and consolidated self-hosted run #726 proved that gate through Blender. Body-relative fit/poke-through and seam quality still remain.

### Weapon

```text
generate rigid mesh OR named generated detail
  -> optional rigid composition
  -> optional axis/length/grip canonicalization
  -> rigid-multiview OR preserve-generator
  -> rigid contract + finite-bounds/no-armature verifier
  -> lookdev preview
```

Weapon production still needs automatic grip/axis inference and visual seam/coverage gates; explicit canonicalization and reusable generated-detail composition are supported when intended dimensions/anchor are known.

### Accessory

The rigid accessory path shares the same optional canonical-axis/target-length/mount-anchor and generated-detail composition contracts plus socket metadata from `runtimePart`. Two-view or single-view accessories normally use `preserve-generator` until a complete multiview set exists.

## Phase 1 — Generic production orchestration

- [x] Add `character_factory.py produce <spec>`.
- [x] Add recursive `produce-batch <directory>` discovery.
- [x] Route standard verification and preview behavior by `assetType`.
- [x] Preserve the existing `build` command as the low-level generator/preparation primitive.
- [x] Record production-stage decisions and commands in `manifest.json`.
- [x] Restore the prepared character FBX if character appearance projection fails, instead of losing the successful geometry/rig result.
- [x] Run focused production-contract CI; run #1 (`32051087040`) passed compile, routing tests, all four asset-type dry runs, and recursive discovery.
- [x] Run the self-hosted Blender Character Factory integration gate on the generation-framework branch; consolidated run #726 (`32074947199`) passed contract + Blender smoke on `Jasons-MacBook-Pro`.

## Phase 2 — Reference-set contract

- [ ] Complete reusable reference ingestion with deterministic normalization/re-encoding. PNG/JPEG header and dimension preflight is implemented; normalization/re-encoding remains.
- [x] Support canonical `front/back/left/right` discovery from a reference directory, with explicit per-view overrides.
- [x] Support optional named detail references such as `face`, `hands`, `ornament`, `material`, or `fit` without hard-coding character names.
- [x] Separate **geometry references** from **appearance references** so preprocessing for reconstruction does not destroy texture/identity information; appearance falls back to geometry when omitted.
- [x] Produce a `reference-audit.json` in every non-dry-run production artifact and record resolved reference paths in `manifest.json`.
- [x] Reject missing/ambiguous canonical views and unsupported/invalid image headers before expensive generation starts.
- [x] Run expanded reference-contract CI; run #8 (`32051547185`) passed the reference tests, all four production dry runs, and recursive discovery.

## Phase 3 — Backend and rig profiles

- [x] Add named generator profiles `hunyuan-quality-macos`, `hunyuan-smoke-macos`, and `triposr-smoke-macos`.
- [x] Move cache roots, pinned source revisions, Python environments, model downloads, and bootstrap checks into generic profile/bootstrap code instead of character/weapon production scripts.
- [x] Allow a production asset to request a profile plus only asset-specific overrides such as seed/resolution; reject profile-owned machine-field overrides.
- [x] Keep manifests explicit about selected profile, resolved backend/model parameters, pinned source revision, and bootstrap command.
- [x] Add `profiles` discovery and `bootstrap-profile <name>` CLI commands.
- [x] Add automatic ready-state detection so already-materialized profile environments do not rerun expensive bootstrap work.
- [x] Add `canonical-humanoid-macos` so characters/clothing no longer embed machine-local canonical donor paths.
- [x] Key the canonical donor by donor-generator code hash and bypass rig bootstrap on prepared-geometry cache hits.
- [x] Add `rig-profiles` discovery and `bootstrap-rig-profile <name>` CLI commands.
- [x] Validate backend + rig profile contracts on macOS; consolidated run #726 (`32074947199`) passed all contract tests before the Blender smoke.

## Phase 4 — Appearance profiles

- [x] Add a common appearance-strategy interface selected by `asset.json` and recorded in both low-level and production manifests.
- [x] Reject incompatible asset-type/strategy combinations and incomplete multiview sets before expensive generation.
- [x] `preserve-generator`: retain generated UV/material output when the backend already supplies useful appearance.
- [x] `garment-multiview`: add a separate clothing route and projection policy with no character/T-pose outer-arm heuristic.
- [ ] Strengthen `garment-multiview` with body-relative fit, depth/occlusion, seam handling, and production visual gates.
- [x] `rigid-multiview`: add a separate weapon/accessory route with rigid-FBX validation and object-surface view selection.
- [x] Add rigid-specific foreground selection that preserves substantial disconnected components while rejecting isolated speckles; pure regression coverage passed in run #52 (`32063774082`).
- [x] Prove multipart rigid masking and character/garment/rigid projection through Blender; consolidated run #726 (`32074947199`) passed the shared self-hosted smoke.
- [ ] Strengthen `rigid-multiview` further with seam handling and view/orientation quality gates.
- [ ] `character-multiview`: finish the current Madeline projection repair with bounded/visibility-aware sampling and production visual gates.
- [x] Exercise all four strategies in focused CI; run #42 (`32063213794`) passed compile, appearance/backend/reference/routing tests, all four per-asset dry runs, and recursive batch production.

## Phase 5 — Type-specific validation

- [ ] Character: projection quality, skeleton, weights, animation deformation, identity proof.
- [ ] Clothing: skeleton compatibility, deformation, body fit/poke-through, hidden-body-region metadata, seam quality.
- [x] Add and prove a 99% minimum per-mesh skin-weight coverage gate for character/clothing; consolidated run #726 (`32074947199`) passed it in Blender.
- [x] Weapon/accessory: mesh present, no unexpected armature, finite/non-degenerate bounds.
- [x] Add generic rigid canonical-axis/physical-length/grip-or-mount-anchor config plus fast spec/command tests; framework run #63 (`32064307955`) remained green with the contract.
- [x] Prove rigid canonicalization and contract verification through FBX round-trip in Blender; consolidated run #726 (`32074947199`) passed weapon and accessory composition/canonicalization fixtures.
- [ ] Weapon: automatically infer grip axis/location and plausible scale when not explicitly declared.
- [ ] Accessory: automatically infer a plausible local mount transform when not explicitly declared.
- [ ] Prevent Unity staging when the production profile fails.

## Phase 6 — Migrate existing assets

- [ ] Finish Madeline projection repair on `agent/madeline-projection-repair` first.
- [ ] Move Madeline reference normalization, body-only preprocessing, and face identity into reusable/configurable stages. Her generator environment and reference declaration are profile/contract-driven now, but cleanup/face operations remain bespoke.
- [ ] Replace `production/madeline/build.sh` with an `asset.json` plus only genuinely asset-specific preprocessing configuration.
- [x] Migrate the Sunlit Cleric character build to `produce` using `hunyuan-quality-macos` + `canonical-humanoid-macos`; the script no longer creates or embeds a canonical donor.
- [ ] Migrate the Cleric robe completely to data-driven production. It now uses the generic T-pose garment preprocessor, `garment-multiview`, and `canonical-humanoid-macos`; the remaining shell orchestration should move into a declared preprocessing stage and a real Hunyuan production proof is still required.
- [ ] Migrate the Sun Staff completely to data-driven production. Generator environment, named-detail reconstruction, shaft composition, canonicalization, verification, and staging are generic now; the remaining asset-local source isolation runs before `BuildSpec` and should become a declared preprocessing stage.
- [ ] Migrate the sun charm/accessories.

## Phase 7 — Scale to many assets

- [x] Build a production asset catalogue/index with type+ID keys, spec/reference SHA-256 fingerprints, generator profile/backend, appearance strategy, runtime slot/socket, rigid canonicalization metadata, normalized tags, and last-known artifact/cache state.
- [x] Detect duplicate `assetType:id` identities while indexing.
- [x] Support filtered `produce-batch` by repeated `--type`, `--id`, and `--tag`; tags use AND semantics.
- [x] Classify incremental input changes as `new`, `spec`, `geometry`, `appearance`, or `details`; run #73 (`32065061128`) proved no-change, spec-only, and appearance-only batch selection.
- [x] Track last-known final FBX/proof SHA-256s, production status, and geometry-cache fingerprint/hit in catalogue entries when manifests exist.
- [x] Cache prepared geometry independently from appearance/detail work. The fingerprint includes geometry-reference bytes, generator/profile/revision/command, canonical rig revision, preparation command/code, and alignment configuration while excluding unrelated appearance/detail bytes.
- [x] Restore prepared geometry before backend/rig bootstrap so appearance/detail-only work can bypass Hunyuan/TripoSR and canonical-donor materialization entirely.
- [x] Atomically write the next catalogue snapshot after successful/no-change `produce-batch` runs, enabling previous-snapshot -> changed build -> next-snapshot operation.
- [ ] Cache/reuse appearance and verification stages independently; today changed appearance/details reuse geometry but still rerun downstream production checks.
- [x] Add Blender smoke fixtures for character, garment, weapon, and accessory; consolidated run #726 (`32074947199`) passed all four on the self-hosted Mac.
- [x] Add one generic manual `character-factory-production.yml` workflow that selects an asset library by type/ID/tag/previous catalogue/change kind, optionally stages Unity assets, writes the next catalogue, and uploads artifacts/proofs.
- [ ] Run the generic production workflow end-to-end on a real production asset on the self-hosted Mac and inspect its published proof before treating it as production-accepted.

## Current status

The framework now has a generic asset production runner, separate geometry/appearance/detail references, named backend profiles, a code-versioned canonical rig profile, explicit character/garment/rigid appearance strategies, reusable generated-detail rigid composition, rigid multipart-reference handling, optional rigid axis/length/grip-or-mount canonicalization, generic T-pose garment and linear-terminal preprocessing tools, a fingerprinted catalogue, changed-only/tag-filtered batch selection, and a persistent prepared-geometry cache. Consolidated self-hosted run #726 (`32074947199`) is green at commit `ee42b6363cedc0633cb4cc55c2f58fdf05715bf0`: contract tests and the full Blender smoke both passed on `Jasons-MacBook-Pro`.

The highest-value remaining framework seam is **declared preprocessing in `asset.json`** so source-specific/reference-derived preparation can run before normal `BuildSpec` validation without named shell wrappers. After that, the major remaining work is production/art quality: finish Madeline visibility-aware projection and configurable face/identity stages, add garment body-fit/occlusion/seam gates, add rigid seam/orientation quality, run the generic production workflow end-to-end on a real asset, and finish migrating the Cleric robe/Sun Staff/Sun Charm into the production asset library.
