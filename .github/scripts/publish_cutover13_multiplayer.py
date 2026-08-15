from pathlib import Path
import subprocess
import textwrap

WORKFLOW = ".github/workflows/cutover13-multiplayer-composition.yml"
SCRIPT = ".github/scripts/publish_cutover13_multiplayer.py"
BRANCH = "refactor/system-boundaries-foundation-storage"


def previous_workflow_text():
    return subprocess.check_output(
        ["git", "show", f"HEAD^:{WORKFLOW}"],
        text=True,
    )


def extract(text, start_marker, end_marker):
    start = text.index(start_marker) + len(start_marker)
    end = text.index(end_marker, start)
    return textwrap.dedent(text[start:end])


def run(command):
    subprocess.run(command, check=True)


source = previous_workflow_text()

composition = extract(
    source,
    "          cat > Assets/VoxelEngine/Composition/NetworkingComposition.cs <<'CS'\n",
    "          CS\n\n          cat > Assets/VoxelEngine/Composition/NetworkingComposition.cs.meta",
)
Path("Assets/VoxelEngine/Composition/NetworkingComposition.cs").write_text(composition)

meta = extract(
    source,
    "          cat > Assets/VoxelEngine/Composition/NetworkingComposition.cs.meta <<'META'\n",
    "          META\n\n          python3 - <<'PY'",
)
Path("Assets/VoxelEngine/Composition/NetworkingComposition.cs.meta").write_text(meta)

transformation = extract(
    source,
    "          python3 - <<'PY'\n",
    "          PY\n\n          rm .github/workflows/cutover13-multiplayer-composition.yml",
)
exec(compile(transformation, "cutover13-multiplayer-transform", "exec"), {"__name__": "__main__"})

# The publisher is one-shot scaffolding. Leave only the architectural cutover in history.
Path(WORKFLOW).unlink()
Path(SCRIPT).unlink()

run(["git", "diff", "--check"])
run(["git", "config", "user.name", "github-actions[bot]"])
run(["git", "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com"])
run([
    "git", "add", "-A",
    "Assets/VoxelEngine/Composition/NetworkingComposition.cs",
    "Assets/VoxelEngine/Composition/NetworkingComposition.cs.meta",
    "Assets/VoxelEngine/Composition/VoxelEngine.Composition.asmdef",
    "Assets/Scenes/Showcase/ShowcaseMultiplayerSession.cs",
    "Assets/Scenes/Showcase/VoxelEngine.Showcase.asmdef",
    WORKFLOW,
    SCRIPT,
])
run(["git", "commit", "-m", "refactor: route showcase networking through composition"])
run(["git", "fetch", "origin", BRANCH])
run(["git", "rebase", f"origin/{BRANCH}"])
run(["git", "push", "origin", f"HEAD:{BRANCH}"])
