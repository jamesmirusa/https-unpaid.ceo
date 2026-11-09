#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Ranking → Friends &amp; duels: add a friend by name (Name#1234), accept or decline requests, challenge a friend in
    /// one of the chapter-2 businesses, and the duel list with each state (play the ones waiting for me). Guests are asked
    /// to create an account first. A first won duel grants the duel badge.
    /// </summary>
    public sealed class FriendsPanel
    {
        readonly UiRoot _ui;
        readonly IReadOnlyList<string> _industries;
        readonly Label _noFriends;
        Dictionary<string, string> _names = new Dictionary<string, string>();

        public VisualElement Section { get; }
        public Label NeedAccount { get; }
        public VisualElement Social { get; }
        public TextField NameField { get; }
        public Label Message { get; }
        public string? MessageKey { get; private set; }
        public VisualElement RequestRows { get; }
        public VisualElement FriendRows { get; }
        public VisualElement DuelRows { get; }

        /// <summary>Play a duel run (a 6-month scenario).</summary>
        public event Action<RunStart>? DuelRequested;
        /// <summary>The duel badge was just granted (the game saves its meta record).</summary>
        public event Action? BadgeGranted;

        public FriendsPanel(VisualElement root, UiRoot ui, IReadOnlyList<string> industries)
        {
            _ui = ui;
            _industries = industries;
            Section = root.Q("friends");
            UiText.Bind(root.Q<Label>("friends-caption"), "friends.title");
            NeedAccount = UiText.Bind(root.Q<Label>("friends-need-account"), "friends.need_account");
            Social = root.Q("friends-social");
            UiText.Bind(root.Q<Label>("friends-add-caption"), "friends.add_caption");
            UiText.Bind(root.Q<Label>("friends-requests-caption"), "friends.requests");
            UiText.Bind(root.Q<Label>("duels-caption"), "duel.list");
            NameField = root.Q<TextField>("friends-name");
            var add = UiText.Bind(root.Q<Button>("friends-add"), "friends.add");
            add.clicked += () => _ = AddFriend(NameField.value ?? "");
            Message = root.Q<Label>("friends-message");
            Message.style.display = DisplayStyle.None;
            RequestRows = root.Q("friends-requests");
            FriendRows = root.Q("friends-list");
            DuelRows = root.Q("duels-list");
            _noFriends = UiText.Bind(root.Q<Label>("friends-none"), "friends.none");
        }

        IOnlineServices Online => _ui.Online;

        public async Task Refresh(IOnlineServices online, Func<bool> stillCurrent)
        {
            bool shown = online.Available && online.Flags.Duels, account = shown && !online.Auth.IsGuest;
            Section.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            NeedAccount.style.display = account ? DisplayStyle.None : DisplayStyle.Flex;
            Social.style.display = account ? DisplayStyle.Flex : DisplayStyle.None;
            if (!account) { _ui.ShowDuelWaiting(false); return; }

            var friends = await online.Friends.List();
            var requests = await online.Friends.Requests();
            var duels = await online.Runs.ListDuels();
            if (!stillCurrent()) return;
            _names = friends.Where(f => f.PlayerId != null).GroupBy(f => f.PlayerId!).ToDictionary(g => g.Key, g => g.First().Name ?? "—");
            FillRequests(requests);
            FillFriends(friends);
            FillDuels(duels);
            if (duels.Any(d => d.State == DuelView.Won) && _ui.Meta != null && Achievements.Grant(_ui.Meta, Achievements.DuelWinner))
                BadgeGranted?.Invoke();
            _ui.ShowDuelWaiting(duels.Any(d => d.State == DuelView.WaitingMe));
        }

        Task RefreshNow() => Refresh(Online, () => true);

        public async Task<bool> AddFriend(string name)
        {
            name = name.Trim();
            bool ok = name.Length > 0 && await Online.Friends.AddByName(name);
            Show(ok ? "friends.request_sent" : "friends.not_found");
            if (ok) { NameField.value = ""; await RefreshNow(); }
            return ok;
        }

        public async Task Accept(string playerId) { await Online.Friends.Accept(playerId); await RefreshNow(); }

        public async Task Decline(string playerId) { await Online.Friends.Decline(playerId); await RefreshNow(); }

        /// <summary>Creates a duel with a friend and starts playing my 6 months.</summary>
        public async Task<DuelView> Challenge(string friendId, string industryId)
        {
            var duel = await Online.Runs.CreateDuel(friendId, industryId);
            Show(duel.Error == null ? "duel.created" : "duel.failed");
            await RefreshNow();
            if (duel.Error == null) Play(duel);
            return duel;
        }

        public void Play(DuelView duel) =>
            DuelRequested?.Invoke(new RunStart { Kind = RunStart.Duel, ContextId = duel.Id, IndustryId = duel.IndustryId });

        void FillRequests(IEnumerable<FriendView> requests)
        {
            RequestRows.Clear();
            foreach (var r in requests.Where(r => r.PlayerId != null))
            {
                var row = Row("friend-row", r.Name);
                var id = r.PlayerId!;
                row.Add(Button("friends.accept", () => _ = Accept(id)));
                row.Add(Button("friends.decline", () => _ = Decline(id)));
                RequestRows.Add(row);
            }
        }

        void FillFriends(IReadOnlyCollection<FriendView> friends)
        {
            FriendRows.Clear();
            _noFriends.style.display = friends.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var f in friends.Where(f => f.PlayerId != null))
            {
                var id = f.PlayerId!;
                var row = Row("friend-row", f.Name);
                var picker = new VisualElement();
                picker.AddToClassList("choice-row");
                picker.style.display = DisplayStyle.None;
                foreach (var industry in _industries)
                {
                    var b = Button("industry." + industry + ".name", () => _ = Challenge(id, industry));
                    picker.Add(b);
                }
                row.Add(Button("friends.challenge", () =>
                    picker.style.display = picker.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None));
                FriendRows.Add(row);
                FriendRows.Add(picker);
            }
        }

        void FillDuels(IEnumerable<DuelView> duels)
        {
            DuelRows.Clear();
            foreach (var d in duels)
            {
                var row = new VisualElement { userData = "duel.state." + d.State };
                row.AddToClassList("duel-row");
                row.Add(UiText.ValueLabel(d.OpponentName ?? (d.Opponent != null && _names.TryGetValue(d.Opponent, out var n) ? n : "—"), "duel-row__name"));
                if (d.IndustryId != null) row.Add(UiText.Label("industry." + d.IndustryId + ".name", "duel-row__industry"));
                row.Add(UiText.Label("duel.state." + d.State, "duel-row__state"));
                if (d.State == DuelView.WaitingMe)
                {
                    var play = Button("duel.play", () => Play(d));
                    play.AddToClassList("duel-row__play");
                    row.Add(play);
                }
                DuelRows.Add(row);
            }
        }

        static VisualElement Row(string className, string? name)
        {
            var row = new VisualElement();
            row.AddToClassList(className);
            row.Add(UiText.ValueLabel(name ?? "—", className + "__name"));
            return row;
        }

        static Button Button(string key, Action onClick)
        {
            var b = UiText.Bind(new Button(onClick), key);
            b.AddToClassList("choice-button");
            return b;
        }

        void Show(string key)
        {
            MessageKey = key;
            UiText.Bind(Message, key);
            Message.style.display = DisplayStyle.Flex;
        }
    }
}
