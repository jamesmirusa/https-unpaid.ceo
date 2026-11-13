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
    /// The Week screen (stage 5A): every decision is a card taken with one tap and closed until next week — the week's
    /// mode (a ready split of the hero's free time), where the work goes (one gig, or evenly), this week's deeds (the free
    /// actions: the same cards as in the rooms) and, from chapter 2, one step per business lever (price, ads, locations,
    /// staff). What was tapped this week is kept in the meta save (<see cref="Snapshot"/>), so a reload cannot undo it.
    /// A lever is sent only when the player stepped it this week, so a scene's effect (staff leaving) is never undone.
    /// </summary>
    public sealed class DecisionsScreen
    {
        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly VisualElement _modeCards, _focusCards, _actions, _business;
        readonly Label _focusTitle, _ownerTime, _businessTitle, _ap, _energy;
        readonly Dictionary<string, Button> _modes = new Dictionary<string, Button>();
        readonly Dictionary<string, Label> _modeStatus = new Dictionary<string, Label>();
        readonly Dictionary<string, VisualElement> _modeChips = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, Button> _focus = new Dictionary<string, Button>();
        readonly Dictionary<string, Label> _focusStatus = new Dictionary<string, Label>();
        readonly Dictionary<string, Label> _focusNeed = new Dictionary<string, Label>();
        readonly Dictionary<string, ChoiceCard> _cards = new Dictionary<string, ChoiceCard>();
        readonly HashSet<string> _planned = new HashSet<string>();
        readonly HashSet<string> _leverChanged = new HashSet<string>();
        readonly Dictionary<string, Button> _leverButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, int> _units = new Dictionary<string, int>(), _staff = new Dictionary<string, int>();
        readonly Dictionary<string, int> _unitsBase = new Dictionary<string, int>(), _staffBase = new Dictionary<string, int>();
        readonly Dictionary<string, long> _price = new Dictionary<string, long>(), _marketing = new Dictionary<string, long>();
        readonly Dictionary<string, long> _priceBase = new Dictionary<string, long>(), _marketingBase = new Dictionary<string, long>();
        readonly Dictionary<string, (Label units, Label staff, Label rejected, Label price, Label marketing)> _rows =
            new Dictionary<string, (Label, Label, Label, Label, Label)>();
        List<string> _gigIds = new List<string>();
        string _businessKey = "", _gigKey, _mode, _focusId;
        bool _modeChosen, _focusChosen;
        int _planWeek = -1;

        public DecisionsScreen(VisualElement root, UiRoot ui, GameEngine engine, GameState state)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("decisions-title"), "decisions.title");
            UiText.Bind(root.Q<Label>("decisions-hint"), "decisions.hint");
            UiText.Bind(root.Q<Label>("energy-caption"), "stat.energy");
            UiText.Bind(root.Q<Label>("ap-caption"), "decisions.ap");
            _energy = root.Q<Label>("week-energy");
            _ap = root.Q<Label>("ap-value");
            UiText.Bind(root.Q<Label>("mode-title"), "week.mode");
            _modeCards = root.Q("mode-cards");
            _focusTitle = UiText.Bind(root.Q<Label>("focus-title"), "week.focus");
            _focusCards = root.Q("focus-cards");
            _ownerTime = UiText.Bind(root.Q<Label>("owner-time"), "decisions.owner_time");
            UiText.Bind(root.Q<Label>("actions-title"), "decisions.actions");
            _actions = root.Q("actions");
            _businessTitle = UiText.Bind(root.Q<Label>("business-title"), "decisions.business");
            _business = root.Q("business-controls");

            foreach (var mode in WeekPlanning.ModeOrder.Where(Balance.WeekModes.ContainsKey))
            {
                var id = mode;
                var b = new Button(() => ChooseMode(id)) { name = "mode-" + id };
                b.AddToClassList("week-card");
                b.Add(UiText.Label("mode." + id, "week-card__title"));
                b.Add(UiText.Label("mode." + id + ".desc", "week-card__desc"));
                var chips = new VisualElement();
                chips.AddToClassList("visit__choice-chips");
                b.Add(chips);
                var status = UiText.Label("week.last", "week-card__status");
                status.name = "mode-status-" + id;
                b.Add(status);
                _modes[id] = b;
                _modeStatus[id] = status;
                _modeChips[id] = chips;
                _modeCards.Add(b);
            }

            foreach (var fa in engine.Content.FreeActions.Values)
            {
                var id = fa.Id;
                var card = new ChoiceCard("action-" + id, "action-status-" + id, "action." + id, () => Plan(id));
                _cards[id] = card;
                _actions.Add(card);
            }
            Refresh();
        }

        Balance Balance => _engine.Content.Balance;

        MonthDecisions Last => _state.LastDecisions ?? MonthDecisions.Default(_state.Businesses.FirstOrDefault()?.Id);

        IndustryKind? KindOf(BusinessState b) => _engine.Content.Industries.TryGetValue(b.IndustryId, out var d) ? d.Kind : (IndustryKind?)null;

        IEnumerable<BusinessState> Businesses => _state.Businesses.Where(b => KindOf(b) == IndustryKind.Business);

        IEnumerable<BusinessState> Gigs() => _state.Businesses.Where(b => KindOf(b) == IndustryKind.Gig);

        bool OwnsUnits => Businesses.Any(b => b.Units > 0);

        // ---------- The week's state ----------

        /// <summary>This week's mode (last week's until another is chosen); null when last week's split was no mode.</summary>
        public string Mode => _mode;
        /// <summary>A mode was tapped this week: the modes are closed until next week.</summary>
        public bool ModeChosen => _modeChosen;
        /// <summary>Where the work goes this week: a gig id or <see cref="WeekPlanning.EvenFocus"/>.</summary>
        public string Focus => _focusId;
        public bool FocusChosen => _focusChosen;

        /// <summary>The free actions done this week, in the order the week settles them.</summary>
        public IEnumerable<string> Planned => _engine.Content.FreeActions.Keys.Where(_planned.Contains);

        /// <summary>The points this week can spend, and how many its deeds leave.</summary>
        public int PointsTotal => ActionChoices.PointsTotal(_state, _engine.Content);
        public int PointsLeft => ActionChoices.PointsLeft(_state, _engine.Content, Planned);
        /// <summary>The hero's energy once this week's deeds are done.</summary>
        public int EnergyAfterPlan => ActionChoices.EnergyAfter(_state, _engine.Content, Planned);
        /// <summary>Something was decided this week (here or at a place in the city).</summary>
        public event Action PlanChanged;

        public bool IsPlanned(string freeAction) => _planned.Contains(freeAction);

        /// <summary>Whether a free action can be done this week, and if not, why (the engine settles the same way).</summary>
        public ChoiceState State(string freeAction) => ActionChoices.State(_state, _engine.Content, Planned, freeAction);

        /// <summary>Weeks until a habit with its own rhythm (the payday transfer) opens again.</summary>
        public int WeeksUntilOpen(string freeAction) => ActionChoices.WeeksUntilOpen(_state, _engine.Content, freeAction);

        /// <summary>It answers what the hero lacks most right now (rest when tired, family when they drift…).</summary>
        public bool Recommended(string freeAction) => ActionChoices.Recommended(_state, _engine.Content, freeAction, Planned);

        /// <summary>The week can still take it (a done choice stays done: there is no undo).</summary>
        public bool CanPlan(string freeAction) => _engine.Content.FreeActions.ContainsKey(freeAction) && State(freeAction) == ChoiceState.Open;

        /// <summary>
        /// One tap: the free action is done this week — it goes into the plan, its energy and money show as spent, and it
        /// stays done until next week. False when the week cannot take it (or it is already done).
        /// </summary>
        public bool Plan(string freeAction)
        {
            if (_planned.Contains(freeAction) || !CanPlan(freeAction)) return false;
            _planned.Add(freeAction);
            Render();
            return true;
        }

        /// <summary>Sets the week's mode with one tap (also last week's, to keep it): the other modes close until next week.</summary>
        public bool ChooseMode(string mode)
        {
            if (_modeChosen || mode == null || !Balance.WeekModes.ContainsKey(mode)) return false;
            _mode = mode;
            _modeChosen = true;
            _ui.Gate.PlanReviewed = true;
            Render();
            return true;
        }

        /// <summary>Puts the week's work into one gig, or evenly, with one tap; a gig that needs more knowledge stays closed.</summary>
        public bool ChooseFocus(string focus)
        {
            if (_focusChosen || OwnsUnits || !FocusOpen(focus)) return false;
            _focusId = focus;
            _focusChosen = true;
            Render();
            return true;
        }

        bool FocusOpen(string focus) =>
            focus == WeekPlanning.EvenFocus ? _gigIds.All(GigOpen) : _gigIds.Contains(focus) && GigOpen(focus);

        bool GigOpen(string gigId) => RequiredStudy(gigId) <= _state.StudyXp;

        int RequiredStudy(string gigId)
        {
            var b = _state.Businesses.FirstOrDefault(x => x.Id == gigId);
            return b != null && _engine.Content.Industries.TryGetValue(b.IndustryId, out var d) ? d.RequiredStudy : 0;
        }

        /// <summary>A lever was stepped this week: its buttons are closed until next week.</summary>
        public bool LeverLocked(string businessId, string lever) => _leverChanged.Contains(businessId + "/" + lever);

        // ---------- Weeks and reloads ----------

        /// <summary>Re-reads the game: a new week opens every card again; the same week keeps what was decided.</summary>
        public void Refresh()
        {
            if (_planWeek != _state.Week) StartWeek();
            RefreshFocusCards();
            RefreshBusinesses();
            Render();
        }

        void StartWeek()
        {
            _planWeek = _state.Week;
            _planned.Clear();
            _leverChanged.Clear();
            _modeChosen = _focusChosen = false;
            _mode = WeekPlanning.ModeOf(Last, Balance);
            _gigIds = Gigs().Select(b => b.Id).ToList();
            _focusId = WeekPlanning.FocusOf(Last, _gigIds);
        }

        /// <summary>What was decided this week, for the meta save.</summary>
        public WeekPlanRecord Snapshot()
        {
            var r = new WeekPlanRecord { Week = _planWeek, Mode = _mode, ModeChosen = _modeChosen, Focus = _focusId, FocusChosen = _focusChosen };
            r.Actions.AddRange(Planned);
            foreach (var b in Businesses)
            {
                if (!_units.ContainsKey(b.Id)) continue;
                if (LeverLocked(b.Id, "price")) r.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "price", ValueCents = _price[b.Id] });
                if (LeverLocked(b.Id, "marketing")) r.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "marketing", ValueCents = _marketing[b.Id] });
                if (LeverLocked(b.Id, "units")) r.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "units", ValueCents = _units[b.Id] });
                if (LeverLocked(b.Id, "staff")) r.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "staff", ValueCents = _staff[b.Id] });
            }
            return r;
        }

        /// <summary>Brings back this week's decisions after a reload; a record of another week is ignored.</summary>
        public void Restore(WeekPlanRecord r)
        {
            if (r == null || r.Week != _state.Week) return;
            StartWeek();
            if (r.Mode != null && Balance.WeekModes.ContainsKey(r.Mode)) { _mode = r.Mode; _modeChosen = r.ModeChosen; }
            if (r.Focus != null && (r.Focus == WeekPlanning.EvenFocus || _gigIds.Contains(r.Focus))) { _focusId = r.Focus; _focusChosen = r.FocusChosen; }
            foreach (var a in r.Actions ?? new List<string>())
                if (_engine.Content.FreeActions.ContainsKey(a)) _planned.Add(a);
            RefreshFocusCards();
            RefreshBusinesses();
            foreach (var l in r.Levers ?? new List<LeverChange>())
            {
                if (l == null || l.BusinessId == null || !_units.ContainsKey(l.BusinessId)) continue;
                switch (l.Lever)
                {
                    case "price": _price[l.BusinessId] = l.ValueCents; break;
                    case "marketing": _marketing[l.BusinessId] = l.ValueCents; break;
                    case "units": _units[l.BusinessId] = (int)l.ValueCents; break;
                    case "staff": _staff[l.BusinessId] = (int)l.ValueCents; break;
                    default: continue;
                }
                _leverChanged.Add(l.BusinessId + "/" + l.Lever);
            }
            Render();
        }

        // ---------- Rendering ----------

        void Render()
        {
            foreach (var kv in _modes)
            {
                bool selected = kv.Key == _mode;
                kv.Value.EnableInClassList("week-card--selected", selected);
                kv.Value.SetEnabled(!_modeChosen);
                string key = selected ? (_modeChosen ? "week.mode_set" : "week.last") : null;
                ShowStatus(_modeStatus[kv.Key], key);
                FillModeChips(kv.Key);
            }

            bool owner = OwnsUnits;
            bool showFocus = !owner && _gigIds.Count > 1;
            _focusTitle.style.display = showFocus ? DisplayStyle.Flex : DisplayStyle.None;
            _focusCards.style.display = showFocus ? DisplayStyle.Flex : DisplayStyle.None;
            _ownerTime.style.display = owner ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var kv in _focus)
            {
                bool selected = kv.Key == _focusId, open = FocusOpen(kv.Key);
                kv.Value.EnableInClassList("week-card--selected", selected);
                kv.Value.SetEnabled(!_focusChosen && open);
                string key = !open ? "focus.needs_study" : selected ? (_focusChosen ? "week.focus_set" : "week.last") : null;
                ShowStatus(_focusStatus[kv.Key], key);
                if (_focusNeed.TryGetValue(kv.Key, out var need))
                {
                    need.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
                    need.text = RequiredStudy(kv.Key).ToString();
                }
            }

            foreach (var kv in _cards)
            {
                if (!_engine.Content.FreeActions.TryGetValue(kv.Key, out var fa)) continue;
                kv.Value.SetChips(fa.Cost, fa.Effects, _ui.Language);
                var st = State(kv.Key);
                kv.Value.Show(st, st == ChoiceState.Open && Recommended(kv.Key), WeeksUntilOpen(kv.Key));
            }
            _ap.text = $"{PointsLeft} / {PointsTotal}";
            _energy.text = VisitPanel.EnergyText(_state.Life.Energy, EnergyAfterPlan);

            foreach (var b in Businesses) Show(b.Id);
            PlanChanged?.Invoke();
        }

        static void ShowStatus(Label status, string key)
        {
            status.style.display = key != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (key != null) UiText.Bind(status, key);
        }

        /// <summary>What a week in this mode does, worked out by the life model: work share, energy, close ones, knowledge.</summary>
        void FillModeChips(string mode)
        {
            var box = _modeChips[mode];
            box.Clear();
            var p = Balance.WeekModes[mode];
            if (p == null || p.Length != 4 || !TimeAllocation.TryCreate(p[0], p[1], p[2], p[3], out var t)) return;
            var life = new LifeStats { Energy = _state.Life.Energy, Relationships = _state.Life.Relationships };
            var delta = LifeSimulator.Apply(life, t, Balance.Life, 1.0 / GameState.WeeksPerMonth);
            box.Add(EffectChips.Chip("decisions.work", p[0] + "%", true));
            if (delta.Energy != 0) box.Add(EffectChips.Chip("stat.energy", EffectChips.Signed(delta.Energy), delta.Energy > 0));
            if (delta.Relationships != 0) box.Add(EffectChips.Chip("stat.relationships", EffectChips.Signed(delta.Relationships), delta.Relationships > 0));
            if (delta.StudyXp != 0) box.Add(EffectChips.Chip("stat.knowledge", EffectChips.Signed(delta.StudyXp), true));
        }

        void RefreshFocusCards()
        {
            var gigs = Gigs().ToList();
            var key = string.Join(",", gigs.Select(g => g.Id));
            if (key == _gigKey) return;
            _gigKey = key;
            _gigIds = gigs.Select(g => g.Id).ToList();
            if (_focusId != null && _focusId != WeekPlanning.EvenFocus && !_gigIds.Contains(_focusId)) _focusId = null;
            _focusCards.Clear();
            _focus.Clear();
            _focusStatus.Clear();
            _focusNeed.Clear();
            foreach (var g in gigs) AddFocusCard(g.Id, "gig." + g.IndustryId);
            AddFocusCard(WeekPlanning.EvenFocus, "focus.even");
        }

        void AddFocusCard(string id, string titleKey)
        {
            var b = new Button(() => ChooseFocus(id)) { name = "focus-" + id };
            b.AddToClassList("week-card");
            b.AddToClassList("week-card--small");
            b.Add(UiText.Label(titleKey, "week-card__title"));
            var row = new VisualElement();
            row.AddToClassList("week-card__status-row");
            var status = UiText.Label("week.last", "week-card__status");
            status.name = "focus-status-" + id;
            row.Add(status);
            if (id != WeekPlanning.EvenFocus)
            {
                var need = UiText.ValueLabel("", "week-card__status");
                need.name = "focus-need-" + id;
                need.style.display = DisplayStyle.None;
                row.Add(need);
                _focusNeed[id] = need;
            }
            b.Add(row);
            _focus[id] = b;
            _focusStatus[id] = status;
            _focusCards.Add(b);
        }

        // ---------- Business levers: one step a week each ----------

        /// <summary>Rebuilds the lever rows when businesses change; a lever not stepped this week follows the business.</summary>
        void RefreshBusinesses()
        {
            var list = Businesses.ToList();
            var key = string.Join(",", list.Select(b => b.Id));
            if (key != _businessKey)
            {
                _businessKey = key;
                _business.Clear();
                _rows.Clear();
                _leverButtons.Clear();
                foreach (var b in list) _business.Add(BusinessRow(b));
            }
            bool any = list.Count > 0;
            _businessTitle.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _business.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var b in list)
            {
                _unitsBase[b.Id] = b.Units;
                _staffBase[b.Id] = b.Employees;
                _priceBase[b.Id] = CurrentPrice(b);
                _marketingBase[b.Id] = b.Marketing.Cents;
                if (!LeverLocked(b.Id, "units") || !_units.ContainsKey(b.Id)) _units[b.Id] = b.Units;
                if (!LeverLocked(b.Id, "staff") || !_staff.ContainsKey(b.Id)) _staff[b.Id] = b.Employees;
                if (!LeverLocked(b.Id, "price") || !_price.ContainsKey(b.Id)) _price[b.Id] = CurrentPrice(b);
                if (!LeverLocked(b.Id, "marketing") || !_marketing.ContainsKey(b.Id)) _marketing[b.Id] = b.Marketing.Cents;
                if (_rows.TryGetValue(b.Id, out var r) && !LeverLocked(b.Id, "units")) r.rejected.style.display = DisplayStyle.None;
                Show(b.Id);
            }
        }

        VisualElement BusinessRow(BusinessState b)
        {
            var def = _engine.Content.Industries[b.IndustryId];
            var box = new VisualElement { name = "business-" + b.Id };
            box.AddToClassList("business-box");
            box.Add(UiText.Label($"industry.{b.IndustryId}.name", "business-box__name"));
            var price = LeverRow(box, "decisions.price", "price", b.Id, "lever.price_down", "lever.price_up", d => StepPrice(b.Id, d));
            var marketing = LeverRow(box, "decisions.marketing", "marketing", b.Id, "lever.marketing_down", "lever.marketing_up", d => StepMarketing(b.Id, d));
            var units = LeverRow(box, "decisions.units", "units", b.Id, "lever.close_unit", "lever.open_unit", d => StepUnits(b.Id, d));
            var cost = new VisualElement();
            cost.AddToClassList("row");
            cost.Add(UiText.Label("decisions.unit_cost", "row__label"));
            cost.Add(UiText.ValueLabel(MoneyFormat.Compact(def.Capex, _ui.Language), "row__value"));
            box.Add(cost);
            var staff = LeverRow(box, "decisions.staff", "staff", b.Id, "lever.fire", "lever.hire", d => StepStaff(b.Id, d));
            var rejected = UiText.Label("decisions.units_rejected", "decisions__warning");
            rejected.name = "units-rejected-" + b.Id;
            rejected.style.display = DisplayStyle.None;
            box.Add(rejected);
            _rows[b.Id] = (units, staff, rejected, price, marketing);
            return box;
        }

        Label LeverRow(VisualElement parent, string key, string what, string id, string downKey, string upKey, Action<int> step)
        {
            var head = new VisualElement();
            head.AddToClassList("row");
            head.Add(UiText.Label(key, "row__label"));
            var value = UiText.ValueLabel("", "row__value");
            value.name = $"{what}-value-{id}";
            head.Add(value);
            parent.Add(head);
            var buttons = new VisualElement();
            buttons.AddToClassList("lever-row");
            var minus = UiText.Bind(new Button(() => step(-1)) { name = $"{what}-minus-{id}" }, downKey);
            minus.AddToClassList("lever-button");
            var plus = UiText.Bind(new Button(() => step(+1)) { name = $"{what}-plus-{id}" }, upKey);
            plus.AddToClassList("lever-button");
            buttons.Add(minus);
            buttons.Add(plus);
            parent.Add(buttons);
            _leverButtons[minus.name] = minus;
            _leverButtons[plus.name] = plus;
            return value;
        }

        int MaxStaff(string id) =>
            Math.Max(0, _units[id]) * Math.Max(1, _engine.Content.Industries[_state.Businesses.First(b => b.Id == id).IndustryId].StaffPerUnit) * 2;

        bool CanStep(string id, string lever) => _units.ContainsKey(id) && !LeverLocked(id, lever);

        void Stepped(string id, string lever, bool changed)
        {
            if (changed) _leverChanged.Add(id + "/" + lever);
            Show(id);
            PlanChanged?.Invoke();
        }

        /// <summary>One location more or fewer this week (one tap, then the lever closes until next week).</summary>
        public void StepUnits(string id, int delta)
        {
            if (!CanStep(id, "units")) return;
            int before = _units[id];
            _units[id] = Math.Max(0, Math.Min(100, _units[id] + Math.Sign(delta)));
            _staff[id] = Math.Min(_staff[id], MaxStaff(id));
            Stepped(id, "units", _units[id] != before);
        }

        /// <summary>One hire or one goodbye this week.</summary>
        public void StepStaff(string id, int delta)
        {
            if (!CanStep(id, "staff")) return;
            int before = _staff[id];
            _staff[id] = Math.Max(0, Math.Min(MaxStaff(id), _staff[id] + Math.Sign(delta)));
            Stepped(id, "staff", _staff[id] != before);
        }

        IndustryDef Def(string id) => _engine.Content.Industries[_state.Businesses.First(b => b.Id == id).IndustryId];

        long CurrentPrice(BusinessState b)
        {
            var def = _engine.Content.Industries[b.IndustryId];
            return b.Price.Cents > 0 ? b.Price.Cents : (def.RefPrice.Cents > 0 ? def.RefPrice.Cents : def.Price.Cents);
        }

        /// <summary>Price moves by 5% of the reference price, between half and double of it; one step a week.</summary>
        public void StepPrice(string id, int delta)
        {
            if (!CanStep(id, "price")) return;
            var def = Def(id);
            long reference = Math.Max(1, def.RefPrice.Cents > 0 ? def.RefPrice.Cents : def.Price.Cents);
            long step = Math.Max(1, (reference * 5 + 50) / 100);
            long before = _price[id];
            _price[id] = Math.Max(Math.Max(1, reference / 2), Math.Min(reference * 2, _price[id] + Math.Sign(delta) * step));
            Stepped(id, "price", _price[id] != before);
        }

        /// <summary>Monthly marketing moves by a quarter of the industry's marketing scale, from zero to four times it; one step a week.</summary>
        public void StepMarketing(string id, int delta)
        {
            if (!CanStep(id, "marketing")) return;
            long scale = Math.Max(10_000, Def(id).MarketingK.Cents);
            long step = Math.Max(1_000, (scale / 4 + 500) / 1_000 * 1_000);
            long before = _marketing[id];
            _marketing[id] = Math.Max(0, Math.Min(scale * 4, _marketing[id] + Math.Sign(delta) * step));
            Stepped(id, "marketing", _marketing[id] != before);
        }

        /// <summary>Sets the unit target for next week (the industry screen's "open the first unit").</summary>
        public void QueueUnits(string id, int units)
        {
            RefreshBusinesses();
            if (!_units.ContainsKey(id)) return;
            _units[id] = Math.Max(0, units);
            Stepped(id, "units", true);
        }

        void Show(string id)
        {
            if (!_rows.TryGetValue(id, out var r) || !_units.ContainsKey(id)) return;
            string Full(long cents) => MoneyFormat.Full(UnpaidCeo.Core.Primitives.Money.FromCents(cents), _ui.Language);
            string Compact(long cents) => MoneyFormat.Compact(UnpaidCeo.Core.Primitives.Money.FromCents(cents), _ui.Language);
            r.units.text = Arrow(_unitsBase[id].ToString(), _units[id].ToString());
            r.staff.text = Arrow(_staffBase[id].ToString(), _staff[id].ToString());
            r.price.text = Arrow(Full(_priceBase[id]), Full(_price[id]));
            r.marketing.text = Arrow(Compact(_marketingBase[id]), Compact(_marketing[id]));
            foreach (var lever in new[] { "price", "marketing", "units", "staff" })
            {
                bool open = !LeverLocked(id, lever);
                if (_leverButtons.TryGetValue($"{lever}-minus-{id}", out var minus)) minus.SetEnabled(open);
                if (_leverButtons.TryGetValue($"{lever}-plus-{id}", out var plus)) plus.SetEnabled(open);
            }
        }

        static string Arrow(string now, string after) => now == after ? now : $"{now} → {after}";

        /// <summary>After a week: explains rejected unit purchases next to the lever.</summary>
        public void ShowRejections(MonthReport report)
        {
            foreach (var rejected in report.RejectedLevers)
            {
                if (!rejected.StartsWith("units:")) continue;
                if (_rows.TryGetValue(rejected.Substring("units:".Length), out var r)) r.rejected.style.display = DisplayStyle.Flex;
            }
        }

        public MonthDecisions Build()
        {
            var d = Last.Routine();
            if (_mode != null) WeekPlanning.ApplyMode(d, _mode, Balance);
            if (_focusId != null && _gigIds.Count > 0) WeekPlanning.ApplyFocus(d, _gigIds, _focusId);
            d.FreeActions.AddRange(Planned);
            foreach (var b in Businesses)
            {
                // Only levers the player stepped: a target left alone never fights a scene's effect.
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
