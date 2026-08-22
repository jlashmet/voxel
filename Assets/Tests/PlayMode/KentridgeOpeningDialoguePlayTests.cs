using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Regression coverage for the recovered MountingForce Opening.m + Art/Opening.txt content.
    /// The original script advances dialogue in three groups: 10 lines, then Logan's first line,
    /// then the remaining 20 lines after the group turns to face him.
    /// </summary>
    public sealed class KentridgeOpeningDialoguePlayTests
    {
        [Test]
        public void OpeningDialogue_MatchesRecoveredOriginalTextAndSpeakerOrder()
        {
            string[] expectedLines =
            {
                "Madeline: Hey Weldon, Glad you could join us!  How did your magic lessons go?  It looks like Medrare kept you late again.",
                "Weldon: Do you have to rub it in?  You know I was forced into wizardry, and I hate every second of it!",
                "Weldon: Just because my father was a wizard, now I have to get stuck with it too?  What kind of crazy system is that?",
                "Madeline: But you're so talented! The rest of the students would give anything to have your skills.",
                "Weldon: Anyway, man I'm starving.  I wish I could get a decent meal in this town.",
                "Steven: Bah, you probably could if it weren't for Lord Radcliffe.",
                "Steven: I see servants bringing food to his house day in and day out.",
                "Madeline: Hey Weldon, why don't you go cast a spell on him?",
                "Weldon: Madeline geez, can we drop the magic talk already?",
                "Madeline: Sorry, tee hee.  Here, next round is on me.",
                "Logan: Pardon me friends, I don't believe weve met before.",
                "Logan: My name is Logan. I couldn't help overhearing your conversation, and I'd like to tell you there are many more in this town that share your sentiments.",
                "Madeline: Yah its pretty hard not to feel that way when you're starving and forced to watch fatty Radcliffe gorge himself.",
                "Logan: I've been trying to gather support to try and take Radcliffe down.  However, its been difficult.",
                "Logan: Even though most of the town is of the same mind, I've yet to find anyone that will help me take action.",
                "Weldon: So... what are you planning?",
                "Logan: Well, I'm a reasonable man.  I'd like to schedule a meeting with Lord Radcliffe and inquire about the official supply distribution policies.",
                "Logan: I'm hoping he owns up to his squandering, and that we can reach an agreement peacefully.",
                "Logan: However, I'd like you all to accompany me as lets say... an insurance policy.",
                "Steven: So you want us to march into an elected officials house like some brute squad and take care of your dirty work?  Unthinkable!",
                "Logan: Look, I can tell by your appearance that you are an ordained knight.  And apparently, one so dedicated that you feel the need to wear your helmet in doors.",
                "Logan: If Lord Radcliffe is breaking the law by stealing food and supplies, isn't it your duty to uphold the law?",
                "Steven: Well, I suppose so... What do you guys think?",
                "Weldon: I'm in.  Its about time Radcliffe was confronted.",
                "Madeline: Me too.  Its time the people got their fair share and I'm sure he will listen to reason.",
                "Weldon: Ok then.  It's settled.  Logan, we're with you.",
                "Weldon: There's a few things I have to do first though.  First, my father wanted me to stop by the house to show me something.",
                "Weldon: Do you mind doing that first?",
                "Logan: For my new companions? Anything!",
                "Weldon: Ok, my house is in the southwest corner of the town just north of the church.",
                "Logan: Alright friends.  Lets head out!"
            };

            CutsceneActorId[] expectedSpeakers =
            {
                KentridgeOpeningCutscene.Madeline,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Madeline,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Steven,
                KentridgeOpeningCutscene.Steven,
                KentridgeOpeningCutscene.Madeline,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Madeline,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Madeline,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Steven,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Steven,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Madeline,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Logan,
                KentridgeOpeningCutscene.Lead,
                KentridgeOpeningCutscene.Logan
            };

            Assert.That(KentridgeOpeningScript.OriginalOpeningLineCount, Is.EqualTo(expectedLines.Length));

            var dialogueSteps = new List<CutsceneStep>();
            IReadOnlyList<CutsceneStep> steps = KentridgeOpeningCutscene.Definition.Steps;
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Type == CutsceneStepType.Dialogue)
                    dialogueSteps.Add(steps[i]);
            }

            Assert.That(dialogueSteps.Count, Is.EqualTo(expectedLines.Length),
                "The recovered opening must keep all 31 original spoken lines as separate dialogue steps.");

            for (var i = 0; i < expectedLines.Length; i++)
            {
                CutsceneCueId expectedCue = KentridgeOpeningScript.CueForOriginalLine(i + 1);
                Assert.That(dialogueSteps[i].Cue, Is.EqualTo(expectedCue), "Unexpected cue at original line " + (i + 1));
                Assert.That(dialogueSteps[i].Actor, Is.EqualTo(expectedSpeakers[i]), "Unexpected speaker at original line " + (i + 1));
                Assert.That(KentridgeOpeningScript.LineFor(expectedCue), Is.EqualTo(expectedLines[i]),
                    "Recovered text changed at original line " + (i + 1));
            }
        }

        [Test]
        public void OpeningChoreography_PreservesOriginalTenOneRestSplitAroundLoganEntrance()
        {
            IReadOnlyList<CutsceneStep> steps = KentridgeOpeningCutscene.Definition.Steps;
            Assert.That(steps.Count, Is.EqualTo(46));

            AssertStep(steps[0], CutsceneStepType.Camera, 0);
            AssertStep(steps[1], CutsceneStepType.Wait, 3000);
            AssertStep(steps[2], CutsceneStepType.Sound, 0);
            AssertStep(steps[3], CutsceneStepType.Wait, 2000);
            AssertStep(steps[4], CutsceneStepType.MoveActor, 2500);
            AssertStep(steps[6], CutsceneStepType.Wait, 500);
            AssertStep(steps[8], CutsceneStepType.Wait, 500);

            for (var i = 0; i < 10; i++)
            {
                Assert.That(steps[9 + i].Type, Is.EqualTo(CutsceneStepType.Dialogue));
                Assert.That(steps[9 + i].Cue, Is.EqualTo(KentridgeOpeningScript.CueForOriginalLine(i + 1)));
            }

            AssertStep(steps[19], CutsceneStepType.Wait, 500);
            Assert.That(steps[20].Type, Is.EqualTo(CutsceneStepType.Parallel),
                "The trio must turn toward the entrance before Logan approaches.");
            AssertStep(steps[21], CutsceneStepType.Wait, 2500);
            AssertStep(steps[22], CutsceneStepType.MoveActor, 2000);
            Assert.That(steps[22].Actor, Is.EqualTo(KentridgeOpeningCutscene.Logan));

            Assert.That(steps[23].Type, Is.EqualTo(CutsceneStepType.Dialogue));
            Assert.That(steps[23].Actor, Is.EqualTo(KentridgeOpeningCutscene.Logan));
            Assert.That(steps[23].Cue, Is.EqualTo(KentridgeOpeningScript.CueForOriginalLine(11)),
                "Logan's first line must occur immediately after his approach.");

            Assert.That(steps[24].Type, Is.EqualTo(CutsceneStepType.Parallel),
                "Only after Logan's first line should Weldon, Madeline, and Steven turn to face him.");
            AssertStep(steps[25], CutsceneStepType.Wait, 500);

            for (var i = 12; i <= 31; i++)
            {
                int stepIndex = 26 + (i - 12);
                Assert.That(steps[stepIndex].Type, Is.EqualTo(CutsceneStepType.Dialogue));
                Assert.That(steps[stepIndex].Cue, Is.EqualTo(KentridgeOpeningScript.CueForOriginalLine(i)));
            }
        }

        private static void AssertStep(CutsceneStep step, CutsceneStepType expectedType, int expectedDurationMilliseconds)
        {
            Assert.That(step.Type, Is.EqualTo(expectedType));
            Assert.That(step.DurationMilliseconds, Is.EqualTo(expectedDurationMilliseconds));
        }
    }
}
