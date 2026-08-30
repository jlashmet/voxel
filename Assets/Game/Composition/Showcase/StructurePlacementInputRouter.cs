using System;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Input-device-independent routing for the showcase structure-placement mode.
    /// While selection is active, wheel and commit input belong exclusively to placement so the
    /// scene can suppress its ordinary brush/jump meanings. Outside selection mode this router
    /// consumes nothing.
    /// </summary>
    public sealed class StructurePlacementInputRouter
    {
        private readonly StructurePlacementSelection _selection;

        public StructurePlacementInputRouter(StructurePlacementSelection selection)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        }

        public bool Active => _selection.Active;
        public int SelectedIndex => _selection.SelectedIndex;
        public string SelectedName => _selection.SelectedName;

        public void Begin(int selectedIndex = 0) => _selection.Begin(selectedIndex);
        public void Cancel() => _selection.Cancel();

        public StructurePlacementInputResult Route(
            int scrollDelta,
            bool commitPressed,
            Func<int, bool> commit)
        {
            bool activeAtStart = _selection.Active;
            if (!activeAtStart)
                return new StructurePlacementInputResult(false, false, false, _selection.SelectedIndex);

            bool consumeScroll = scrollDelta != 0;
            if (consumeScroll) _selection.Scroll(scrollDelta);

            bool committed = false;
            if (commitPressed)
            {
                if (commit == null) throw new ArgumentNullException(nameof(commit));
                committed = _selection.TryCommitSelected(commit);
            }

            return new StructurePlacementInputResult(
                consumeScroll,
                commitPressed,
                committed,
                _selection.SelectedIndex);
        }
    }

    public readonly struct StructurePlacementInputResult
    {
        public readonly bool ConsumeScroll;
        public readonly bool ConsumeCommitControl;
        public readonly bool PlacementCommitted;
        public readonly int SelectedIndex;

        public StructurePlacementInputResult(
            bool consumeScroll,
            bool consumeCommitControl,
            bool placementCommitted,
            int selectedIndex)
        {
            ConsumeScroll = consumeScroll;
            ConsumeCommitControl = consumeCommitControl;
            PlacementCommitted = placementCommitted;
            SelectedIndex = selectedIndex;
        }
    }
}
