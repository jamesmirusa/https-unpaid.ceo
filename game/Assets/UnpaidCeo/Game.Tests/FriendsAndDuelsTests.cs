#nullable enable
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.Services;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Tests.Fakes;
using UnpaidCeo.Game.Ui;
using static UnpaidCeo.Game.Tests.OnlineServicesTests;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Friends and async duels (stage 2B, Task 11).</summary>
    public class FriendsAndDuelsTests
    {
        const string QueueKey = "unpaidceo.queue.test";
        GameObject? _host;
        RenderTexture? _target;
        UiRoot _ui = null!;
        string? _run;

        static string[] Texts()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            return ContentPack.Files.Select(f => File.ReadAllText(Path.Combine(dir, f + ".json"))).ToArray();
        }

        static readonly System.Lazy<GameEngine> Engine = new System.Lazy<GameEngine>(() => new GameEngine(ContentPack.Load(Texts())));

        [SetUp]
        public void Keep() { _run = PlayerPrefs.GetString(SaveService.RunKey, null); SaveService.DeleteRun(); PlayerPrefs.DeleteKey(QueueKey); }

        [TearDown]
        public void Restore()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
            if (string.IsNullOrEmpty(_run)) PlayerPrefs.DeleteKey(SaveService.RunKey); else PlayerPrefs.SetString(SaveService.RunKey, _run);
            PlayerPrefs.DeleteKey(QueueKey);
        }

        static FakeOnlineServices Account()
        {
            var f = new FakeOnlineServices();
            f.SignInAnonymously().Wait();
            f.SignUp("ceo_one", "Str0ng!pass").Wait();
            f.AddPlayer("rival", "Rival#1234");
            return f;
        }

        IEnumerator Boot(IOnlineServices online)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _ui.Initialize(Engine.Value, Engine.Value.NewGame(13, new HeroAppearance()), _target);
            _ui.Online = online;
            _ui.Meta = new MetaProgress();
            yield return _ui.SetLanguage("en");
            _ui.Open(UiRoot.Ranking);
            yield return Run(() => _ui.RankingPanel.Refresh());
        }

        FriendsPanel Panel => _ui.RankingPanel.Friends;

        [UnityTest]
        public IEnumerator Adding_a_friend_by_name_sends_a_request()
        {
            var fake = Account();
            yield return Boot(fake);
            yield return Run(async () =>
            {
                Assert.IsTrue(await Panel.AddFriend("Rival#1234"));
                Assert.AreEqual("friends.request_sent", Panel.MessageKey);
                Assert.IsFalse(await Panel.AddFriend("Nobody#0000"));
                Assert.AreEqual("friends.not_found", Panel.MessageKey);
            });
        }

        [UnityTest]
        public IEnumerator Accepting_a_request_lists_the_friend()
        {
            var fake = Account();
            fake.QueueRequest("rival");
            yield return Boot(fake);
            Assert.AreEqual(1, Panel.RequestRows.Query(className: "friend-row").ToList().Count);
            yield return Run(async () =>
            {
                await Panel.Accept("rival");
                Assert.AreEqual(0, Panel.RequestRows.Query(className: "friend-row").ToList().Count);
                var friends = Panel.FriendRows.Query<Label>(className: "friend-row__name").ToList();
                Assert.AreEqual("Rival#1234", friends.Single().text);
            });
        }

        [UnityTest]
        public IEnumerator Duel_plays_six_months_and_submits() => Run(async () =>
        {
            var fake = Account();
            var duel = await fake.Runs.CreateDuel("rival", "coffee_cart");
            var runs = new RunController(Engine.Value, new SubmissionQueue(QueueKey), () => fake, ContentVersion.Of(Texts()));
            var run = runs.Begin(new RunStart { Kind = RunStart.Duel, ContextId = duel.Id, IndustryId = "coffee_cart", Hero = new HeroAppearance() });
            Assert.AreEqual(Duels.For(duel.Id, "coffee_cart").Months, run.Months);
            Assert.AreEqual(6, run.Months);
            for (int i = 0; i < 6; i++) runs.PlayMonth(MonthDecisions.Default(run.State.Businesses[0].Id));

            var outcome = await runs.Finish();
            Assert.IsFalse(outcome!.Queued);
            Assert.AreEqual(DuelView.WaitingThem, outcome.Duel!.State);
            Assert.AreEqual(1, (await fake.Runs.ListDuels()).Count(d => d.MyNetWorthCents.HasValue));
        });

        [UnityTest]
        public IEnumerator Duel_list_shows_won_lost_waiting_expired()
        {
            var fake = Account();
            fake.AddByName("Rival#1234").Wait();
            foreach (var (id, state) in new[] { ("d1", DuelView.Won), ("d2", DuelView.Lost), ("d3", DuelView.WaitingMe), ("d4", DuelView.Expired) })
                fake.PutDuel(new DuelView { Id = id, Opponent = "rival", IndustryId = "food_truck", State = state, CreatedUnix = fake.NowUnix });
            yield return Boot(fake);

            var rows = Panel.DuelRows.Query(className: "duel-row").ToList();
            Assert.AreEqual(4, rows.Count);
            var states = rows.Select(r => r.Q<Label>(className: "duel-row__state")).ToList();
            CollectionAssert.AreEquivalent(new[] { "duel.state.won", "duel.state.lost", "duel.state.waiting_me", "duel.state.expired" },
                rows.Select(r => (string)r.userData).ToArray());
            Assert.IsTrue(states.All(UiText.IsLocalized));
            Assert.AreEqual("Rival#1234", rows[0].Q<Label>(className: "duel-row__name").text, "the opponent's name comes from the friends list");
            var playable = rows.Where(r => r.Q<Button>(className: "duel-row__play") != null).ToList();
            Assert.AreEqual(1, playable.Count, "only the duel waiting for me can be played");

            RunStart? requested = null;
            Panel.DuelRequested += s => requested = s;
            Panel.Play(new DuelView { Id = "d3", IndustryId = "food_truck", State = DuelView.WaitingMe });
            Assert.AreEqual(RunStart.Duel, requested!.Kind);
            Assert.AreEqual("d3", requested.ContextId);
            Assert.AreEqual("food_truck", requested.IndustryId);
        }

        [UnityTest]
        public IEnumerator Guest_is_asked_to_create_an_account_first()
        {
            var guest = new FakeOnlineServices();
            guest.SignInAnonymously().Wait();
            yield return Boot(guest);
            Assert.AreEqual(DisplayStyle.Flex, Panel.NeedAccount.style.display.value);
            Assert.IsTrue(UiText.IsLocalized(Panel.NeedAccount));
            Assert.AreEqual(DisplayStyle.None, Panel.Social.style.display.value, "no friend list for a guest");
        }

        [UnityTest]
        public IEnumerator First_duel_win_grants_the_badge()
        {
            var fake = Account();
            fake.PutDuel(new DuelView { Id = "d1", Opponent = "rival", IndustryId = "coffee_cart", State = DuelView.Won, CreatedUnix = fake.NowUnix });
            int granted = 0;
            yield return Boot(fake);
            Panel.BadgeGranted += () => granted++;
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.Contains(Achievements.DuelWinner, _ui.Meta.Badges);
            yield return Run(() => _ui.RankingPanel.Refresh());
            Assert.AreEqual(1, _ui.Meta.Badges.Count(b => b == Achievements.DuelWinner), "granted once");
        }
    }
}
