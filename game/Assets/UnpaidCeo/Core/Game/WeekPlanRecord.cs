using System.Collections.Generic;

namespace UnpaidCeo.Core.Game
{
    /// <summary>
    /// What the player decided during the week being played (stage 5A), kept in the meta save next to the game: one tap
    /// is final, so a reload must not undo it. It holds the week it is for; a record of another week is ignored.
    /// </summary>
    public sealed class WeekPlanRecord
    {
        public int Week { get; set; }
        public string Mode { get; set; }
        public bool ModeChosen { get; set; }
        public string Focus { get; set; }
        public bool FocusChosen { get; set; }
        /// <summary>The free actions done this week.</summary>
        public List<string> Actions { get; set; } = new List<string>();
        /// <summary>The business levers stepped this week, with the target each was stepped to.</summary>
        public List<LeverChange> Levers { get; set; } = new List<LeverChange>();
    }
}
