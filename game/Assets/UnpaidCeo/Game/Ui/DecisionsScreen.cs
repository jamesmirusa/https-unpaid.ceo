using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Decisions tab: CEO time split, work split between gigs, units and staff per business (chapters 2–3),
    /// and free actions within this month's action points.
    /// </summary>
    public sealed class DecisionsScreen
    {
        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly TimeAllocationControl _time;
        readonly ShareControl _split;
        readonly List<string> _gigIds;
        readonly Label _ap, _businessTitle;
        readonly VisualElement _business;
        readonly Dictionary<string, Toggle> _toggles = new Dictionary<string, Toggle>();
        readonly Dictionary<string, int> _units = new Dictionary<string, int>(), _staff = new Dictionary<string, int>();
        readonly Dictionary<string, (Label units, Label staff, Label rejected)> _rows = new Dictionary<string, (Label, Label, Label)>();
        string _businessKey = "";

        public DecisionsScreen(VisualElement root, UiRoot ui, GameEngine engine, GameState state)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("decisions-title"), "decisions.title");
            UiText.Bind(root.Q<Label>("time-title"), "decisions.time");
            UiText.Bind(root.Q<Label>("split-title"), "decisions.split");
            UiText.Bind(root.Q<Label>("actions-title"), "decisions.actions");
            UiText.Bind(root.Q<Label>("ap-caption"), "decisions.ap");
            _businessTitle = UiText.Bind(root.Q<Label>("business-title"), "decisions.business");
            _business = root.Q("business-controls");
            _ap = root.Q<Label>("ap-value");

            var last = state.LastDecisions ?? MonthDecisions.Default(state.Businesses.FirstOrDefault()?.Id);
            _time = new TimeAllocationControl(last.Work, last.Family, last.Rest, last.Study);
            root.Q("time-control").Add(_time);

            var gigs = state.Businesses.Where(b => KindOf(b) == IndustryKind.Gig).ToList();
            _gigIds = gigs.Select(b => b.Id).ToList();
            var split = _gigIds.Select(id => last.WorkSplit != null && last.WorkSplit.TryGetValue(id, out var v) ? v : 0).ToArray();
            if (split.Sum() != 100 && split.Length > 0) split = TimeAllocation.Rebalance(new int[split.Length], 0, 100);
            _split = new ShareControl(gigs.Select(b => "gig." + b.IndustryId).ToList(), split) { name = "work-split" };
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
            RefreshBusinesses();
            RefreshPoints();
        }

        IndustryKind? KindOf(BusinessState b) => _engine.Content.Industries.TryGetValue(b.IndustryId, out var d) ? d.Kind : (IndustryKind?)null;

        IEnumerable<BusinessState> Businesses => _state.Businesses.Where(b => KindOf(b) == IndustryKind.Business);

        int Available => LifeSimulator.ActionPoints(_state.Life, 0, _engine.Content.Balance.Life);

        public void Refresh()
        {
            foreach (var t in _toggles.Values) t.SetValueWithoutNotify(false);
            RefreshBusinesses();
            RefreshPoints();
        }

        /// <summary>Rebuilds the units and staff steppers when businesses change, and resets targets to the current counts.</summary>
        void RefreshBusinesses()
        {
            var list = Businesses.ToList();
            var key = string.Join(",", list.Select(b => b.Id));
            if (key != _businessKey)
            {
                _businessKey = key;
                _business.Clear();
                _rows.Clear();
                foreach (var b in list) _business.Add(BusinessRow(b));
            }
            bool any = list.Count > 0;
            _businessTitle.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _business.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var b in list)
            {
                _units[b.Id] = b.Units;
                _staff[b.Id] = b.Employees;
                if (_rows.TryGetValue(b.Id, out var r)) r.rejected.style.display = DisplayStyle.None;
                Show(b.Id);
            }
        }

        VisualElement BusinessRow(BusinessState b)
        {
            var def = _engine.Content.Industries[b.IndustryId];
            var box = new VisualElement { name = "business-" + b.Id };
            box.AddToClassList("business-box");
            box.Add(UiText.Label($"industry.{b.IndustryId}.name", "business-box__name"));
            var units = Stepper(box, "decisions.units", "units", b.Id, d => StepUnits(b.Id, d));
            var cost = new VisualElement();
            cost.AddToClassList("row");
            cost.Add(UiText.Label("decisions.unit_cost", "row__label"));
            cost.Add(UiText.ValueLabel(MoneyFormat.Compact(def.Capex, _ui.Language), "row__value"));
            box.Add(cost);
            var staff = Stepper(box, "decisions.staff", "staff", b.Id, d => StepStaff(b.Id, d));
            var rejected = UiText.Label("decisions.units_rejected", "decisions__warning");
            rejected.name = "units-rejected-" + b.Id;
            rejected.style.display = DisplayStyle.None;
            box.Add(rejected);
            _rows[b.Id] = (units, staff, rejected);
            return box;
        }

        static Label Stepper(VisualElement parent, string key, string what, string id, Action<int> step)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.AddToClassList("stepper");
            row.Add(UiText.Label(key, "row__label"));
            var minus = UiText.Value(new Button(() => step(-1)) { name = $"{what}-minus-{id}" }, "−");
            minus.AddToClassList("stepper__button");
            var value = UiText.ValueLabel("", "stepper__value");
            value.name = $"{what}-value-{id}";
            var plus = UiText.Value(new Button(() => step(+1)) { name = $"{what}-plus-{id}" }, "+");
            plus.AddToClassList("stepper__button");
            row.Add(minus);
            row.Add(value);
            row.Add(plus);
            parent.Add(row);
            return value;
        }

        int MaxStaff(string id) =>
            Math.Max(0, _units[id]) * Math.Max(1, _engine.Content.Industries[_state.Businesses.First(b => b.Id == id).IndustryId].StaffPerUnit) * 2;

        public void StepUnits(string id, int delta)
        {
            if (!_units.ContainsKey(id)) return;
            _units[id] = Math.Max(0, Math.Min(100, _units[id] + delta));
            _staff[id] = Math.Min(_staff[id], MaxStaff(id));
            Show(id);
        }

        public void StepStaff(string id, int delta)
        {
            if (!_staff.ContainsKey(id)) return;
            _staff[id] = Math.Max(0, Math.Min(MaxStaff(id), _staff[id] + delta));
            Show(id);
        }

        /// <summary>Sets the unit target for next month (the industry screen's "open the first unit").</summary>
        public void QueueUnits(string id, int units)
        {
            RefreshBusinesses();
            if (!_units.ContainsKey(id)) return;
            _units[id] = Math.Max(0, units);
            Show(id);
        }

        void Show(string id)
        {
            if (!_rows.TryGetValue(id, out var r)) return;
            r.units.text = _units[id].ToString();
            r.staff.text = _staff[id].ToString();
        }

        /// <summary>After a month: explains rejected unit purchases next to the stepper.</summary>
        public void ShowRejections(MonthReport report)
        {
            foreach (var rejected in report.RejectedLevers)
            {
                if (!rejected.StartsWith("units:")) continue;
                if (_rows.TryGetValue(rejected.Substring("units:".Length), out var r)) r.rejected.style.display = DisplayStyle.Flex;
            }
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
            foreach (var b in Businesses)
            {
                if (_units.TryGetValue(b.Id, out var u) && u != b.Units) d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "units", ValueCents = u });
                if (_staff.TryGetValue(b.Id, out var st) && st != b.Employees) d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "staff", ValueCents = st });
            }
            return d;
        }
    }
}
