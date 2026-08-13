using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Read-only presentation for the player-level proactive activation rule.
    /// It deliberately says who each player committed this round without suggesting any reaction/combo choices.
    /// </summary>
    [RequireComponent(typeof(ChainCombatLabController))]
    public sealed class ChainCombatActivationOverlay : MonoBehaviour
    {
        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);

        private ChainCombatLabController _controller;
        private ChainCombatBoard _board;
        private GUIStyle _header;
        private GUIStyle _small;

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
        }

        private void Update()
        {
            if (_board == null && _controller != null && BoardField != null)
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
        }

        private void OnGUI()
        {
            if (_board == null) return;
            EnsureStyles();

            GUILayout.BeginArea(new Rect(10f, 200f, 310f, 176f), GUI.skin.box);
            GUILayout.Label("PLAYER ACTIVATIONS", _header);
            GUILayout.Label("Each player commits one recruit for proactive play this round. That recruit gets one move + one action. Every recruit can still react.", _small);

            for (int group = 1; group <= 4; group++)
            {
                int activeId = _board.GetActiveRecruitId(group);
                ChainUnitState active = _board.GetUnit(activeId);
                if (active == null)
                {
                    GUILayout.Label($"P{group}: choose an active recruit", _small);
                    continue;
                }

                string move = active.MoveSpent ? "move spent" : "move ready";
                string action = active.ActionSpent ? "action spent" : "action ready";
                GUILayout.Label($"P{group}: {active.Name} — {move}, {action}", _small);
            }

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
        }
    }
}
