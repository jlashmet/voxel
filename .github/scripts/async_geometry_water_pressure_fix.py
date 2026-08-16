from pathlib import Path

path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuWaterSurfaceChunkCache.cs')
s = path.read_text()
old = '''            if (farthest < 0f) return false;
            RemoveWaterChunk(victim);
            return true;
        }
'''
new = '''            if (farthest < 0f) return false;
            if (!_entries.TryGetValue(victim, out Entry entry)) return false;

            // Arena pressure is publication backpressure, not authoritative water eviction.
            // Keep the discovered brick set + residency record so the chunk is rebuilt later.
            _entries.Remove(victim);
            ReleaseEntry(entry);
            MarkDirty(victim);
            return true;
        }
'''
if s.count(old) != 1:
    raise SystemExit(f'pressure eviction anchor expected once, found {s.count(old)}')
s = s.replace(old, new, 1)
path.write_text(s)

# Strengthen the architecture guard so this cannot regress into forgetting water topology.
test = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test.read_text()
needle = '''            StringAssert.DoesNotContain("List<int3> gone", water);
        }
'''
replacement = '''            StringAssert.DoesNotContain("List<int3> gone", water);
            int pressure = water.IndexOf("TryEvictOneForArenaPressure", StringComparison.Ordinal);
            int pressureEnd = water.IndexOf("public void Dispose()", pressure,
                                            StringComparison.Ordinal);
            Assert.GreaterOrEqual(pressure, 0);
            Assert.Greater(pressureEnd, pressure);
            string pressurePath = water.Substring(pressure, pressureEnd - pressure);
            StringAssert.Contains("MarkDirty(victim)", pressurePath);
            StringAssert.DoesNotContain("RemoveWaterChunk(victim)", pressurePath);
        }
'''
if t.count(needle) != 1:
    raise SystemExit('water maintenance test anchor missing')
t = t.replace(needle, replacement, 1)
test.write_text(t)

assert 'RemoveWaterChunk(victim);' not in path.read_text()[path.read_text().index('TryEvictOneForArenaPressure'):path.read_text().index('public void Dispose()')]
assert 'MarkDirty(victim);' in path.read_text()[path.read_text().index('TryEvictOneForArenaPressure'):path.read_text().index('public void Dispose()')]
