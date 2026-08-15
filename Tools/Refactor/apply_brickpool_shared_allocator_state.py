from pathlib import Path

path = Path('Assets/VoxelEngine/Core/Storage/BrickPool.cs')
text = path.read_text()

replacements = [
    ('        private NativeList<int> _freeList;\n        private int _highWater;\n',
     '        private NativeList<int> _freeList;\n        // Handle-like allocator state. BrickPool is copied into Storage capability objects, so\n        // scalar allocator bookkeeping must live in shared native memory just like the payloads.\n        private NativeArray<int> _highWaterState;\n\n        private int HighWater\n        {\n            [MethodImpl(MethodImplOptions.AggressiveInlining)]\n            get => _highWaterState.IsCreated ? _highWaterState[0] : 0;\n            [MethodImpl(MethodImplOptions.AggressiveInlining)]\n            set => _highWaterState[0] = value;\n        }\n'),
    ('        public int AllocatedCount => _highWater - _freeList.Length;\n',
     '        public int AllocatedCount => HighWater - _freeList.Length;\n'),
    ('            _freeList = new NativeList<int>(capacity >> 4, allocator);\n            _highWater = 0;\n',
     '            _freeList = new NativeList<int>(capacity >> 4, allocator);\n            _highWaterState = new NativeArray<int>(1, allocator, NativeArrayOptions.ClearMemory);\n'),
    ('                if (_highWater >= Capacity)\n',
     '                int highWater = HighWater;\n                if (highWater >= Capacity)\n'),
    ('                index = _highWater++;\n',
     '                index = highWater;\n                HighWater = highWater + 1;\n'),
    ('            if ((uint)brickIndex >= (uint)_highWater)\n',
     '            if ((uint)brickIndex >= (uint)HighWater)\n'),
    ('            if (_freeList.IsCreated) _freeList.Dispose();\n            Capacity = 0;\n            _highWater = 0;\n',
     '            if (_freeList.IsCreated) _freeList.Dispose();\n            if (_highWaterState.IsCreated) _highWaterState.Dispose();\n            Capacity = 0;\n'),
]

for old, new in replacements:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f'expected exactly one match, found {count}: {old[:100]!r}')
    text = text.replace(old, new)

if '_highWater' in text.replace('_highWaterState', ''):
    raise RuntimeError('stale scalar _highWater reference remains')
if 'NativeArray<int> _highWaterState' not in text:
    raise RuntimeError('shared allocator state was not installed')

path.write_text(text)
print('BrickPool allocator state converted to shared native backing.')
