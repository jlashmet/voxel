from pathlib import Path

path = Path("Assets/Scenes/Showcase/ShowcaseWorld.cs")
text = path.read_text()

replacements = [
    (
        "        private readonly RegionReadSource _readSource;\n",
        "        private readonly RegionReadSource _readSource;\n        private readonly RegionResidencyStore _residencyStore;\n",
    ),
    (
        "            _readSource = new RegionReadSource(in _table, in _pool, _changes);\n",
        "            _readSource = new RegionReadSource(in _table, in _pool, _changes);\n            _residencyStore = new RegionResidencyStore(in _table, in _pool);\n",
    ),
    (
        "        private void EvictDistantRegions(int3 centre)\n        {\n            var resident = _table.GetResidentCoords(Allocator.Temp);\n",
        "        private void EvictDistantRegions(int3 centre)\n        {\n            _residencyStore.Refresh(in _table, in _pool);\n            var resident = _table.GetResidentCoords(Allocator.Temp);\n",
    ),
    (
        "                ResidencyManager.EvictWithoutWriteBack(rc, ref _table, ref _pool);",
        "                ResidencyManager.EvictWithoutWriteBack(rc, _residencyStore);",
    ),
]

for old, new in replacements:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"expected one occurrence, found {count}: {old[:100]!r}")
    text = text.replace(old, new)

if "ResidencyManager.EvictWithoutWriteBack(rc, ref _table, ref _pool)" in text:
    raise RuntimeError("old physical eviction call remains")

path.write_text(text)
print("Showcase residency-store cutover applied successfully.")
