using System.Collections.Generic;

namespace Game.Persistence.Api
{
    /// <summary>Read-only save discovery used by application presentation. Restore authority remains in Persistence/Orchestration.</summary>
    public interface ISessionSaveCatalog
    {
        IReadOnlyList<SessionSaveMetadata> ListSaves();
    }
}
