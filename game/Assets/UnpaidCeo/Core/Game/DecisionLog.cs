namespace UnpaidCeo.Core.Game
{
    /// <summary>One replayable player action. Payload is canonical JSON.</summary>
    public sealed class DecisionLogEntry
    {
        public const string TutorialGig = "tutorialGig";
        public const string BeginMonth = "beginMonth";
        public const string StoryletChoice = "storyletChoice";
        public const string EndMonth = "endMonth";

        public int Month { get; set; }
        public string Kind { get; set; }
        public string Payload { get; set; }
    }
}
