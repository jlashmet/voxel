using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// Dialogue content for the recovered Kentridge opening. Text is copied verbatim from the
    /// pinned Mounting Force payload when that payload exists. Missing legacy payloads resolve to an
    /// empty string so this port preserves their identity/gates without inventing replacement prose.
    /// </summary>
    public static class KentridgeOpeningScript
    {
        public const int OriginalOpeningLineCount = 31;
        public const int AwonOpeningLineCount = 22;
        public const int AwonOpeningBeatCount = AwonOpeningLineCount;

        private const string OpeningCuePrefix = "kentridge.pub.opening.line-";
        private const string AwonOpeningCuePrefix = "kentridge.awon.opening.line-";

        public static readonly CutsceneCueId SeeMedrareSourceDialogue =
            new CutsceneCueId("mounting-force.dialogue.kentridge-see-medrare");
        public static readonly CutsceneCueId MedrareJoinSourceDialogue5000 =
            new CutsceneCueId("mounting-force.dialogue.5000");
        public static readonly CutsceneCueId MedrareFirstSpellSourceDialogue =
            new CutsceneCueId("mounting-force.dialogue.medrare-first-spell");
        public static readonly CutsceneCueId MedrareToChurchSourceDialogue =
            new CutsceneCueId("mounting-force.dialogue.medrare-to-church");

        private static readonly string[] OriginalOpeningLines =
        {
            "Hey Weldon, Glad you could join us!  How did your magic lessons go?  It looks like Medrare kept you late again.",
            "Do you have to rub it in?  You know I was forced into wizardry, and I hate every second of it!",
            "Just because my father was a wizard, now I have to get stuck with it too?  What kind of crazy system is that?",
            "But you're so talented! The rest of the students would give anything to have your skills.",
            "Anyway, man I'm starving.  I wish I could get a decent meal in this town.",
            "Bah, you probably could if it weren't for Lord Radcliffe.",
            "I see servants bringing food to his house day in and day out.",
            "Hey Weldon, why don't you go cast a spell on him?",
            "Madeline geez, can we drop the magic talk already?",
            "Sorry, tee hee.  Here, next round is on me.",
            "Pardon me friends, I don't believe weve met before.",
            "My name is Logan. I couldn't help overhearing your conversation, and I'd like to tell you there are many more in this town that share your sentiments.",
            "Yah its pretty hard not to feel that way when you're starving and forced to watch fatty Radcliffe gorge himself.",
            "I've been trying to gather support to try and take Radcliffe down.  However, its been difficult.",
            "Even though most of the town is of the same mind, I've yet to find anyone that will help me take action.",
            "So... what are you planning?",
            "Well, I'm a reasonable man.  I'd like to schedule a meeting with Lord Radcliffe and inquire about the official supply distribution policies.",
            "I'm hoping he owns up to his squandering, and that we can reach an agreement peacefully.",
            "However, I'd like you all to accompany me as lets say... an insurance policy.",
            "So you want us to march into an elected officials house like some brute squad and take care of your dirty work?  Unthinkable!",
            "Look, I can tell by your appearance that you are an ordained knight.  And apparently, one so dedicated that you feel the need to wear your helmet in doors.",
            "If Lord Radcliffe is breaking the law by stealing food and supplies, isn't it your duty to uphold the law?",
            "Well, I suppose so... What do you guys think?",
            "I'm in.  Its about time Radcliffe was confronted.",
            "Me too.  Its time the people got their fair share and I'm sure he will listen to reason.",
            "Ok then.  It's settled.  Logan, we're with you.",
            "There's a few things I have to do first though.  First, my father wanted me to stop by the house to show me something.",
            "Do you mind doing that first?",
            "For my new companions? Anything!",
            "Ok, my house is in the southwest corner of the town just north of the church.",
            "Alright friends.  Lets head out!"
        };

        private static readonly string[] AwonOpeningLines =
        {
            "Weldon my boy!",
            "Hey dad.",
            "How are you all?  Good to see you Steven.  Hey madeline.",
            "Greetings sir.  A pleasure to see you again.",
            "Hi!  Tee hee.",
            "I don't believe I've met this young fellow.  Pleased to meet you.  I'm Weldon's father, Awon.",
            "The pleasure is all mine sir.  ",
            "We're going with Logan to meet with Lord Radcliffe later to ask about the lack of food lately.",
            "Ohhhh Madeline, you are a brave young bunch.  Be very careful though, Radcliffe can be a dangerous man.",
            "We understand and agree sir, but this matter is too important to ignore.  If things do go awry, I'm confident in our ability to defend ourselves.",
            "Well certainly you and Steven can hold your own, but what about Weldon?",
            "Weldon, when you see them rush gallantly into battle, you remember to stay back and cast your spells like a ninny you hear?",
            "Yes dad...",
            "And try not to cause too much harm, or everyone will come after you and you'll have to run around in circles, again like a complete ninny.",
            "Dad cut it out! I know how to handle myself.",
            "Ok ok. Haha.  Well anyway, the reason I asked you to stop by is I found an old family heirloom in the back room.",
            "Its behind a bunch of boxes that are too heavy for my old bones to move, but if you can clear them out, I think you'll find it useful.",
            "And even better, you don't even have to equip it!  Because any items you find will add to your skills, no equipping or unequipping is needed.",
            "If you click on your picture in the top left corner, you can see which items are equipped to each of your party members.",
            "What do I do after clicking the picture?",
            "Really Weldon? You are a wizard.  Figure it out already.",
            "Ok thanks dad, thats helpful.  We will check it out."
        };

        private static readonly Dictionary<string, string> AdditionalLines = new Dictionary<string, string>
        {
            { "destination-conversation.dialogue", "You made it. Tell me what you found on the road." },
            { SeeMedrareSourceDialogue.Value, string.Empty },
            { MedrareJoinSourceDialogue5000.Value, string.Empty },
            { MedrareFirstSpellSourceDialogue.Value, string.Empty },
            { MedrareToChurchSourceDialogue.Value, string.Empty }
        };

        public static CutsceneCueId CueForOriginalLine(int oneBasedLineNumber) =>
            CueForLine(OpeningCuePrefix, oneBasedLineNumber, OriginalOpeningLineCount);

        public static CutsceneCueId CueForAwonOpeningLine(int oneBasedLineNumber) =>
            CueForLine(AwonOpeningCuePrefix, oneBasedLineNumber, AwonOpeningLineCount);

        public static CutsceneCueId CueForAwonOpeningBeat(int oneBasedLineNumber) =>
            CueForAwonOpeningLine(oneBasedLineNumber);

        public static string LineFor(CutsceneCueId cue)
        {
            string id = cue.Value ?? string.Empty;
            if (TryLine(id, OpeningCuePrefix, OriginalOpeningLines, out string line)) return line;
            if (TryLine(id, AwonOpeningCuePrefix, AwonOpeningLines, out line)) return line;
            return AdditionalLines.TryGetValue(id, out line) ? line : "[" + id + "]";
        }

        private static CutsceneCueId CueForLine(string prefix, int oneBasedLineNumber, int lineCount)
        {
            if (oneBasedLineNumber < 1 || oneBasedLineNumber > lineCount)
                throw new ArgumentOutOfRangeException(nameof(oneBasedLineNumber));
            return new CutsceneCueId(prefix + oneBasedLineNumber.ToString("00"));
        }

        private static bool TryLine(string id, string prefix, string[] lines, out string line)
        {
            if (id.StartsWith(prefix, StringComparison.Ordinal))
            {
                string suffix = id.Substring(prefix.Length);
                if (int.TryParse(suffix, out int lineNumber) && lineNumber >= 1 && lineNumber <= lines.Length)
                {
                    line = lines[lineNumber - 1];
                    return true;
                }
            }
            line = null;
            return false;
        }
    }
}
