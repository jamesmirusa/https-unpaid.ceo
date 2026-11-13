namespace UnpaidCeo.Core.Game
{
    /// <summary>One replayable player action. Payload is canonical JSON.</summary>
    public sealed class DecisionLogEntry
    {
        public const string TutorialGig = "tutorialGig";
        public const string BeginMonth = "beginMonth";
        public const string StoryletChoice = "storyletChoice";
        public const string EndMonth = "endMonth";
        /// <summary>Stage 4B: the turn is a week.</summary>
        public const string BeginWeek = "beginWeek";
        public const string EndWeek = "endWeek";
        public const string QuizAnswer = "quizAnswer";
        public const string TakeLoan = "takeLoan";
        public const string ChooseIndustry = "chooseIndustry";
        public const string AdvanceChapter = "advanceChapter";

        public int Month { get; set; }
        public string Kind { get; set; }
        public string Payload { get; set; }
    }
}
