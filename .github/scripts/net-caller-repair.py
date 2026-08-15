from pathlib import Path
import re


def write_if_changed(path, text, original):
    if text != original:
        path.write_text(text)
        print(f"updated {path}")


total_drain = total_apply = total_server = total_processor = 0
for path in Path("Assets/Tests/EditMode").glob("*.cs"):
    original = path.read_text()
    text = original

    def drain(m):
        global total_drain
        total_drain += 1
        target, table, pool = m.group(1), m.group(2), m.group(3)
        return (f"{target}(new RegionMutationStore(in {table}, in {pool}), "
                f"new RegionReadSource(in {table}, in {pool}), out ")
    text = re.sub(
        r"(\b[A-Za-z_][\w.]*\.DrainReady)\(\s*ref ([A-Za-z_]\w*),\s*ref ([A-Za-z_]\w*),\s*out ",
        drain, text)

    def apply_ready(m):
        global total_apply
        total_apply += 1
        target, table, pool = m.group(1), m.group(2), m.group(3)
        return (f"{target}(new RegionMutationStore(in {table}, in {pool}), "
                f"new RegionReadSource(in {table}, in {pool}), "
                f"new RegionSnapshotMutationStore(in {table}, in {pool}), out ")
    text = re.sub(
        r"(\b[A-Za-z_][\w.]*\.ApplyReadyAuthoritativeEvents)\(\s*ref ([A-Za-z_]\w*),\s*ref ([A-Za-z_]\w*),\s*out ",
        apply_ready, text)

    def server_tick(m):
        global total_server
        total_server += 1
        prefix, table, pool, zones = m.group(1), m.group(2), m.group(3), m.group(4)
        return (f"{prefix}new RegionReadSource(in {table}, in {pool}), "
                f"new RegionMutationStore(in {table}, in {pool}), "
                f"new RegionReadSource(in {table}, in {pool}), in {zones},")
    text = re.sub(
        r"(\b[A-Za-z_][\w.]*\.ProcessAuthoritativeTick\(\s*[^,\n]+,\s*)ref ([A-Za-z_]\w*),\s*ref ([A-Za-z_]\w*),\s*in ([A-Za-z_]\w*),",
        server_tick, text)

    def processor_tick(m):
        global total_processor
        total_processor += 1
        prefix, table, pool, zones = m.group(1), m.group(2), m.group(3), m.group(4)
        return (f"{prefix}new RegionReadSource(in {table}, in {pool}), "
                f"new RegionMutationStore(in {table}, in {pool}), in {zones},")
    text = re.sub(
        r"(\b[A-Za-z_][\w.]*\.ProcessTick\(\s*[^,\n]+,\s*)ref ([A-Za-z_]\w*),\s*ref ([A-Za-z_]\w*),\s*in ([A-Za-z_]\w*),",
        processor_tick, text)

    if path.name == "CanonicalBrushValidationTests.cs":
        old = """                    players,
                    mutationStorage,
                    new DeterministicAlterationApplier(),
                    ref table,
                    in pool,
                    new Validation.DensityCap(1f, 0));"""
        new = """                    players,
                    new RegionReadSource(in table, in pool),
                    mutationStorage,
                    new DeterministicAlterationApplier(),
                    new Validation.DensityCap(1f, 0));"""
        if old not in text:
            raise SystemExit("CanonicalBrushValidationTests expected legacy validator call not found")
        text = text.replace(old, new, 1)

    write_if_changed(path, text, original)

if total_drain != 6:
    raise SystemExit(f"expected 6 legacy DrainReady calls, found {total_drain}")
if total_apply != 9:
    raise SystemExit(f"expected 9 legacy ApplyReadyAuthoritativeEvents calls, found {total_apply}")
if total_server != 10:
    raise SystemExit(f"expected 10 legacy ProcessAuthoritativeTick calls, found {total_server}")
if total_processor != 1:
    raise SystemExit(f"expected 1 legacy ProcessTick call, found {total_processor}")

