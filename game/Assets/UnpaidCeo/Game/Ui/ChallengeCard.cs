#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Ranking → this week's challenge: industry and start cash, attempts left (from the server), my best, the top 10,
    /// and Play (or Continue for a run left half-way). Hidden offline, without a challenge, or with the flag off.
    /// </summary>
    public sealed class ChallengeCard
    {
        public const int TopCount = 10;

        readonly UiRoot _ui;
        readonly Func<HeroAppearance> _hero;
        HelloResult? _hello;
        int _left;

        public VisualElement Card { get; }
        public Label Industry { get; }
        public Label StartCash { get; }
        public Label AttemptsLeft { get; }
        public Label Best { get; }
        public Button PlayButton { get; }
        public VisualElement Top { get; }

        /// <summary>The player chose to play (or continue) this week's challenge.</summary>
        public event Action<RunStart>? Requested;

        public ChallengeCard(VisualElement root, UiRoot ui, Func<HeroAppearance> hero)
        {
            _ui = ui;
            _hero = hero;
            Card = root.Q("challenge-card");
            UiText.Bind(root.Q<Label>("challenge-caption"), "challenge.title");
            UiText.Bind(root.Q<Label>("challenge-start-caption"), "challenge.start");
            UiText.Bind(root.Q<Label>("challenge-attempts-caption"), "challenge.attempts");
            UiText.Bind(root.Q<Label>("challenge-best-caption"), "challenge.best");
            Industry = root.Q<Label>("challenge-industry");
            StartCash = root.Q<Label>("challenge-start");
            AttemptsLeft = root.Q<Label>("challenge-attempts");
            Best = root.Q<Label>("challenge-best");
            Top = root.Q("challenge-top");
            PlayButton = UiText.Bind(root.Q<Button>("challenge-play"), "challenge.play");
            PlayButton.clicked += Play;
            Card.style.display = DisplayStyle.None;
        }

        public void Play()
        {
            if (_hello?.Challenge == null || _left <= 0) return;
            Requested?.Invoke(new RunStart { Kind = RunStart.Challenge, ContextId = _hello.Week.ToString(), Hero = _hero() });
        }

        /// <returns>False when a later refresh made this one stale.</returns>
        public async Task<bool> Refresh(IOnlineServices online, string? me, Func<bool> stillCurrent)
        {
            _hello = online.Available && online.Flags.Challenge ? await online.Runs.Hello() : null;
            if (!stillCurrent()) return false;
            var def = _hello?.Challenge;
            Card.style.display = def != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (def == null) return true;

            _left = Math.Max(0, WeeklyChallenge.AttemptsPerWeek - _hello!.ChallengeAttemptsUsed);
            UiText.Bind(Industry, "industry." + def.IndustryId + ".name");
            UiText.Value(StartCash, MoneyFormat.Compact(def.StartCash, _ui.Language));
            UiText.Value(AttemptsLeft, _left.ToString());
            var saved = RunController.Saved();
            bool resumable = saved != null && saved.Start.Kind == RunStart.Challenge && saved.Start.ContextId == _hello.Week.ToString();
            UiText.Bind(PlayButton, resumable ? "challenge.continue" : "challenge.play");
            PlayButton.SetEnabled(_left > 0);

            var top = await online.Boards.Top(GameBoards.WeeklyChallenge, TopCount);
            var mine = await online.Boards.Mine(GameBoards.WeeklyChallenge);
            if (!stillCurrent()) return false;
            RankingScreen.FillRows(Top, top, me, _ui.Language);
            UiText.Value(Best, mine != null ? MoneyFormat.Compact(Money.FromCents((long)Math.Round(mine.Score * 100)), _ui.Language) : "—");
            return true;
        }
    }
}
