using System.Linq;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Tests.Game;

namespace UnpaidCeo.Core.Tests.Online
{
    /// <summary>Honest runs for the online tests: the optimal bot plays through GameEngine, so every action is logged.</summary>
    internal static class OnlineRuns
    {
        public static ScenarioDef NoScenario(RunStart r) => null;

        public static RunStart Career(GameState s) => new RunStart { Kind = RunStart.Career, Seed = s.Seed, Hero = s.Hero };

        public static (GameEngine e, GameState s, MonthReport last) PlayCareer(ulong seed, int months, string industry = "coffee_cart")
        {
            var e = new GameEngine(BotRunner.Content());
            var s = e.NewGame(seed, TestContent.Hero);
            var bot = new OptimalBot();
            MonthReport last = null;
            e.TutorialGig(s);
            while (s.Month <= months)
            {
                foreach (var scene in e.BeginMonth(s).ToList())
                    e.ChooseStoryletOption(s, scene.Id, bot.PickChoice(e, s, scene));
                last = e.EndMonth(s, bot.Decide(e, s));
                bot.AfterMonth(e, s, last);
                if (e.Chapter(s).GoalMet && s.Chapter < Chapters.LastChapter && e.AdvanceChapter(s) && s.Chapter == 2)
                    bot.StartChapter2(e, s, industry);
            }
            return (e, s, last);
        }
    }
}
