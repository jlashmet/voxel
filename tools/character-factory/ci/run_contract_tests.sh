#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

python3 -m compileall -q tools/character-factory

bash -n tools/character-factory/production/madeline/build.sh
bash -n tools/character-factory/production/madeline/bootstrap_hunyuan_quality_macos.sh
bash -n tools/character-factory/production/sunlit-cleric/build_macos.sh
bash -n tools/character-factory/production/sunlit-cleric/build_robe_macos.sh
bash -n tools/character-factory/production/sunlit-cleric/build_staff_macos.sh
bash -n tools/character-factory/ci/bootstrap_hunyuan_quality_macos.sh
bash -n tools/character-factory/ci/run_character_weapon_e2e_macos.sh
bash -n tools/character-factory/new_asset_macos.sh

python3 -m unittest \
  tools/character-factory/tests/test_pipeline_routing.py \
  tools/character-factory/tests/test_generator_backends.py \
  tools/character-factory/tests/test_production_routing.py \
  tools/character-factory/tests/test_reference_contract.py \
  tools/character-factory/tests/test_backend_profiles.py \
  tools/character-factory/tests/test_rig_profiles.py \
  tools/character-factory/tests/test_preprocess.py \
  tools/character-factory/tests/test_preprocess_audit.py \
  tools/character-factory/tests/test_appearance_profiles.py \
  tools/character-factory/tests/test_projection_components.py \
  tools/character-factory/tests/test_rigid_contract.py \
  tools/character-factory/tests/test_catalogue.py \
  tools/character-factory/tests/test_catalogue_tags.py \
  tools/character-factory/tests/test_geometry_cache.py \
  tools/character-factory/tests/test_scaffold.py \
  tools/character-factory/tests/test_unity_staging.py \
  tools/character-factory/tests/test_character_alignment.py \
  -v

python3 tools/character-factory/character_factory.py profiles | tee /tmp/character-factory-profiles.txt
grep -q '^hunyuan-quality-macos' /tmp/character-factory-profiles.txt
grep -q '^hunyuan-smoke-macos' /tmp/character-factory-profiles.txt
grep -q '^triposr-smoke-macos' /tmp/character-factory-profiles.txt

python3 tools/character-factory/character_factory.py rig-profiles | tee /tmp/character-factory-rig-profiles.txt
grep -q '^canonical-humanoid-macos' /tmp/character-factory-rig-profiles.txt
grep -q 'canonical_female_with_garment_donor.glb' /tmp/character-factory-rig-profiles.txt

for spec in \
  tools/character-factory/examples/cleric_character.json \
  tools/character-factory/examples/cleric_robe.json \
  tools/character-factory/examples/cleric_staff.json \
  tools/character-factory/examples/cleric_sun_charm.json; do
  python3 tools/character-factory/character_factory.py produce "$spec" --dry-run
done

CATALOGUE="${TMPDIR:-/tmp}/character-factory-contract-catalogue.json"
python3 tools/character-factory/character_factory.py catalogue \
  tools/character-factory/examples --output "$CATALOGUE"
python3 - "$CATALOGUE" <<'PY'
import json
from pathlib import Path
import sys
payload = json.loads(Path(sys.argv[1]).read_text())
assert payload["assetCount"] == 4, payload
assert payload["typeCounts"] == {
    "character": 1,
    "clothing": 1,
    "weapon": 1,
    "accessory": 1,
}, payload["typeCounts"]
PY

python3 tools/character-factory/character_factory.py produce-batch \
  tools/character-factory/examples --dry-run --type weapon --id cleric_staff_01

echo "CHARACTER_FACTORY_CONTRACT_OK"
