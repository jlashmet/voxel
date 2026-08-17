from pathlib import Path

patch = Path(__file__).resolve().parent / "apply-hierarchy-followup.py"
text = patch.read_text()
needle = "        private bool TryRemoveChunk'''"
replacement = "        /// <summary>'''"
count = text.count(needle)
if count != 2:
    raise RuntimeError(f"expected two EnforceCapacity boundary markers, found {count}")
text = text.replace(needle, replacement)
patch.write_text(text)
namespace = {"__file__": str(patch), "__name__": "__main__"}
exec(compile(text, str(patch), "exec"), namespace)
