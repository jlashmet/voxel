using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Runtime-facing lookup for concrete gameplay actors. Composition owns the translation from
    /// WorldBuilder's semantic NPC/player targets into the engine-independent cutscene actor API.
    /// Implementations may be backed by the authoritative player/NPC runtime without exposing that
    /// runtime to either WorldBuilder or Cutscenes.
    /// </summary>
    public interface IWorldBoundCutsceneActorProvider
    {
        bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor);
        bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor);
    }

    /// <summary>
    /// Concrete actor controller for one world-bound CutsceneSpec. It consumes only public APIs:
    /// WorldBuilder supplies semantic bindings and Cutscenes supplies the actor-control contract.
    /// No Game runtime assembly is referenced across subsystem boundaries.
    /// </summary>
    public sealed class WorldBoundCutsceneActorController : ICutsceneActorController
    {
        private readonly Dictionary<CutsceneActorId, ICutsceneActorRuntime> _actors =
            new Dictionary<CutsceneActorId, ICutsceneActorRuntime>();

        public WorldBoundCutsceneActorController(
            CutsceneSpec cutscene,
            IWorldBoundCutsceneActorProvider provider)
        {
            if (cutscene == null) throw new ArgumentNullException(nameof(cutscene));
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            for (var i = 0; i < cutscene.ActorBindings.Count; i++)
            {
                CutsceneActorBindingSpec binding = cutscene.ActorBindings[i];
                if (_actors.ContainsKey(binding.Actor))
                    throw new InvalidOperationException(
                        "Cutscene '" + cutscene.Ref + "' binds actor '" + binding.Actor + "' more than once.");

                ICutsceneActorRuntime actor;
                bool resolved;
                switch (binding.Target.Kind)
                {
                    case CutsceneActorTargetKind.Npc:
                        resolved = provider.TryResolveNpc(binding.Target.Npc, out actor);
                        break;
                    case CutsceneActorTargetKind.PlayerSlot:
                        resolved = provider.TryResolvePlayer(binding.Target.PlayerSlot, out actor);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Cutscene '" + cutscene.Ref + "' contains unsupported actor target kind '" +
                            binding.Target.Kind + "'.");
                }

                if (!resolved || actor == null)
                    throw new InvalidOperationException(
                        "Cutscene '" + cutscene.Ref + "' cannot resolve runtime target for actor '" +
                        binding.Actor + "'.");

                _actors.Add(binding.Actor, actor);
            }

            for (var i = 0; i < cutscene.Definition.RequiredActors.Count; i++)
            {
                CutsceneActorId required = cutscene.Definition.RequiredActors[i];
                if (!_actors.ContainsKey(required))
                    throw new InvalidOperationException(
                        "Cutscene '" + cutscene.Ref + "' requires actor '" + required +
                        "', but no runtime actor binding is available.");
            }
        }

        public bool Contains(CutsceneActorId actor) => _actors.ContainsKey(actor);

        public void PlaceAt(CutsceneActorId actor, CutsceneStagePoint destination) =>
            Resolve(actor).PlaceAt(destination);

        public ICutsceneOperation MoveTo(
            CutsceneActorId actor,
            CutsceneStagePoint destination,
            int durationHintMilliseconds) =>
            Resolve(actor).MoveTo(destination, durationHintMilliseconds);

        public ICutsceneOperation FaceActor(CutsceneActorId actor, CutsceneActorId target) =>
            Resolve(actor).FaceTowards(Resolve(target).Position);

        public ICutsceneOperation FacePoint(CutsceneActorId actor, CutsceneStagePoint target) =>
            Resolve(actor).FaceTowards(target.Position);

        private ICutsceneActorRuntime Resolve(CutsceneActorId actor)
        {
            ICutsceneActorRuntime runtime;
            if (_actors.TryGetValue(actor, out runtime)) return runtime;
            throw new KeyNotFoundException("Cutscene actor '" + actor + "' is not bound to a runtime actor.");
        }
    }
}
