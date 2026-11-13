using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    public class ReplayTests
    {
        [Test]
        public void Replay_reproduces_state_hash()
        {
            var content = TestContent.Db(capacityPerWorkPoint: 3.0, weights: "[1, 2, 1]");
            var e = new GameEngine(content);
            var s = e.NewGame(2026, TestContent.Hero);
            e.TutorialGig(s);
            for (int m = 0; m < 12; m++)
            {
                foreach (var st in e.BeginMonth(s).ToList())
                    e.ChooseStoryletOption(s, st.Id, st.Choices.Keys.OrderBy(k => k).First());
                e.EndMonth(s, TestContent.Decisions(55, 15, 20, 10, m % 3 == 0 ? "overtime" : "network"));
            }
            Assert.AreEqual(13, s.Month);
            var replayed = GameEngine.Replay(content, 2026, TestContent.Hero, s.Log);
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(replayed));
        }
    }
}
