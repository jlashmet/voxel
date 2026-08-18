using System;
using System.Collections.Generic;
using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Streaming-lifecycle owner for generated WorldObject scenes. Persistent sparse state is retained by parent
    /// identity even when the live descriptors/runtime are unloaded and later regenerated deterministically.
    /// </summary>
    public sealed class WorldObjectSceneRegistry
    {
        private readonly Dictionary<uint, Entry> _entries = new Dictionary<uint, Entry>();

        public int LoadedSceneCount
        {
            get
            {
                int count = 0;
                foreach (var pair in _entries)
                    if (pair.Value.Scene != null) count++;
                return count;
            }
        }

        public int PersistentSceneCount => _entries.Count;

        public WorldObjectGeneratedScene LoadCastle(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, in CastlePlan plan,
            WorldObjectGeometryEmissionMode emissionMode = WorldObjectGeometryEmissionMode.AllVoxel)
        {
            Entry entry = GetForLoad(parentId);
            entry.Scene = WorldObjectGeneratedSceneFactory.CreateCastle(
                geometry, worldSeed, parentId, in plan, entry.State, emissionMode);
            WorldObjectSceneLifecycle.PublishLoaded(this, parentId, entry.Scene);
            return entry.Scene;
        }

        public WorldObjectGeneratedScene LoadMineCave(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, DecorationBounds chamber,
            WorldObjectGeometryEmissionMode emissionMode = WorldObjectGeometryEmissionMode.AllVoxel)
        {
            Entry entry = GetForLoad(parentId);
            entry.Scene = WorldObjectGeneratedSceneFactory.CreateMineCave(
                geometry, worldSeed, parentId, chamber, entry.State, emissionMode);
            WorldObjectSceneLifecycle.PublishLoaded(this, parentId, entry.Scene);
            return entry.Scene;
        }

        public WorldObjectGeneratedScene LoadCastleForUnityDynamicPresentation(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, in CastlePlan plan) =>
            LoadCastle(geometry, worldSeed, parentId, in plan, WorldObjectGeometryEmissionMode.StaticOnly);

        public WorldObjectGeneratedScene LoadMineCaveForUnityDynamicPresentation(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, DecorationBounds chamber) =>
            LoadMineCave(geometry, worldSeed, parentId, chamber, WorldObjectGeometryEmissionMode.StaticOnly);

        public WorldObjectGeneratedScene LoadDecorations(uint parentId, DecorationPlacement[] placements)
        {
            Entry entry = GetForLoad(parentId);
            entry.Scene = DecorationWorldObjectRuntimeBridge.Create(placements, entry.State);
            WorldObjectSceneLifecycle.PublishLoaded(this, parentId, entry.Scene);
            return entry.Scene;
        }

        public bool TryGetLoaded(uint parentId, out WorldObjectGeneratedScene scene)
        {
            if (_entries.TryGetValue(parentId, out Entry entry) && entry.Scene != null)
            {
                scene = entry.Scene;
                return true;
            }
            scene = null;
            return false;
        }

        public bool Unload(uint parentId)
        {
            if (!_entries.TryGetValue(parentId, out Entry entry) || entry.Scene == null) return false;
            entry.Scene = null;
            WorldObjectSceneLifecycle.PublishUnloaded(this, parentId);
            return true;
        }

        public bool RemovePersistentState(uint parentId)
        {
            if (!_entries.TryGetValue(parentId, out Entry entry)) return false;
            bool wasLoaded = entry.Scene != null;
            _entries.Remove(parentId);
            if (wasLoaded)
                WorldObjectSceneLifecycle.PublishUnloaded(this, parentId);
            return true;
        }

        public WorldObjectStateDelta[] Snapshot(uint parentId)
        {
            if (!_entries.TryGetValue(parentId, out Entry entry)) return Array.Empty<WorldObjectStateDelta>();
            return entry.State.Snapshot();
        }

        public void Restore(uint parentId, WorldObjectStateDelta[] deltas)
        {
            Entry entry = GetOrCreate(parentId);
            if (entry.Scene != null)
                throw new InvalidOperationException(
                    $"World object scene {parentId} is loaded. Unload it before restoring persistent state.");
            entry.State.Clear();
            if (deltas == null) return;
            for (int i = 0; i < deltas.Length; i++)
                entry.State.Set(in deltas[i]);
        }

        public int TickLoaded(int ticks = 1)
        {
            int changed = 0;
            foreach (var pair in _entries)
                if (pair.Value.Scene != null)
                    changed += pair.Value.Scene.Runtime.Tick(ticks);
            return changed;
        }

        private Entry GetForLoad(uint parentId)
        {
            Entry entry = GetOrCreate(parentId);
            if (entry.Scene != null)
                throw new InvalidOperationException(
                    $"World object scene {parentId} is already loaded. Unload it before loading it again.");
            return entry;
        }

        private Entry GetOrCreate(uint parentId)
        {
            if (parentId == 0) throw new ArgumentOutOfRangeException(nameof(parentId));
            if (_entries.TryGetValue(parentId, out Entry existing)) return existing;
            var created = new Entry();
            _entries.Add(parentId, created);
            return created;
        }

        private sealed class Entry
        {
            public readonly WorldObjectStateStore State = new WorldObjectStateStore();
            public WorldObjectGeneratedScene Scene;
        }
    }
}