world = Path("Assets/Scenes/Showcase/ShowcaseWorld.cs")
original = world.read_text()
text = original
old = """        private readonly RegionReadSource _readSource;
        private readonly RegionMutationStore _mutationStore;
        private readonly RegionResidencyStore _residencyStore;"""
new = """        private readonly RegionReadSource _readSource;
        private readonly RegionMutationStore _mutationStore;
        private readonly RegionSnapshotMutationStore _snapshotMutationStore;
        private readonly RegionResidencyStore _residencyStore;"""
if old not in text:
    raise SystemExit("ShowcaseWorld storage field seam not found")
text = text.replace(old, new, 1)
old = """            _readSource = new RegionReadSource(in _table, in _pool, _changes);
            _mutationStore = new RegionMutationStore(in _table, in _pool);
            _residencyStore = new RegionResidencyStore(in _table, in _pool);"""
new = """            _readSource = new RegionReadSource(in _table, in _pool, _changes);
            _mutationStore = new RegionMutationStore(in _table, in _pool);
            _snapshotMutationStore = new RegionSnapshotMutationStore(in _table, in _pool);
            _residencyStore = new RegionResidencyStore(in _table, in _pool);"""
if old not in text:
    raise SystemExit("ShowcaseWorld storage constructor seam not found")
text = text.replace(old, new, 1)
old = "        public MaterialPalette Palette => _palette;"
new = """        public IRegionMutationStore MutationStorage
        {
            get
            {
                _mutationStore.Refresh(in _table, in _pool);
                return _mutationStore;
            }
        }
        public IRegionSnapshotSource SnapshotStorage
        {
            get
            {
                _readSource.Refresh(in _table, in _pool);
                return _readSource;
            }
        }
        public IRegionSnapshotMutationStore SnapshotMutationStorage
        {
            get
            {
                _snapshotMutationStore.Refresh(in _table, in _pool);
                return _snapshotMutationStore;
            }
        }
        public MaterialPalette Palette => _palette;"""
if old not in text:
    raise SystemExit("ShowcaseWorld API property insertion seam not found")
text = text.replace(old, new, 1)
write_if_changed(world, text, original)

session = Path("Assets/Scenes/Showcase/ShowcaseMultiplayerSession.cs")
original = session.read_text()
text = original
old = """            _server.ProcessAuthoritativeTick(
                _serverTick,
                ref _world.Table,
                ref _world.Pool,
                in zones,
                this);"""
new = """            _server.ProcessAuthoritativeTick(
                _serverTick,
                _world.ReadStorage,
                _world.MutationStorage,
                _world.SnapshotStorage,
                in zones,
                this);"""
if old not in text:
    raise SystemExit("Showcase server tick seam not found")
text = text.replace(old, new, 1)
old = """            _client.ApplyReadyAuthoritativeEvents(
                ref _world.Table,
                ref _world.Pool,
                out int appliedEvents);"""
new = """            _client.ApplyReadyAuthoritativeEvents(
                _world.MutationStorage,
                _world.SnapshotStorage,
                _world.SnapshotMutationStorage,
                out int appliedEvents);"""
if old not in text:
    raise SystemExit("Showcase client apply seam not found")
text = text.replace(old, new, 1)
write_if_changed(session, text, original)

roots = [Path("Assets/Tests/EditMode"), Path("Assets/Scenes/Showcase")]
forbidden = [
    re.compile(r"\.DrainReady\(\s*ref "),
    re.compile(r"\.ApplyReadyAuthoritativeEvents\(\s*ref "),
    re.compile(r"\.ProcessAuthoritativeTick\(\s*[^,\n]+,\s*ref "),
]
for root in roots:
    for path in root.glob("*.cs"):
        body = path.read_text()
        for pattern in forbidden:
            if pattern.search(body):
                raise SystemExit(f"legacy physical Net call remains in {path}: {pattern.pattern}")
