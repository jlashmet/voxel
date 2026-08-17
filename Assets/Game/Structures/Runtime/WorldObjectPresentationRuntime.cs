using System;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public interface IWorldObjectPresentationSink
    {
        void CreateOrUpdate(in WorldObjectPresentationPlan plan);
        void Remove(WorldObjectId id);
    }

    /// <summary>
    /// Keeps object-owned dynamic presentation synchronized with a live WorldObject scene. The sink decides
    /// whether plans become generated meshes, GameObjects, ECS entities, colliders, lights, or another backend.
    /// </summary>
    public sealed class WorldObjectPresentationRuntime : IDisposable
    {
        private readonly WorldObjectGeneratedScene _scene;
        private readonly IWorldObjectPresentationSink _sink;
        private bool _disposed;

        public WorldObjectPresentationRuntime(WorldObjectGeneratedScene scene, IWorldObjectPresentationSink sink)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _scene.Runtime.StateChanged += OnStateChanged;
            RefreshAll();
        }

        public void RefreshAll()
        {
            WorldObjectPresentationPlan[] plans = WorldObjectPresentationPlanner.PlanAll(
                _scene.Objects, _scene.Runtime.StateStore);
            for (int i = 0; i < plans.Length; i++)
                Apply(in plans[i]);
        }

        public bool Refresh(WorldObjectId id)
        {
            if (!_scene.Runtime.TryResolve(id, out WorldObjectResolvedState state)) return false;
            WorldObjectPresentationPlan plan = WorldObjectPresentationPlanner.Plan(in state);
            Apply(in plan);
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scene.Runtime.StateChanged -= OnStateChanged;
            for (int i = 0; i < _scene.Objects.Length; i++)
                _sink.Remove(_scene.Objects[i].Id);
        }

        private void OnStateChanged(WorldObjectId id)
        {
            Refresh(id);
        }

        private void Apply(in WorldObjectPresentationPlan plan)
        {
            if (!plan.Visible)
                _sink.Remove(plan.Id);
            else
                _sink.CreateOrUpdate(in plan);
        }
    }
}
