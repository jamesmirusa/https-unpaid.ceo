#nullable enable
using System;
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
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.Services;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Tests.Fakes;
using UnpaidCeo.Game.Ui;
using static UnpaidCeo.Game.Tests.OnlineServicesTests;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Weekly challenge runs (stage 2B, Task 10).</summary>
    public class ScenarioRunTests
    {
        const string QueueKey = "unpaidceo.queue.test";
        string? _save, _meta, _run;
        GameObject? _host;
        RenderTexture? _target;

        static string[] Texts()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            return ContentPack.Files.Select(f => File.ReadAllText(Path.Combine(dir, f + ".json"))).ToArray();
        }

        static readonly Lazy<GameEngine> Engine = new Lazy<GameEngine>(() => new GameEngine(ContentPack.Load(Texts())));
        static readonly Lazy<string> Version = new Lazy<string>(() => ContentVersion.Of(Texts()));

        [SetUp]
        public void Keep()
        {
            _save = PlayerPrefs.GetString(SaveService.Key, null);
            _meta = PlayerPrefs.GetString(SaveService.MetaKey, null);
            _run = PlayerPrefs.GetString(SaveService.RunKey, null);
            SaveService.DeleteRun();
            PlayerPrefs.DeleteKey(QueueKey);
        }

        [TearDown]
        public void Restore()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            if (_target != null) UnityEngine.Object.Destroy(_target);
            void Put(string key, string? value) { if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, value); }
            Put(SaveService.Key, _save);
            Put(SaveService.MetaKey, _meta);
            Put(SaveService.RunKey, _run);
            PlayerPrefs.DeleteKey(QueueKey);
        }

        static RunStart Challenge(FakeOnlineServices f) => new RunStart
        {
            Kind = RunStart.Challenge, ContextId = WeeklyChallenge.WeekIndex(f.NowUnix).ToString(), Hero = new HeroAppearance(),
        };

        static FakeOnlineServices Online()
        {
            var f = new FakeOnlineServices();
            f.SignInAnonymously().Wait();
            return f;
        }

        RunController Runs(FakeOnlineServices f) => new RunController(Engine.Value, new SubmissionQueue(QueueKey), () => f, Version.Value);

        static void Play(RunController runs, int months)
        {
            for (int i = 0; i < months; i++)
                runs.PlayMonth(MonthDecisions.Default(runs.Current!.State.Businesses[0].Id));
        }

        [Test]
        public void Challenge_run_uses_no_daily_turns_and_leaves_the_career_alone()
        {
            var career = Engine.Value.NewGame(3, new HeroAppearance());
            career.Month = 7;
            SaveService.Save(career);
            var meta = new PlayerMeta();
            meta.Turns.ManualLeft = 6;
            SaveService.SaveMeta(meta);
            string careerSave = PlayerPrefs.GetString(SaveService.Key), metaSave = PlayerPrefs.GetString(SaveService.MetaKey);

            var fake = Online();
            var runs = Runs(fake);
            var run = runs.Begin(Challenge(fake));
            var def = Scenarios.For(run.Start, Engine.Value.Content.Balance)!;
            Assert.AreEqual(2, run.State.Chapter);
            Assert.IsTrue(run.State.Businesses.Any(b => b.IndustryId == def.IndustryId), "the scenario's business is set up");
            Assert.AreEqual(12, run.Months);

            Play(runs, 2);
            Assert.AreEqual(2, run.MonthsDone);
            Assert.AreEqual(careerSave, PlayerPrefs.GetString(SaveService.Key), "the career save is untouched");
            Assert.AreEqual(metaSave, PlayerPrefs.GetString(SaveService.MetaKey), "no daily turns were spent");
            Assert.IsTrue(PlayerPrefs.HasKey(SaveService.RunKey), "the run has its own slot");
        }

        [UnityTest]
        public IEnumerator Twelfth_month_submits_once_and_returns_to_the_career() => Run(async () =>
        {
            var fake = Online();
            var runs = Runs(fake);
            var start = Challenge(fake);
            runs.Begin(start);
            Play(runs, 12);
            Assert.IsTrue(runs.Current!.Finished);
            Assert.Throws<InvalidOperationException>(() => Play(runs, 1), "a finished run plays no more months");

            var outcome = await runs.Finish();
            Assert.IsNotNull(outcome);
            Assert.IsFalse(outcome!.Queued);
            Assert.IsTrue(outcome.Result!.Ok);
            Assert.AreEqual(1, fake.Challenges.Count);
            Assert.AreEqual(start.ContextId, fake.Challenges[0].Start.ContextId);
            Assert.AreEqual(12, fake.Challenges[0].Log.Count(e => e.Kind == DecisionLogEntry.EndMonth), "twelve months in the log");
            Assert.IsNull(runs.Current, "back to the career");
            Assert.IsFalse(PlayerPrefs.HasKey(SaveService.RunKey));

            Assert.IsNull(await runs.Finish(), "nothing left to submit");
            Assert.AreEqual(1, fake.Challenges.Count, "submitted once");
        });

        [Test]
        public void Leaving_and_resuming_a_run_keeps_its_months()
        {
            var fake = Online();
            var runs = Runs(fake);
            var start = Challenge(fake);
            runs.Begin(start);
            Play(runs, 3);
            runs.Leave();
            Assert.IsNull(runs.Current);

            var later = Runs(fake);   // a relaunch
            Assert.AreEqual(3, RunController.Saved()!.MonthsDone, "the card can offer to continue");
            var resumed = later.Begin(start);
            Assert.AreEqual(3, resumed.MonthsDone);
            Assert.AreEqual(start.ContextId, resumed.Start.ContextId);
        }

        [UnityTest]
        public IEnumerator Offline_challenge_result_is_queued() => Run(async () =>
        {
            var fake = Online();
            var queue = new SubmissionQueue(QueueKey);
            var runs = new RunController(Engine.Value, queue, () => fake, Version.Value);
            runs.Begin(Challenge(fake));
            Play(runs, 12);
            fake.Online = false;

            var outcome = await runs.Finish();
            Assert.IsTrue(outcome!.Queued);
            Assert.AreEqual(RunStart.Challenge, queue.Entries.Single().Kind);
            Assert.AreEqual(0, fake.Challenges.Count);

            fake.Online = true;
            await queue.Flush(fake.Runs, fake.Auth.PlayerId);
            Assert.AreEqual(1, fake.Challenges.Count);
        });

        [UnityTest]
        public IEnumerator Challenge_card_shows_attempts_left()
        {
            var fake = Online();
            int week = WeeklyChallenge.WeekIndex(fake.NowUnix);
            fake.HelloChallenge = WeeklyChallenge.For(week, Engine.Value.Content.Balance);
            fake.ChallengeAttemptsUsed = 1;
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            var ui = _host.AddComponent<UiRoot>();
            ui.Initialize(Engine.Value, Engine.Value.NewGame(5, new HeroAppearance()), _target);
            ui.Online = fake;
            yield return ui.SetLanguage("en");
            ui.Open(UiRoot.Ranking);
            yield return Run(() => ui.RankingPanel.Refresh());

            var card = ui.RankingPanel.Challenge;
            Assert.AreEqual(DisplayStyle.Flex, card.Card.style.display.value);
            Assert.AreEqual("2", card.AttemptsLeft.text);
            Assert.IsTrue(UiText.IsLocalized(card.Industry), "this week's industry is named");
            Assert.IsTrue(card.PlayButton.enabledSelf);
            RunStart? requested = null;
            ui.RankingPanel.Challenge.Requested += s => requested = s;
            card.Play();
            Assert.AreEqual(week.ToString(), requested!.ContextId);

            fake.ChallengeAttemptsUsed = 3;
            yield return Run(() => ui.RankingPanel.Refresh());
            Assert.AreEqual("0", card.AttemptsLeft.text);
            Assert.IsFalse(card.PlayButton.enabledSelf, "no attempts left this week");
        }
    }
}
