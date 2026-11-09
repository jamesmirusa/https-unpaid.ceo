using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Online
{
    /// <summary>
    /// How a run began: a career (NewGame from the seed) or a scenario (weekly challenge, duel) built from a
    /// <see cref="ScenarioDef"/>. With the decision log it is everything needed to replay the run.
    /// </summary>
    public sealed class RunStart
    {
        public const string Career = "career", Challenge = "challenge", Duel = "duel";

        public string Kind { get; set; } = Career;
        public ulong Seed { get; set; }
        public HeroAppearance Hero { get; set; } = new HeroAppearance();
        /// <summary>The challenge week (as text) or the duel id.</summary>
        public string ContextId { get; set; }
        /// <summary>The scenario's industry (duels; challenges derive it from the week).</summary>
        public string IndustryId { get; set; }
    }
}
