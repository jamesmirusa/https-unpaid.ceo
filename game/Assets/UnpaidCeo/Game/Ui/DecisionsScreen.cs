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
    /// Decisions tab: CEO time split, work split between gigs, the four business levers per business (chapters 2–3:
    /// price, marketing, locations, staff — spec §3.1) and free actions within this month's action points.
    /// A lever is sent only when the player changed its stepper this month, so a scene's effect (staff leaving)
    /// is never undone by a stale target.
    /// </summary>
    public sealed class DecisionsScreen
    {
        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly TimeAllocationControl _time;
        readonly ShareControl _split;
        readonly List<string> _gigIds;
        readonly Label _ap, _businessTitle, _splitTitle, _ownerTime;
        readonly VisualElement _splitControl;
        readonly VisualElement _business;
        readonly Dictionary<string, Toggle> _toggles = new Dictionary<string, Toggle>();
        readonly Dictionary<string, int> _units = new Dictionary<string, int>(), _staff = new Dictionary<string, int>();
        readonly Dictionary<string, int> _unitsBase = new Dictionary<string, int>(), _staffBase = new Dictionary<string, int>();
        readonly Dictionary<string, long> _price = new Dictionary<string, long>(), _marketing = new Dictionary<string, long>();
        readonly Dictionary<string, long> _priceBase = new Dictionary<string, long>(), _marketingBase = new Dictionary<string, long>();
        readonly Dictionary<string, (Label units, Label staff, Label rejected, Label price, Label marketing)> _rows =
            new Dictionary<string, (Label, Label, Label, Label, Label)>();
        string _businessKey = "";

        public DecisionsScreen(VisualElement root, UiRoot ui, GameEngine engine, GameState state)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("decisions-title"), "decisions.title");
            UiText.Bind(root.Q<Label>("decisions-hint"), "decisions.hint");
            UiText.Bind(root.Q<Label>("time-title"), "decisions.time");
            _splitTitle = UiText.Bind(root.Q<Label>("split-title"), "decisions.split");
            _splitControl = root.Q("split-control");
            _ownerTime = UiText.Label("decisions.owner_time", "hint");
            _ownerTime.name = "owner-time";
            _splitControl.parent.Insert(_splitControl.parent.IndexOf(_splitControl) + 1, _ownerTime);
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

        /// <summary>This month's action points, and how many the planned free actions leave.</summary>
        public int PointsTotal => Available;
        public int PointsLeft => Available - _toggles.Where(t => t.Value.value).Sum(t => _engine.Content.FreeActions[t.Key].ActionPoints);
        /// <summary>A free action was put into the plan or taken out (here or at a place in the city).</summary>
        public event Action PlanChanged;

        public bool IsPlanned(string freeAction) => _toggles.TryGetValue(freeAction, out var t) && t.value;

        /// <summary>Already planned (it can be taken out), or the points left still pay for it.</summary>
        public bool CanPlan(string freeAction) =>
            _toggles.TryGetValue(freeAction, out var t) && (t.value || _engine.Content.FreeActions[freeAction].ActionPoints <= PointsLeft);

        /// <summary>Puts a free action into this month's plan or takes it out: the same list the plan screen ticks.</summary>
        public void SetPlanned(string freeAction, bool planned)
        {
            if (!_toggles.TryGetValue(freeAction, out var t) || t.value == planned || planned && !CanPlan(freeAction)) return;
            t.SetValueWithoutNotify(planned);
            RefreshPoints();
        }

        public void Refresh()
        {
            foreach (var t in _toggles.Values) t.SetValueWithoutNotify(false);
            RefreshBusinesses();
            RefreshPoints();
        }

        /// <summary>Rebuilds the business steppers when businesses change, and resets every target to the current state.</summary>
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
            // With a unit open the owner works the business: gigs stop paying, so their split is hidden.
            bool ownsUnits = list.Any(b => b.Units > 0);
            _splitTitle.style.display = ownsUnits ? DisplayStyle.None : DisplayStyle.Flex;
            _splitControl.style.display = ownsUnits ? DisplayStyle.None : DisplayStyle.Flex;
            _ownerTime.style.display = ownsUnits ? DisplayStyle.Flex : DisplayStyle.None;
            _businessTitle.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _business.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var b in list)
            {
                _units[b.Id] = _unitsBase[b.Id] = b.Units;
                _staff[b.Id] = _staffBase[b.Id] = b.Employees;
                _price[b.Id] = _priceBase[b.Id] = CurrentPrice(b);
                _marketing[b.Id] = _marketingBase[b.Id] = b.Marketing.Cents;
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
            var price = Stepper(box, "decisions.price", "price", b.Id, d => StepPrice(b.Id, d));
            var marketing = Stepper(box, "decisions.marketing", "marketing", b.Id, d => StepMarketing(b.Id, d));
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
            _rows[b.Id] = (units, staff, rejected, price, marketing);
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

        IndustryDef Def(string id) => _engine.Content.Industries[_state.Businesses.First(b => b.Id == id).IndustryId];

        long CurrentPrice(BusinessState b)
        {
            var def = _engine.Content.Industries[b.IndustryId];
            return b.Price.Cents > 0 ? b.Price.Cents : (def.RefPrice.Cents > 0 ? def.RefPrice.Cents : def.Price.Cents);
        }

        /// <summary>Price steps by 5% of the reference price, between half and double of it.</summary>
        public void StepPrice(string id, int delta)
        {
            if (!_price.ContainsKey(id)) return;
            var def = Def(id);
            long reference = Math.Max(1, def.RefPrice.Cents > 0 ? def.RefPrice.Cents : def.Price.Cents);
            long step = Math.Max(1, (reference * 5 + 50) / 100);
            _price[id] = Math.Max(Math.Max(1, reference / 2), Math.Min(reference * 2, _price[id] + delta * step));
            Show(id);
        }

        /// <summary>Monthly marketing steps by a quarter of the industry's marketing scale, from zero to four times it.</summary>
        public void StepMarketing(string id, int delta)
        {
            if (!_marketing.ContainsKey(id)) return;
            long scale = Math.Max(10_000, Def(id).MarketingK.Cents);
            long step = Math.Max(1_000, (scale / 4 + 500) / 1_000 * 1_000);
            _marketing[id] = Math.Max(0, Math.Min(scale * 4, _marketing[id] + delta * step));
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
            r.price.text = MoneyFormat.Full(UnpaidCeo.Core.Primitives.Money.FromCents(_price[id]), _ui.Language);
            r.marketing.text = MoneyFormat.Compact(UnpaidCeo.Core.Primitives.Money.FromCents(_marketing[id]), _ui.Language);
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
            PlanChanged?.Invoke();
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
                // Only steppers the player moved: a target left alone never fights a scene's effect.
                if (_price.TryGetValue(b.Id, out var p) && p != _priceBase[b.Id] && p != b.Price.Cents)
                    d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "price", ValueCents = p });
                if (_marketing.TryGetValue(b.Id, out var m) && m != _marketingBase[b.Id] && m != b.Marketing.Cents)
                    d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "marketing", ValueCents = m });
                if (_units.TryGetValue(b.Id, out var u) && u != _unitsBase[b.Id] && u != b.Units)
                    d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "units", ValueCents = u });
                if (_staff.TryGetValue(b.Id, out var st) && st != _staffBase[b.Id] && st != b.Employees)
                    d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "staff", ValueCents = st });
            }
            return d;
        }
    }
}
