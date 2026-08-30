using System;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Input-agnostic state for the showcase's structure picker. The scene decides which key enters
    /// selection mode and translates wheel/key-down edges into these calls; this object guarantees
    /// that a successful commit cannot repeat on later Update frames.
    /// </summary>
    public sealed class StructurePlacementSelection
    {
        private readonly string[] _names;
        private int _selectedIndex;

        public StructurePlacementSelection(string[] names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (names.Length == 0) throw new ArgumentException("At least one structure is required.", nameof(names));
            _names = (string[])names.Clone();
            for (int i = 0; i < _names.Length; i++)
                if (string.IsNullOrWhiteSpace(_names[i]))
                    throw new ArgumentException($"Structure name {i} is empty.", nameof(names));
        }

        public bool Active { get; private set; }
        public bool Committed { get; private set; }
        public int SelectedIndex => _selectedIndex;
        public string SelectedName => _names[_selectedIndex];
        public int Count => _names.Length;

        public void Begin(int selectedIndex = 0)
        {
            if ((uint)selectedIndex >= (uint)_names.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            _selectedIndex = selectedIndex;
            Committed = false;
            Active = true;
        }

        public void Cancel()
        {
            Active = false;
            Committed = false;
        }

        public void Scroll(int delta)
        {
            if (!Active || Committed || delta == 0) return;
            int direction = delta > 0 ? 1 : -1;
            _selectedIndex = (_selectedIndex + direction + _names.Length) % _names.Length;
        }

        public bool TryCommitSelected(Func<int, bool> commit)
        {
            if (commit == null) throw new ArgumentNullException(nameof(commit));
            if (!Active || Committed) return false;
            if (!commit(_selectedIndex)) return false;
            Committed = true;
            Active = false;
            return true;
        }
    }
}
