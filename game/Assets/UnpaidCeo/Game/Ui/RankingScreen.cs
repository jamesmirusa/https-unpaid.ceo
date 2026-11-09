#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Ranking: the league (from this device's net worth, so it works offline), the top 20 by capital with my place,
    /// market wars for my industry from chapter 2 (my share of the top 10), and my badges. Offline, one notice replaces
    /// the online sections.
    /// </summary>
    public sealed class RankingScreen
    {
        public const int TopCount = 20, MarketCount = 10;

        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly VisualElement _leagueFill, _nextRow;
        readonly Label _nextValue, _myCaption, _share;
        int _ticket;

        public Label LeagueLabel { get; }
        public string? LeagueKey { get; private set; }
        /// <summary>0..1 toward the next league (1 in the top league).</summary>
        public double LeagueProgress { get; private set; }
        public Label OfflineNotice { get; }
        public VisualElement OnlineSections { get; }
        public VisualElement TopRows { get; }
        public Label Empty { get; }
        /// <summary>My place under the list when I am not in the top 20.</summary>
        public Label MyRank { get; }
        public VisualElement MarketSection { get; }
        public VisualElement MarketRows { get; }
        /// <summary>My market-wars score ÷ the sum of the top 10.</summary>
        public double MarketShare { get; private set; }
        public VisualElement BadgeList { get; }
        public Label NoBadges { get; }
        /// <summary>This week's challenge.</summary>
        public ChallengeCard Challenge { get; }

        public RankingScreen(VisualElement root, UiRoot ui, GameEngine engine, GameState state)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("ranking-title"), "ranking.title");
            UiText.Bind(root.Q<Label>("ranking-league-caption"), "ranking.league");
            UiText.Bind(root.Q<Label>("ranking-next-caption"), "ranking.next");
            UiText.Bind(root.Q<Label>("ranking-top-caption"), "ranking.top");
            UiText.Bind(root.Q<Label>("ranking-market-caption"), "ranking.market");
            UiText.Bind(root.Q<Label>("ranking-share-caption"), "ranking.share");
            UiText.Bind(root.Q<Label>("ranking-badges-caption"), "ranking.badges");
            LeagueLabel = root.Q<Label>("ranking-league");
            _leagueFill = root.Q("ranking-league-fill");
            _nextRow = root.Q("ranking-next-row");
            _nextValue = root.Q<Label>("ranking-next");
            OfflineNotice = UiText.Bind(root.Q<Label>("ranking-offline"), "ranking.offline");
            OnlineSections = root.Q("ranking-online");
            TopRows = root.Q("ranking-top");
            Empty = UiText.Bind(root.Q<Label>("ranking-empty"), "ranking.empty");
            _myCaption = UiText.Bind(root.Q<Label>("ranking-me-caption"), "ranking.my_place");
            MyRank = root.Q<Label>("ranking-me");
            MarketSection = root.Q("ranking-market");
            MarketRows = root.Q("ranking-market-list");
            _share = root.Q<Label>("ranking-share");
            BadgeList = root.Q("ranking-badges");
            NoBadges = UiText.Bind(root.Q<Label>("ranking-no-badges"), "ranking.no_badges");
            Challenge = new ChallengeCard(root, ui, () => state.Hero);
            ui.OnlineChanged += _ => { if (_ui.CurrentTab == UiRoot.Ranking) RefreshSoon(); };
            ShowLeague();
            ShowBadges();
        }

        /// <summary>Refreshes without waiting (tab opened, services changed).</summary>
        public void RefreshSoon() => _ = RefreshLogged();

        async Task RefreshLogged()
        {
            try { await Refresh(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public async Task Refresh()
        {
            int ticket = ++_ticket;   // a later refresh wins over a slow earlier one
            ShowLeague();
            ShowBadges();
            var online = _ui.Online;
            Show(OfflineNotice, !online.Available);
            Show(OnlineSections, online.Available);
            if (!online.Available) return;

            string? me = online.Auth.PlayerId;
            if (!await Challenge.Refresh(online, me, () => ticket == _ticket)) return;
            var top = await online.Boards.Top(GameBoards.Capital, TopCount);
            if (ticket != _ticket) return;
            Fill(TopRows, top, me);
            Show(Empty, top.Length == 0);
            var mine = me == null || top.Any(e => e.PlayerId == me) ? null : await online.Boards.Mine(GameBoards.Capital);
            if (ticket != _ticket) return;
            Show(MyRank, mine != null);
            Show(_myCaption, mine != null);
            if (mine != null) UiText.Value(MyRank, $"#{mine.Rank} · {Dollars(mine.Score)}");
            await ShowMarket(online, me, ticket);
        }

        async Task ShowMarket(IOnlineServices online, string? me, int ticket)
        {
            var business = _state.Businesses.FirstOrDefault(b =>
                _engine.Content.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == IndustryKind.Business);
            bool on = _state.Chapter >= 2 && online.Flags.MarketWars && business != null;
            Show(MarketSection, on);
            MarketShare = 0;
            if (!on) return;
            var board = GameBoards.Market(business!.IndustryId);
            var top = await online.Boards.Top(board, MarketCount);
            if (ticket != _ticket) return;
            Fill(MarketRows, top, me);
            double mine = top.FirstOrDefault(e => e.PlayerId == me)?.Score ?? (await online.Boards.Mine(board))?.Score ?? 0;
            if (ticket != _ticket) return;
            double sum = top.Sum(e => e.Score);
            MarketShare = sum > 0 ? mine / sum : 0;
            UiText.Value(_share, (MarketShare * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%");
        }

        void ShowLeague()
        {
            long worth = Chapters.NetWorth(_state, _engine.Content).Cents;
            string league = Leagues.For(worth);
            LeagueKey = "league." + league;
            UiText.Bind(LeagueLabel, LeagueKey);
            long from = Leagues.ThresholdsCents[Math.Max(0, Array.IndexOf(Leagues.All, league))];
            long? next = Leagues.Next(worth);
            LeagueProgress = next.HasValue ? Math.Clamp((worth - from) / (double)(next.Value - from), 0, 1) : 1;
            _leagueFill.style.width = Length.Percent((float)(LeagueProgress * 100));
            Show(_nextRow, next.HasValue);
            if (next.HasValue) UiText.Value(_nextValue, MoneyFormat.Compact(Money.FromCents(next.Value), _ui.Language));
        }

        void ShowBadges()
        {
            BadgeList.Clear();
            IReadOnlyList<string> badges = _ui.Meta?.Badges ?? new List<string>();
            foreach (var id in badges) BadgeList.Add(UiText.Label("badge." + id, "badge"));
            Show(NoBadges, badges.Count == 0);
        }

        void Fill(VisualElement list, IEnumerable<RankEntry> entries, string? me) => FillRows(list, entries, me, _ui.Language);

        /// <summary>Board rows: place, name, score in dollars; my row is marked.</summary>
        internal static void FillRows(VisualElement list, IEnumerable<RankEntry> entries, string? me, string language)
        {
            list.Clear();
            foreach (var e in entries)
            {
                var row = new VisualElement();
                row.AddToClassList("rank-row");
                row.EnableInClassList("rank-row--me", me != null && e.PlayerId == me);
                row.Add(UiText.ValueLabel("#" + e.Rank, "rank-row__rank"));
                row.Add(UiText.ValueLabel(e.Name ?? "—", "rank-row__name"));
                row.Add(UiText.ValueLabel(Dollars(e.Score, language), "rank-row__score"));
                list.Add(row);
            }
        }

        string Dollars(double score) => Dollars(score, _ui.Language);

        static string Dollars(double score, string language) => MoneyFormat.Compact(Money.FromCents((long)Math.Round(score * 100)), language);

        static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
