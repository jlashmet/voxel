using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Content.Kentridge
{
    /// <summary>
    /// The spoken lines behind the opening cutscene's dialogue cues.
    ///
    /// The cutscene definition deliberately carries cue <em>ids</em> rather than text, so that the
    /// choreography stays free of localisation and of writing. This is the other side of that
    /// boundary: the id to line mapping, kept out of the definition so a rewrite never touches
    /// timing, staging or blocking.
    ///
    /// These lines are stand-ins written to the beats the recovered choreography implies — the
    /// party talking before Logan arrives, Logan arriving, and the conversation that sends them on.
    /// They should be replaced with the recovered script when it is available; the cue ids are the
    /// contract and will not change when they are.
    /// </summary>
    public static class KentridgeOpeningScript
    {
        private static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            {
                "kentridge.pub.opening.before-logan",
                "We can't keep waiting on rumours. If the road north is closed, somebody has to " +
                "go and see it for themselves."
            },
            {
                "kentridge.pub.opening.logan-arrives",
                "Sorry I'm late. You'll want to hear this — and you'll want to be sitting down."
            },
            {
                "kentridge.pub.opening.logan-conversation",
                "The bridge still stands, but nobody's crossed it in a week. Whatever's on the " +
                "far bank, it came from Hightown."
            },
        };

        /// <summary>
        /// The line for a cue, or a visible placeholder naming the cue.
        ///
        /// Returning the cue id rather than an empty string is deliberate: a missing line then
        /// shows up on screen during the scene it belongs to, instead of the dialogue box silently
        /// appearing blank and looking like a rendering fault.
        /// </summary>
        public static string LineFor(CutsceneCueId cue)
        {
            string id = cue.Value ?? string.Empty;
            return Lines.TryGetValue(id, out string line) ? line : "[" + id + "]";
        }
    }
}
