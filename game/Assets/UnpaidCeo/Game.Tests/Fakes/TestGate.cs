using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Does what the stage-4 checklist asks before "Next month", the way a player would: opens the plan, drops by home
    /// and, on a study month, answers the open academy questions. Tests that are about something else call this.
    /// </summary>
    public static class TestGate
    {
        public static void Satisfy(UiRoot ui, ContentDb content, GameState state)
        {
            ui.Open(UiRoot.Decisions);
            // Looking is not deciding: the week's mode is tapped (last week's, to keep it).
            if (!ui.Gate.PlanReviewed) ui.DecisionsPanel.ChooseMode(ui.DecisionsPanel.Mode ?? "balance");
            ui.OpenPlace(Places.Home);
            foreach (var lesson in content.LessonsInOrder)
            {
                if (!state.LessonsSeen.ContainsKey(lesson.Id)) continue;
                for (int q = 0; q < lesson.Quiz.Count && ui.Gate.AnswersGiven < ui.Gate.AnswersNeeded; q++)
                    if (!state.QuizAnswers.ContainsKey(lesson.Id + "/" + q)) ui.AcademyPanel.Answer(lesson.Id, q, lesson.Quiz[q].CorrectIndex);
            }
            ui.Open(UiRoot.City);
        }
    }
}
