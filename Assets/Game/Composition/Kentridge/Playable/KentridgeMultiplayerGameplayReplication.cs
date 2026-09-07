using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Encounters.Api;
using Game.GameplayReplication.Adapters;
using Game.GameplayReplication.Api;
using Game.Inventory.Api;
using Game.Progression.Api;
using Game.Vitality.Api;
using Game.WorldObjects.Api;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Kentridge composition for the semantic gameplay projections shared by multiplayer authority
    /// and clients. Queries are resolved lazily because Sessions admission exists before the campaign
    /// runtime graph is composed; until an owning runtime exists the source publishes the same schema
    /// with no entries rather than manufacturing gameplay state.
    /// </summary>
    public sealed class KentridgeMultiplayerGameplayReplication
    {
        public static readonly GameplayProjectionDescriptor CharactersDescriptor =
            new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, true);
        public static readonly GameplayProjectionDescriptor InventoryDescriptor =
            new GameplayProjectionDescriptor(new GameplayProjectionId("inventory"), 2, true);
        public static readonly GameplayProjectionDescriptor ProgressionDescriptor =
            new GameplayProjectionDescriptor(new GameplayProjectionId("progression"), 1, true);
        public static readonly GameplayProjectionDescriptor EncountersDescriptor =
            new GameplayProjectionDescriptor(new GameplayProjectionId("encounters"), 1, true);
        public static readonly GameplayProjectionDescriptor VitalityDescriptor =
            new GameplayProjectionDescriptor(new GameplayProjectionId("vitality"), 1, true);
        public static readonly GameplayProjectionDescriptor CombatDescriptor =
            new GameplayProjectionDescriptor(new GameplayProjectionId("combat"), 1, true);
        public static readonly GameplayProjectionDescriptor WorldObjectsDescriptor =
            new GameplayProjectionDescriptor(new GameplayProjectionId("world-objects"), 1, true);

        private static readonly IReadOnlyList<GameplayProjectionDescriptor> s_descriptors =
            Array.AsReadOnly(new[]
            {
                CharactersDescriptor,
                InventoryDescriptor,
                ProgressionDescriptor,
                EncountersDescriptor,
                VitalityDescriptor,
                CombatDescriptor,
                WorldObjectsDescriptor
            });

        private readonly IReadOnlyList<IGameplayProjectionSource> _authoritySources;

        private KentridgeMultiplayerGameplayReplication(IGameplayProjectionSource[] authoritySources)
        {
            _authoritySources = Array.AsReadOnly(authoritySources);
        }

        public static IReadOnlyList<GameplayProjectionDescriptor> Descriptors => s_descriptors;
        public IReadOnlyList<IGameplayProjectionSource> AuthoritySources => _authoritySources;

        public static KentridgeMultiplayerGameplayReplication Create(
            ICharacterQuery characters,
            Func<IInventoryQuery> inventory,
            Func<IProgressionQuery> progression,
            Func<IEncounterQuery> encounters,
            Func<IVitalityQuery> vitality,
            Func<ICombatService> combat,
            Func<IWorldObjectRegistry> worldObjects = null)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            if (encounters == null) throw new ArgumentNullException(nameof(encounters));
            if (vitality == null) throw new ArgumentNullException(nameof(vitality));
            if (combat == null) throw new ArgumentNullException(nameof(combat));
            Func<IWorldObjectRegistry> worldObjectQuery = worldObjects ?? (() => null);

            return new KentridgeMultiplayerGameplayReplication(new IGameplayProjectionSource[]
            {
                new DeferredSource<ICharacterQuery>(
                    CharactersDescriptor,
                    () => characters,
                    query => new CharactersGameplayProjectionSource(query)),
                new DeferredSource<IInventoryQuery>(
                    InventoryDescriptor,
                    inventory,
                    query => new InventoryGameplayProjectionSource(query)),
                new DeferredSource<IProgressionQuery>(
                    ProgressionDescriptor,
                    progression,
                    query => new ProgressionGameplayProjectionSource(query)),
                new DeferredSource<IEncounterQuery>(
                    EncountersDescriptor,
                    encounters,
                    query => new EncounterGameplayProjectionSource(query)),
                new DeferredSource<IVitalityQuery>(
                    VitalityDescriptor,
                    vitality,
                    query => new VitalityGameplayProjectionSource(query)),
                new DeferredSource<ICombatService>(
                    CombatDescriptor,
                    combat,
                    query => new CombatGameplayProjectionSource(query)),
                new DeferredSource<IWorldObjectRegistry>(
                    WorldObjectsDescriptor,
                    worldObjectQuery,
                    query => new WorldObjectsProjectionSource(query))
            });
        }

        private sealed class WorldObjectsProjectionSource : IGameplayProjectionSource
        {
            private readonly IWorldObjectRegistry _objects;

            public WorldObjectsProjectionSource(IWorldObjectRegistry objects)
            {
                _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            }

            public GameplayProjectionDescriptor Descriptor => WorldObjectsDescriptor;

            public GameplayProjectionState Capture()
            {
                IReadOnlyList<WorldObjectStateSnapshot> snapshots = _objects.CaptureState();
                var entries = new List<GameplayProjectionEntry>(snapshots.Count * 5);
                for (int i = 0; i < snapshots.Count; i++)
                {
                    WorldObjectStateSnapshot snapshot = snapshots[i];
                    string prefix = "object/" + snapshot.ObjectId.Value + "/";
                    entries.Add(new GameplayProjectionEntry(prefix + "kind", snapshot.Kind.ToString()));
                    entries.Add(new GameplayProjectionEntry(prefix + "enabled", snapshot.Enabled ? "true" : "false"));
                    entries.Add(new GameplayProjectionEntry(
                        prefix + "state-code", snapshot.StateCode.ToString(CultureInfo.InvariantCulture)));
                    entries.Add(new GameplayProjectionEntry(
                        prefix + "revision", snapshot.Revision.ToString(CultureInfo.InvariantCulture)));
                }
                return new GameplayProjectionState(Descriptor, entries);
            }
        }

        private sealed class DeferredSource<TQuery> : IGameplayProjectionSource where TQuery : class
        {
            private readonly Func<TQuery> _query;
            private readonly Func<TQuery, IGameplayProjectionSource> _sourceFactory;
            private TQuery _lastQuery;
            private IGameplayProjectionSource _source;

            public DeferredSource(
                GameplayProjectionDescriptor descriptor,
                Func<TQuery> query,
                Func<TQuery, IGameplayProjectionSource> sourceFactory)
            {
                Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
                _query = query ?? throw new ArgumentNullException(nameof(query));
                _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
            }

            public GameplayProjectionDescriptor Descriptor { get; }

            public GameplayProjectionState Capture()
            {
                TQuery query = _query();
                if (query == null)
                {
                    _lastQuery = null;
                    _source = null;
                    return new GameplayProjectionState(Descriptor, Array.Empty<GameplayProjectionEntry>());
                }

                if (!ReferenceEquals(query, _lastQuery))
                {
                    _lastQuery = query;
                    _source = _sourceFactory(query)
                        ?? throw new InvalidOperationException(
                            "Kentridge gameplay projection factory returned no source for " + Descriptor.Id.Value + ".");
                    ValidateDescriptor(_source.Descriptor);
                }

                GameplayProjectionState state = _source.Capture()
                    ?? throw new InvalidOperationException(
                        "Kentridge gameplay projection source returned no state for " + Descriptor.Id.Value + ".");
                ValidateDescriptor(state.Descriptor);
                return state;
            }

            private void ValidateDescriptor(GameplayProjectionDescriptor actual)
            {
                if (actual == null ||
                    actual.Id != Descriptor.Id ||
                    actual.SchemaVersion != Descriptor.SchemaVersion ||
                    actual.RequiredForGameplayReady != Descriptor.RequiredForGameplayReady)
                    throw new InvalidOperationException(
                        "Kentridge gameplay projection schema changed for " + Descriptor.Id.Value + ".");
            }
        }
    }
}
