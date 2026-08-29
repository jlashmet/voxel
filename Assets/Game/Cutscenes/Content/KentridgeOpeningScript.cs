using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    public static class KentridgeOpeningScript
    {
        public const int OriginalOpeningLineCount = 31;
        public const int LoganToChurchLineCount = 3;
        public const int AwonOpeningBeatCount = 1;
        public const int SeeMedrareLineCount = 2;
        public const int MedrareFirstSpellLineCount = 23;
        public const int MedrareToChurchLineCount = 1;

        private const string OpeningCuePrefix = "kentridge.pub.opening.line-";
        private const string LoganToChurchCuePrefix = "kentridge.logan.to-church.line-";
        private const string AwonOpeningCuePrefix = "kentridge.awon.opening.line-";
        private const string SeeMedrareCuePrefix = "kentridge.see-medrare.line-";
        private const string MedrareFirstSpellCuePrefix = "kentridge.medrare.first-spell.line-";
        private const string MedrareToChurchCuePrefix = "kentridge.medrare.to-church.line-";

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

        // Retained for compatibility with earlier campaign content; this is not used by the source-faithful
        // Awon -> Medrare opening chain ported by this feature.
        private static readonly string[] LoganToChurchLines =
        {
            "Let's take it easy.  I'll meet you outside the church.",
            "It is just south of the citadel.",
            "Hurry up though, we need to talk to your father."
        };

        // The referenced kentridge-awon-house-back-room.txt payload is absent from the retained and
        // upstream inventory snapshots. Repository integration policy requires this literal placeholder.
        private static readonly string[] AwonOpeningLines =
        {
            "Dialogue coming soon."
        };

        private static readonly string[] SeeMedrareLines =
        {
            "Hi, Michael. Awon told me about you. Come back to my house when you have a moment, I need to ask of you a favor.",
            "And don't forget to bring Michael."
        };

        private static readonly string[] MedrareFirstSpellLines =
        {
            "Hello, Michael",
            "Sorry for not introducing myself further, but I just moved in and things have been very hectic",
            "It took a lot of research, and some effort, but I think I figured out where the building you came from is located",
            "However, I think that there's something more pressing than that right now",
            "When Michael told me what happened earlier today, I spoke with him about it",
            "Apparently, after he led the... zombies... out of town and William eliminated them",
            "Michael muttered a few words in Escher's language and the bodies simply vanished",
            "Obviously, he can't tell me what he said, but I think that something is going to happen soon",
            "He mentioned something about a man in a robe. I fear that it may be Escher, but at the same time, why would Escher be in Kentridge?",
            "Actually, one more thing before I continue",
            "Michael, I've heard... well, exactly nothing about your backstory, even from Michael",
            "But I'm assuming you're not from *around here*",
            "More specifically, I'm assuming you're from a different physical plane",
            "Something separated our two planes",
            "Something brought you here",
            "And that something is breaking. This world should have magic flowing through it",
            "Seriously! Where's all the fire, and the electricity, and the health recovery?!",
            "Speaking of which... do me a favor and brace yourself. I need to try something",
            "medrare attacks you",
            "medrare hits you",
            "What on Earth....",
            "Where's the damage?",
            "Slowly, the room fades away"
        };

        private static readonly string[] MedrareToChurchLines =
        {
            "Michael! William's at the church, and he needs us! Get over there!"
        };

        private static readonly Dictionary<string, string> AdditionalLines = new Dictionary<string, string>
        {
            { "destination-conversation.dialogue", "You made it. Tell me what you found on the road." }
        };

        public static CutsceneCueId CueForOriginalLine(int oneBasedLineNumber) =>
            CueForLine(OpeningCuePrefix, oneBasedLineNumber, OriginalOpeningLineCount);

        public static CutsceneCueId CueForLoganToChurchLine(int oneBasedLineNumber) =>
            CueForLine(LoganToChurchCuePrefix, oneBasedLineNumber, LoganToChurchLineCount);

        public static CutsceneCueId CueForAwonOpeningBeat(int oneBasedLineNumber) =>
            CueForLine(AwonOpeningCuePrefix, oneBasedLineNumber, AwonOpeningBeatCount);

        public static CutsceneCueId CueForSeeMedrareLine(int oneBasedLineNumber) =>
            CueForLine(SeeMedrareCuePrefix, oneBasedLineNumber, SeeMedrareLineCount);

        public static CutsceneCueId CueForMedrareFirstSpellLine(int oneBasedLineNumber) =>
            CueForLine(MedrareFirstSpellCuePrefix, oneBasedLineNumber, MedrareFirstSpellLineCount);

        public static CutsceneCueId CueForMedrareToChurchLine(int oneBasedLineNumber) =>
            CueForLine(MedrareToChurchCuePrefix, oneBasedLineNumber, MedrareToChurchLineCount);

        public static string LineFor(CutsceneCueId cue)
        {
            string id = cue.Value ?? string.Empty;
            if (TryLine(id, OpeningCuePrefix, OriginalOpeningLines, out string line)) return line;
            if (TryLine(id, LoganToChurchCuePrefix, LoganToChurchLines, out line)) return line;
            if (TryLine(id, AwonOpeningCuePrefix, AwonOpeningLines, out line)) return line;
            if (TryLine(id, SeeMedrareCuePrefix, SeeMedrareLines, out line)) return line;
            if (TryLine(id, MedrareFirstSpellCuePrefix, MedrareFirstSpellLines, out line)) return line;
            if (TryLine(id, MedrareToChurchCuePrefix, MedrareToChurchLines, out line)) return line;
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
