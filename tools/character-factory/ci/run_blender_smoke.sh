#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
test -x "$BLENDER_BIN"
OUT="${CHARACTER_FACTORY_SMOKE_OUT:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}/character-factory-blender-smoke}"
rm -rf "$OUT"
mkdir -p "$OUT"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_canonical_character_fixture.py -- \
  --canonical "$OUT/canonical.glb" \
  --input "$OUT/body.png" \
  --garment-input "$OUT/garment.png"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_appearance_strategy_fixtures.py -- \
  --canonical "$OUT/canonical.glb" \
  --character "$OUT/character.fbx" \
  --garment "$OUT/garment.fbx" \
  --rigid "$OUT/rigid-basic.fbx"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_rigid_canonicalization_fixture.py -- \
  --output "$OUT/rigid-raw.glb"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_prepare_rigid_part.py -- \
  --input "$OUT/rigid-raw.glb" \
  --output "$OUT/rigid.fbx" \
  --part-kind weapon \
  --canonical-axis z \
  --target-length 1.5 \
  --anchor-fraction 0.5 0.5 0.1

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_rigid_asset.py -- \
  --input "$OUT/rigid.fbx"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_rigid_reference_fixture.py -- \
  --output "$OUT/rigid-multipart.png"

for profile in character garment rigid; do
  mkdir -p "$OUT/$profile-refs"
done
for view in front back left right; do
  cp "$OUT/body.png" "$OUT/character-refs/$view.png"
  cp "$OUT/garment.png" "$OUT/garment-refs/$view.png"
  cp "$OUT/rigid-multipart.png" "$OUT/rigid-refs/$view.png"
done

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_project_multiview_asset.py -- \
  --input "$OUT/character.fbx" \
  --output "$OUT/character.projected.fbx" \
  --front "$OUT/character-refs/front.png" \
  --back "$OUT/character-refs/back.png" \
  --left "$OUT/character-refs/left.png" \
  --right "$OUT/character-refs/right.png" \
  --atlas "$OUT/character.atlas.png" \
  --profile character
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_skinned_character.py -- \
  --input "$OUT/character.projected.fbx"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_project_multiview_asset.py -- \
  --input "$OUT/garment.fbx" \
  --output "$OUT/garment.projected.fbx" \
  --front "$OUT/garment-refs/front.png" \
  --back "$OUT/garment-refs/back.png" \
  --left "$OUT/garment-refs/left.png" \
  --right "$OUT/garment-refs/right.png" \
  --atlas "$OUT/garment.atlas.png" \
  --profile garment
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_skinned_character.py -- \
  --input "$OUT/garment.projected.fbx"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_rigid_reference_mask.py -- \
  --input "$OUT/rigid-refs/front.png"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_project_multiview_asset.py -- \
  --input "$OUT/rigid.fbx" \
  --output "$OUT/rigid.projected.fbx" \
  --front "$OUT/rigid-refs/front.png" \
  --back "$OUT/rigid-refs/back.png" \
  --left "$OUT/rigid-refs/left.png" \
  --right "$OUT/rigid-refs/right.png" \
  --atlas "$OUT/rigid.atlas.png" \
  --profile rigid
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_rigid_asset.py -- \
  --input "$OUT/rigid.projected.fbx"

# New reusable rigid-composition gate. The generated-detail stage receives an
# intentionally X-long detail mesh, constructs the shaft, then must canonicalize
# the final asset to Z with an exact physical length and a grip anchor near one end.
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_rigid_canonicalization_fixture.py -- \
  --output "$OUT/generated-detail.glb"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_compose_generated_detail_shaft.py -- \
  --input-detail "$OUT/generated-detail.glb" \
  --output "$OUT/composed-staff.fbx" \
  --part-kind weapon \
  --total-length 1.8 \
  --detail-length 0.38 \
  --shaft-radius 0.024 \
  --axis auto \
  --attachment-side min \
  --overlap 0.025 \
  --canonical-axis z \
  --target-length 1.8 \
  --anchor-fraction 0.5 0.5 0.1
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_rigid_asset.py -- \
  --input "$OUT/composed-staff.fbx"

test -s "$OUT/composed-staff.rigid-contract.json"
test -s "$OUT/character.atlas.png"
test -s "$OUT/garment.atlas.png"
test -s "$OUT/rigid.atlas.png"

echo "CHARACTER_FACTORY_BLENDER_SMOKE_OK out=$OUT"
