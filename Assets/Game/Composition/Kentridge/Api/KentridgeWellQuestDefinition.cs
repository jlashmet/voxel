using System.Collections.Generic;
using Game.Quests.Api;

namespace Game.Composition.Kentridge.Api
{
    /// <summary>
    /// Stable semantic content for the recovered Kentridge kid-in-the-well quest. The current world
    /// already owns the market well; this definition owns only quest identity and progression.
    /// </summary>
    public static class KentridgeWellQuestDefinition
    {
        public const string QuestId = "kentridge-kid-in-the-well";
        public const string WellTargetId = "kentridge-well";
        public const string MadelineNpcId = "madeline";
        public const string RewardItemId = "kentridge-well-rescue-token";

        public static QuestRef Ref => new QuestRef(QuestId);

        public static QuestDefinition Create() =>
            new QuestDefinition(
                Ref,
                new[]
                {
                    new QuestStepDefinition(
                        new QuestStepRef("rescue-boy-at-well"),
                        WellTargetId,
                        QuestCompletion.InteractWithSubject(WellTargetId)),
                    new QuestStepDefinition(
                        new QuestStepRef("return-to-madeline"),
                        MadelineNpcId,
                        QuestCompletion.InteractWith(MadelineNpcId)),
                });

        public static IReadOnlyList<QuestDefinition> CreateDefinitions() =>
            new[] { Create() };
    }
}
