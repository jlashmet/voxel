using System;
using System.Collections.Generic;
using Game.Persistence.Api;

namespace Game.Persistence.Runtime
{
    /// <summary>Thin API adapter over the existing SessionPersistenceService save discovery path.</summary>
    public sealed class SessionSaveCatalog : ISessionSaveCatalog
    {
        private readonly SessionPersistenceService _persistence;

        public SessionSaveCatalog(SessionPersistenceService persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public IReadOnlyList<SessionSaveMetadata> ListSaves() => _persistence.ListSaves();
    }
}
