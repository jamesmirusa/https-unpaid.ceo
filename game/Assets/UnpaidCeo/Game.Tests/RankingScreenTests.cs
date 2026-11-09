#nullable enable
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Tests.Fakes;
using UnpaidCeo.Game.Ui;
using static UnpaidCeo.Game.Tests.OnlineServicesTests;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Ranking: league, top 20, market wars, badges (stage 2B, Task 9).</summary>
    public class RankingScreenTests
    {
        GameObject? _host;
        RenderTexture? _target;
        UiRoot _ui = null!;
        GameEngine _engine = null!;
        GameState _state = null!;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        static ContentDb Content()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) => File.ReadAllText(Path.Combine(dir, f));
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"), Missions = Read("missions.json"),
            });
        }

        IEnumerator Boot(IOnlineServices online, System.Action<GameEngine, GameState>? setup = null)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(11, new HeroAppearance());
            setup?.Invoke(_engine, _state);
            _ui.Initialize(_engine, _state, _target);
            _ui.Online = online;
            yield return _ui.SetLanguage("en");
            _ui.Open(UiRoot.Ranking);
            for (int i = 0; i < 3; i++) yield return null;
        }

        static FakeOnlineServices Me()
        {
            var f = new FakeOnlineServices();
            f.SignInAnonymously().Wait();
            f.AddPlayer(f.Auth.PlayerId!, "Me#0001");
            return f;
        }

        static bool Shown(VisualElement e) => e.resolvedStyle.display != DisplayStyle.None && e.style.display != DisplayStyle.None;

        [UnityTest]
        public IEnumerator League_card_shows_my_league_and_progress()
        {
            yield return Boot(Me(), (e, s) => s.Cash = Money.FromCents(2_500_000));
            yield return Run(() => _ui.RankingPanel.Refresh());
            long worth = Chapters.NetWorth(_state, _engine.Content).Cents;
            string league = Leagues.For(worth);
            Assert.AreEqual("hustler", league, "set-up: $25k is the Hustler league");
            Assert.AreEqual("league." + league, _ui.RankingPanel.LeagueKey);
            Assert.IsTrue(UiText.IsLocalized(_ui.RankingPanel.LeagueLabel));
            long from = Leagues.ThresholdsCents[1], to = Leagues.ThresholdsCents[2];
            Assert.AreEqual((worth - from) / (double)(to - from), _ui.RankingPanel.LeagueProgress, 1e-6);
        }

        [UnityTest]
        public IEnumerator Top_board_highlights_me()
        {
            var fake = Me();
            for (int i = 0; i < 25; i++) fake.SetScore(GameBoards.Capital, "p" + i, 1_000_000 - i * 10_000);
            fake.SetScore(GameBoards.Capital, fake.Auth.PlayerId!, 985_000);   // third place
            yield return Boot(fake);
            yield return Run(() => _ui.RankingPanel.Refresh());

            var rows = _ui.RankingPanel.TopRows.Query(className: "rank-row").ToList();
            Assert.AreEqual(RankingScreen.TopCount, rows.Count);
            Assert.IsTrue(rows[2].ClassListContains("rank-row--me"));
            Assert.AreEqual(1, rows.Count(r => r.ClassListContains("rank-row--me")));
            Assert.IsFalse(Shown(_ui.RankingPanel.MyRank), "my place is in the list already");

            fake = Me();
            for (int i = 0; i < 25; i++) fake.SetScore(GameBoards.Capital, "p" + i, 1_000_000 - i * 10_000);
            fake.SetScore(GameBoards.Capital, fake.Auth.PlayerId!, 5);
            _ui.Online = fake;
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.IsTrue(Shown(_ui.RankingPanel.MyRank), "below the top 20 my place is shown under it");
            StringAssert.Contains("26", _ui.RankingPanel.MyRank.text);
        }

        [UnityTest]
        public IEnumerator Market_wars_show_my_industry_only_from_chapter_2()
        {
            var fake = Me();
            fake.SetScore(GameBoards.Market("coffee_cart"), fake.Auth.PlayerId!, 300);
            fake.SetScore(GameBoards.Market("coffee_cart"), "a", 100);
            fake.SetScore(GameBoards.Market("coffee_cart"), "b", 600);
            fake.SetScore(GameBoards.Market("food_truck"), "c", 9_000);

            yield return Boot(fake);
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.IsFalse(Shown(_ui.RankingPanel.MarketSection), "chapter 1 has no business to compete with");
            Object.Destroy(_host);
            Object.Destroy(_target);

            yield return Boot(fake, (e, s) => { s.Chapter = 2; s.Cash = Money.FromCents(5_000_000); e.ChooseIndustry(s, "coffee_cart"); });
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.IsTrue(Shown(_ui.RankingPanel.MarketSection));
            Assert.AreEqual(3, _ui.RankingPanel.MarketRows.Query(className: "rank-row").ToList().Count, "only my industry's board");
            Assert.AreEqual(0.3, _ui.RankingPanel.MarketShare, 1e-9);

            fake.Flags.MarketWars = false;
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.IsFalse(Shown(_ui.RankingPanel.MarketSection), "the flag turns market wars off");
        }

        [UnityTest]
        public IEnumerator Offline_ranking_shows_one_notice_and_the_league()
        {
            yield return Boot(new OfflineServices());
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.IsTrue(Shown(_ui.RankingPanel.OfflineNotice));
            Assert.IsTrue(UiText.IsLocalized(_ui.RankingPanel.OfflineNotice));
            Assert.IsFalse(Shown(_ui.RankingPanel.OnlineSections));
            Assert.IsTrue(Shown(_ui.RankingPanel.LeagueLabel), "the league is counted on the device");
        }

        [UnityTest]
        public IEnumerator Badges_list_what_I_earned()
        {
            yield return Boot(Me());
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.AreEqual(0, _ui.RankingPanel.BadgeList.Query(className: "badge").ToList().Count);
            Assert.IsTrue(Shown(_ui.RankingPanel.NoBadges));

            _ui.Meta = new MetaProgress { Badges = { Achievements.BreakEven3, Achievements.DuelWinner } };
            yield return Run(() => _ui.RankingPanel.Refresh());
            var badges = _ui.RankingPanel.BadgeList.Query<Label>(className: "badge").ToList();
            Assert.AreEqual(2, badges.Count);
            Assert.IsTrue(badges.All(UiText.IsLocalized));
            Assert.IsFalse(Shown(_ui.RankingPanel.NoBadges));
        }
    }
}
