from pathlib import Path


def replace_exact(path: str, old: str, new: str, expected: int = 1) -> None:
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected} occurrences, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new))


WORLD = "Assets/Scenes/Showcase/ShowcaseWorld.cs"
SHOWCASE = "Assets/Scenes/Showcase/VoxelShowcase.cs"
ARCH = "Assets/Scenes/Showcase/ArchLookdev.cs"
TERRAIN = "Assets/Scenes/Showcase/TerrainLookdev.cs"
TREES = "Assets/Scenes/Showcase/ShowcaseTreePopulation.cs"

replace_exact(
    WORLD,
    "        private RegionTable _table;\n        private BrickPool _pool;\n",
    "        private RegionTable _table;\n        private BrickPool _pool;\n        private readonly RegionReadSource _readSource;\n",
)
replace_exact(
    WORLD,
    "        public ref RegionTable Table => ref _table;\n        public ref BrickPool Pool => ref _pool;\n",
    "        public ref RegionTable Table => ref _table;\n        public ref BrickPool Pool => ref _pool;\n        public IRegionReadSource ReadStorage\n        {\n            get\n            {\n                _readSource.Refresh(in _table, in _pool);\n                return _readSource;\n            }\n        }\n",
)
replace_exact(
    WORLD,
    "            _table = new RegionTable(64, Allocator.Persistent);\n            _pool = new BrickPool(brickPoolCapacity, Allocator.Persistent);\n",
    "            _table = new RegionTable(64, Allocator.Persistent);\n            _pool = new BrickPool(brickPoolCapacity, Allocator.Persistent);\n            _readSource = new RegionReadSource(in _table, in _pool, _changes);\n",
)

replace_exact(
    SHOWCASE,
    "                Table = _world.Table,\n                Pool = _world.Pool,\n",
    "                Storage = _world.ReadStorage,\n",
)

replace_exact(
    ARCH,
    "        private RegionTable _table;\n        private BrickPool _pool;\n",
    "        private RegionTable _table;\n        private BrickPool _pool;\n        private RegionReadSource _readSource;\n",
)
replace_exact(
    ARCH,
    "        private VoxelWorldView WorldView() => new()\n        {\n            Table = _table, Pool = _pool, Palette = _palette,\n            SurfaceCatalogue = _surfaces, CoatingCatalogue = _coatings,\n            ProfileBlocks = _profileBlocks,\n        };",
    "        private VoxelWorldView WorldView()\n        {\n            _readSource ??= new RegionReadSource(in _table, in _pool, _changes);\n            _readSource.Refresh(in _table, in _pool);\n            return new VoxelWorldView\n            {\n                Storage = _readSource, Palette = _palette,\n                SurfaceCatalogue = _surfaces, CoatingCatalogue = _coatings,\n                ProfileBlocks = _profileBlocks,\n            };\n        }",
)
replace_exact(
    ARCH,
    "            _table = default;\n            _pool = default;\n",
    "            _table = default;\n            _pool = default;\n            _readSource = null;\n",
)

replace_exact(
    TERRAIN,
    "        private RegionTable _table;\n        private BrickPool _pool;\n",
    "        private RegionTable _table;\n        private BrickPool _pool;\n        private RegionReadSource _readSource;\n",
)
replace_exact(
    TERRAIN,
    "        private VoxelWorldView WorldView() => new()\n        {\n            Table = _table,\n            Pool = _pool,\n            Palette = _palette,\n            SurfaceCatalogue = _surfaces,\n            CoatingCatalogue = _coatings,\n            ProfileBlocks = _profiles,\n        };",
    "        private VoxelWorldView WorldView()\n        {\n            _readSource ??= new RegionReadSource(in _table, in _pool, _changes);\n            _readSource.Refresh(in _table, in _pool);\n            return new VoxelWorldView\n            {\n                Storage = _readSource,\n                Palette = _palette,\n                SurfaceCatalogue = _surfaces,\n                CoatingCatalogue = _coatings,\n                ProfileBlocks = _profiles,\n            };\n        }",
)
replace_exact(
    TERRAIN,
    "            _table = default;\n            _pool = default;\n            _built = false;",
    "            _table = default;\n            _pool = default;\n            _readSource = null;\n            _built = false;",
)

replace_exact(
    TREES,
    "            if (!CastleVegetationPlanner.TryBuild(\n                    in plan, ref view.Table, in view.Pool, worldSeed, out var instances))",
    "            if (!CastleVegetationPlanner.TryBuild(\n                    in plan, view.Storage, worldSeed, out var instances))",
)

# VoxelWorldView producers/consumers must no longer reach through physical storage fields.
for path in (SHOWCASE, ARCH, TERRAIN, TREES):
    text = Path(path).read_text()
    for token in ("view.Table", "view.Pool", "Table = _world.Table", "Pool = _world.Pool", "Table = _table", "Pool = _pool"):
        if token in text:
            raise RuntimeError(f"{path}: stale VoxelWorldView physical storage access remains: {token}")

print("Showcase render Storage cutover applied successfully.")
