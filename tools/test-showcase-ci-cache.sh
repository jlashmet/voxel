#!/usr/bin/env bash
set -euo pipefail

REPOSITORY_ROOT="$(git rev-parse --show-toplevel)"
FIXTURE_ROOT="$(mktemp -d /tmp/voxel-showcase-cache-test.XXXXXX)"
cleanup() {
  rm -rf "$FIXTURE_ROOT"
}
trap cleanup EXIT

mkdir -p \
  "$FIXTURE_ROOT/.github" \
  "$FIXTURE_ROOT/ProjectSettings" \
  "$FIXTURE_ROOT/Packages" \
  "$FIXTURE_ROOT/Assets/Scenes" \
  "$FIXTURE_ROOT/Assets/Game/Composition/Showcase" \
  "$FIXTURE_ROOT/Assets/VoxelEngine/Rendering/Runtime/Shaders" \
  "$FIXTURE_ROOT/Assets/Tests"
cp "$REPOSITORY_ROOT/.github/showcase-bake-inputs.txt" "$FIXTURE_ROOT/.github/"
printf 'm_EditorVersion: fixture\n' > "$FIXTURE_ROOT/ProjectSettings/ProjectVersion.txt"
printf '{}\n' > "$FIXTURE_ROOT/Packages/manifest.json"
printf '{}\n' > "$FIXTURE_ROOT/Packages/packages-lock.json"
printf 'scene\n' > "$FIXTURE_ROOT/Assets/Scenes/VoxelShowcase.unity"
printf 'semantic-v1\n' > "$FIXTURE_ROOT/Assets/Game/Composition/Showcase/Semantic.cs"
printf 'render-v1\n' > "$FIXTURE_ROOT/Assets/VoxelEngine/Rendering/Runtime/Shaders/Fake.shader"
printf 'test-v1\n' > "$FIXTURE_ROOT/Assets/Tests/FakeTest.cs"

git -C "$FIXTURE_ROOT" init -q
git -C "$FIXTURE_ROOT" config user.name fixture
git -C "$FIXTURE_ROOT" config user.email fixture@example.invalid
git -C "$FIXTURE_ROOT" add .
git -C "$FIXTURE_ROOT" commit -qm baseline

cd "$FIXTURE_ROOT"
export VOXEL_SHOWCASE_BAKE_INPUTS="$FIXTURE_ROOT/.github/showcase-bake-inputs.txt"
export VOXEL_SHOWCASE_BAKE_CACHE="$FIXTURE_ROOT/bake-cache"
export VOXEL_SHOWCASE_PLAYER_CACHE="$FIXTURE_ROOT/player-cache"

bake_before="$("$REPOSITORY_ROOT/tools/showcase-bake-cache.sh" fingerprint)"
printf 'render-v2\n' > Assets/VoxelEngine/Rendering/Runtime/Shaders/Fake.shader
git add Assets/VoxelEngine/Rendering/Runtime/Shaders/Fake.shader
git commit -qm rendering
bake_after_render="$("$REPOSITORY_ROOT/tools/showcase-bake-cache.sh" fingerprint)"
[[ "$bake_before" == "$bake_after_render" ]] || {
  echo "ERROR: presentation-only rendering invalidated the semantic bake key" >&2
  exit 1
}

printf 'semantic-v2\n' > Assets/Game/Composition/Showcase/Semantic.cs
git add Assets/Game/Composition/Showcase/Semantic.cs
git commit -qm semantic
bake_after_semantic="$("$REPOSITORY_ROOT/tools/showcase-bake-cache.sh" fingerprint)"
[[ "$bake_before" != "$bake_after_semantic" ]] || {
  echo "ERROR: authoritative semantic input did not invalidate the bake key" >&2
  exit 1
}

printf 'bake-v1\n' > ShowcaseWorld.bytes
"$REPOSITORY_ROOT/tools/showcase-bake-cache.sh" store ShowcaseWorld.bytes
rm ShowcaseWorld.bytes
"$REPOSITORY_ROOT/tools/showcase-bake-cache.sh" restore ShowcaseWorld.bytes
grep -q '^bake-v1$' ShowcaseWorld.bytes

player_before="$("$REPOSITORY_ROOT/tools/showcase-player-cache.sh" \
  fingerprint Assets/Scenes/VoxelShowcase.unity development ShowcaseWorld.bytes)"
printf 'render-v3\n' > Assets/VoxelEngine/Rendering/Runtime/Shaders/Fake.shader
git add Assets/VoxelEngine/Rendering/Runtime/Shaders/Fake.shader
git commit -qm player-rendering
player_after_render="$("$REPOSITORY_ROOT/tools/showcase-player-cache.sh" \
  fingerprint Assets/Scenes/VoxelShowcase.unity development ShowcaseWorld.bytes)"
[[ "$player_before" != "$player_after_render" ]] || {
  echo "ERROR: runtime rendering change did not invalidate the player key" >&2
  exit 1
}

printf 'bake-v2\n' > ShowcaseWorld.bytes
player_after_bake="$("$REPOSITORY_ROOT/tools/showcase-player-cache.sh" \
  fingerprint Assets/Scenes/VoxelShowcase.unity development ShowcaseWorld.bytes)"
[[ "$player_after_render" != "$player_after_bake" ]] || {
  echo "ERROR: generated bake bytes did not invalidate the player key" >&2
  exit 1
}

mkdir -p Build/Fake.app/Contents/MacOS
printf '#!/usr/bin/env bash\nexit 0\n' > Build/Fake.app/Contents/MacOS/fake
chmod +x Build/Fake.app/Contents/MacOS/fake
"$REPOSITORY_ROOT/tools/showcase-player-cache.sh" store "$player_after_bake" Build
"$REPOSITORY_ROOT/tools/showcase-player-cache.sh" restore "$player_after_bake" Restored
test -x Restored/Fake.app/Contents/MacOS/fake

printf 'Showcase CI cache contracts passed.\n'
