using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>Decisions tab: CEO time split, work split between gigs, and free actions within this month's action points.</summary>
    public sealed class DecisionsScreen
    {
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly TimeAllocationControl _time;
        readonly ShareControl _split;
        readonly List<string> _gigIds;
        readonly Label _ap;
        readonly Dictionary<string, Toggle> _toggles = new Dictionary<string, Toggle>();

        public DecisionsScreen(VisualElement root, GameEngine engine, GameState state)
        {
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("decisions-title"), "decisions.title");
            UiText.Bind(root.Q<Label>("time-title"), "decisions.time");
            UiText.Bind(root.Q<Label>("split-title"), "decisions.split");
            UiText.Bind(root.Q<Label>("actions-title"), "decisions.actions");
            UiText.Bind(root.Q<Label>("ap-caption"), "decisions.ap");
            _ap = root.Q<Label>("ap-value");

            var last = state.LastDecisions ?? MonthDecisions.Default(state.Businesses.FirstOrDefault()?.Id);
            _time = new TimeAllocationControl(last.Work, last.Family, last.Rest, last.Study);
            root.Q("time-control").Add(_time);

            _gigIds = state.Businesses.Select(b => b.Id).ToList();
            var split = _gigIds.Select(id => last.WorkSplit != null && last.WorkSplit.TryGetValue(id, out var v) ? v : 0).ToArray();
            if (split.Sum() != 100 && split.Length > 0) split = TimeAllocation.Rebalance(new int[split.Length], 0, 100);
            _split = new ShareControl(state.Businesses.Select(b => "gig." + b.IndustryId).ToList(), split) { name = "work-split" };
            root.Q("split-control").Add(_split);

            var actions = root.Q("actions");
            foreach (var a in engine.Content.FreeActions.Values)
            {
                var row = new VisualElement();
                row.AddToClassList("action-row");
                var toggle = new Toggle { name = "action-" + a.Id };
                toggle.RegisterValueChangedCallback(_ => RefreshPoints());
                _toggles[a.Id] = toggle;
                row.Add(toggle);
                row.Add(UiText.Label("action." + a.Id, "action-row__label"));
                row.Add(UiText.ValueLabel(a.ActionPoints.ToString(), "action-row__cost"));
                actions.Add(row);
            }
            RefreshPoints();
        }

        int Available => LifeSimulator.ActionPoints(_state.Life, 0, _engine.Content.Balance.Life);

        public void Refresh()
        {
            foreach (var t in _toggles.Values) t.SetValueWithoutNotify(false);
            RefreshPoints();
        }

        void RefreshPoints()
        {
            int used = _toggles.Where(t => t.Value.value).Sum(t => _engine.Content.FreeActions[t.Key].ActionPoints);
            int left = Available - used;
            _ap.text = $"{left} / {Available}";
            foreach (var t in _toggles)
                t.Value.SetEnabled(t.Value.value || _engine.Content.FreeActions[t.Key].ActionPoints <= left);
        }

        public MonthDecisions Build()
        {
            var shares = _time.Values;
            var d = new MonthDecisions { Work = shares[0], Family = shares[1], Rest = shares[2], Study = shares[3] };
            var split = _split.Values;
            for (int i = 0; i < _gigIds.Count; i++) d.WorkSplit[_gigIds[i]] = split[i];
            d.FreeActions.AddRange(_toggles.Where(t => t.Value.value).Select(t => t.Key));
            return d;
        }
    }
}
