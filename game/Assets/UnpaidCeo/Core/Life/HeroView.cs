using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Life
{
    /// <summary>One of the hero's four needs: its value, its bar (0–100) and whether it is low.</summary>
    public sealed class Need
    {
        public string Id { get; set; }
        public int Value { get; set; }
        public int Bar { get; set; }
        public bool Low { get; set; }
    }

    /// <summary>A mood badge: it explains the state ("Rested +", "Debt to the loan shark −"); it never changes the game.</summary>
    public sealed class Mood
    {
        public string Id { get; set; }
        public bool Good { get; set; }
        /// <summary>Weeks the badge stays (null = while the cause lasts).</summary>
        public int? WeeksLeft { get; set; }
    }

    /// <summary>A "want" of the week: the next real step toward the chapter's goal; ticked when the week's plan has it.</summary>
    public sealed class Want
    {
        public string Id { get; set; }
        /// <summary>A host, a lesson or a knowledge number the line names.</summary>
        public string Arg { get; set; }
        public bool Done { get; set; }
    }

    /// <summary>
    /// Stage 5B · the hero's state as the HUD shows it: four needs, up to four mood badges and three wants. Pure reads —
    /// they explain and lead, they add no second economy (spec: «объясняют, не меняют баланс»).
    /// </summary>
    public static class HeroView
    {
        public const int MaxMoods = 4, MaxWants = 3, RestedFrom = 80, LonelyBelow = 30, TiredBelow = 40;
        static readonly string[] TalkHosts = { "tony", "mom" };

        public static List<Need> Needs(GameState s)
        {
            int burnout = 30;
            return new List<Need>
            {
                new Need { Id = "energy", Value = s.Life.Energy, Bar = s.Life.Energy, Low = s.Life.Energy < burnout },
                new Need { Id = "relationships", Value = s.Life.Relationships, Bar = s.Life.Relationships, Low = s.Life.Relationships < LonelyBelow },
                new Need { Id = "knowledge", Value = s.StudyXp, Bar = System.Math.Min(100, System.Math.Max(0, s.StudyXp)), Low = false },
                new Need { Id = "reputation", Value = s.Life.Reputation, Bar = s.Life.Reputation, Low = s.Life.Reputation < 20 },
            };
        }

        public static List<Mood> Moods(GameState s, Balance b, MonthReport last)
        {
            var all = new List<Mood>();
            if (s.Life.Energy < b.Life.BurnoutThreshold) all.Add(new Mood { Id = "burnout" });
            if (s.Facts.Has("shark_debt")) all.Add(new Mood { Id = "shark_debt" });
            if (s.Cash.Cents < 0) all.Add(new Mood { Id = "in_debt" });
            if (s.Life.Relationships < LonelyBelow) all.Add(new Mood { Id = "lonely" });
            if (last != null && last.Revenue.Cents > 0) all.Add(new Mood { Id = "paycheck", Good = true, WeeksLeft = 1 });
            if (s.Life.Energy >= RestedFrom) all.Add(new Mood { Id = "rested", Good = true, WeeksLeft = 1 });
            if (s.Reserve.Cents > 0) all.Add(new Mood { Id = "cushion", Good = true });
            return all.OrderBy(m => m.Good ? 1 : 0).Take(MaxMoods).ToList();
        }

        public static List<Want> Wants(GameState s, ContentDb c, IEnumerable<string> planned)
        {
            var plan = planned.ToList();
            var wants = new List<Want>();
            foreach (var host in TalkHosts)
            {
                bool talked = s.HostTalkWeek.TryGetValue(host, out var w) && w == s.Week;
                if (talked || HostScenes.HasScene(s, c, host)) wants.Add(new Want { Id = "talk", Arg = host, Done = talked });
            }
            void Deed(string want, string action, bool wanted = true)
            {
                if (!wanted || !c.FreeActions.ContainsKey(action)) return;
                var st = ActionChoices.State(s, c, plan, action);
                if (st == ChoiceState.Planned) wants.Add(new Want { Id = want, Done = true });
                else if (st == ChoiceState.Open) wants.Add(new Want { Id = want });
            }
            Deed("reserve", "reserve_transfer", s.Chapter == 1);
            var lesson = OpenLesson(s, c);
            if (lesson != null) wants.Add(new Want { Id = "lesson", Arg = lesson.TitleKey });
            Deed("rest", "rest", s.Life.Energy < TiredBelow);
            Deed("earn", "overtime");
            var gig = NextGig(s, c);
            if (gig != null) Deed("study", "study");
            var studyWant = wants.FirstOrDefault(x => x.Id == "study");
            if (studyWant != null) studyWant.Arg = gig.RequiredStudy.ToString();
            return wants.Take(MaxWants).ToList();
        }

        /// <summary>A lesson the hero has opened but not answered every question of.</summary>
        static LessonDef OpenLesson(GameState s, ContentDb c) =>
            c.Lessons.Values.Where(l => s.LessonsSeen.ContainsKey(l.Id))
                .FirstOrDefault(l => Enumerable.Range(0, l.Quiz.Count).Any(i => !s.QuizAnswers.ContainsKey(l.Id + "/" + i)));

        /// <summary>The cheapest gig the hero cannot do yet for lack of knowledge.</summary>
        static IndustryDef NextGig(GameState s, ContentDb c) =>
            c.Industries.Values.Where(d => d.Kind == IndustryKind.Gig && d.RequiredStudy > s.StudyXp)
                .OrderBy(d => d.RequiredStudy).FirstOrDefault();
    }
}
