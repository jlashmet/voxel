using Game.Combat.Api;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Kentridge-owned balance for the authored forest ambush. Core Combat remains team/turn generic;
    /// this composition decides the persistent player's survivability relative to the three 6-vitality bandits.
    /// </summary>
    public static class KentridgeForestCombatTuning
    {
        public const int BattleSeed = 20260829;
        public const int PlayerInitialVitality = 40;
        public const int BanditInitialVitality = 6;

        public static int InitialVitality(CombatTeam team) =>
            team == CombatTeam.Player ? PlayerInitialVitality : BanditInitialVitality;
    }
}
