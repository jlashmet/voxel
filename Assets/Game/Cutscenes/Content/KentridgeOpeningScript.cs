using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Spoken content for the Kentridge opening.
    ///
    /// Lines 1-31 are transcribed verbatim from the original MountingForce Art/Opening.txt.
    /// The choreography consumes stable cue ids while presentation resolves those ids here, so
    /// dialogue content remains separate from staging and timing.
    /// </summary>
    public static class KentridgeOpeningScript
    {
        public const int OriginalOpeningLineCount = 31;

        private const string OpeningCuePrefix = "kentridge.pub.opening.line-";

        private static readonly string[] OriginalOpeningLines =
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

        private static readonly Dictionary<string, string> AdditionalLines = new Dictionary<string, string>
        {
            {
                "destination-conversation.dialogue",
                "You made it. Tell me what you found on the road."
            },
        };

        public static CutsceneCueId CueForOriginalLine(int oneBasedLineNumber)
        {
            if (oneBasedLineNumber < 1 || oneBasedLineNumber > OriginalOpeningLineCount)
                throw new ArgumentOutOfRangeException(nameof(oneBasedLineNumber));

            return new CutsceneCueId(OpeningCuePrefix + oneBasedLineNumber.ToString("00"));
        }

        /// <summary>
        /// The line for a cue, or a visible placeholder naming the cue.
        /// </summary>
        public static string LineFor(CutsceneCueId cue)
        {
            string id = cue.Value ?? string.Empty;
            if (id.StartsWith(OpeningCuePrefix, StringComparison.Ordinal))
            {
                string suffix = id.Substring(OpeningCuePrefix.Length);
                if (int.TryParse(suffix, out int lineNumber)
                    && lineNumber >= 1
                    && lineNumber <= OriginalOpeningLineCount)
                    return OriginalOpeningLines[lineNumber - 1];
            }

            return AdditionalLines.TryGetValue(id, out string line) ? line : "[" + id + "]";
        }
    }
}
