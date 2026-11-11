using System;
using System.Linq;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// "My day · 24 hours" (stage 4 mockup): sleep, the commute and food &amp; chores are fixed; the twelve hours left are
    /// the CEO's to give to work, family, study or themselves, an hour at a time. The engine still thinks in percent of
    /// the CEO's time (TimeAllocation), so hours and shares are mapped both ways by largest remainder and round-trip.
    /// </summary>
    public sealed class DayPlanControl : VisualElement
    {
        public const int Sleep = 8, Travel = 1, Chores = 3, FreeHours = 12;
        /// <summary>Indices match TimeAllocation / MonthDecisions: work, family, rest ("my time"), study.</summary>
        public const int Work = 0, Family = 1, MyTime = 2, Study = 3;
        public static readonly string[] Keys = { "work", "family", "me", "study" };
        static readonly string[] LabelKeys = { "decisions.work", "decisions.family", "day.me", "decisions.study" };
        static readonly (string key, int hours)[] Fixed = { ("sleep", Sleep), ("travel", Travel), ("chores", Chores) };

        int[] _hours;
        readonly Label[] _hourLabels = new Label[Keys.Length];
        readonly Button[] _minus = new Button[Keys.Length], _plus = new Button[Keys.Length];
        readonly VisualElement[] _segments = new VisualElement[Keys.Length];

        public event Action<int[]> Changed;

        public DayPlanControl(int work, int family, int rest, int study)
        {
            name = "day-plan";
            AddToClassList("day-plan");
            _hours = HoursFromShares(new[] { work, family, rest, study });

            var bar = new VisualElement { name = "day-bar" };
            bar.AddToClassList("day-bar");
            foreach (var f in Fixed) bar.Add(Segment("day-bar__seg--" + f.key, f.hours));
            for (int i = 0; i < Keys.Length; i++) bar.Add(_segments[i] = Segment("day-bar__seg--" + Keys[i], _hours[i]));
            Add(bar);

            var legend = new VisualElement { name = "day-fixed" };
            legend.AddToClassList("day-fixed");
            foreach (var f in Fixed)
            {
                var item = new VisualElement();
                item.AddToClassList("day-fixed__item");
                var dot = new VisualElement();
                dot.AddToClassList("day-dot");
                dot.AddToClassList("day-bar__seg--" + f.key);
                item.Add(dot);
                item.Add(UiText.Label("day." + f.key, "day-fixed__label"));
                item.Add(UiText.ValueLabel(f.hours.ToString(), "day-fixed__hours"));
                legend.Add(item);
            }
            Add(legend);

            for (int i = 0; i < Keys.Length; i++)
            {
                int index = i;
                var row = new VisualElement { name = "day-row-" + Keys[i] };
                row.AddToClassList("day-row");
                var dot = new VisualElement();
                dot.AddToClassList("day-dot");
                dot.AddToClassList("day-bar__seg--" + Keys[i]);
                row.Add(dot);
                row.Add(UiText.Label(LabelKeys[i], "day-row__label"));
                _minus[i] = new Button(() => Remove(index)) { name = "day-minus-" + Keys[i], text = "\u2212" };
                _minus[i].AddToClassList("day-row__step");
                _minus[i].AddToClassList("value");
                var hours = new VisualElement();
                hours.AddToClassList("day-row__hours");
                _hourLabels[i] = UiText.ValueLabel("", "day-row__number");
                _hourLabels[i].name = "day-hours-" + Keys[i];
                hours.Add(_hourLabels[i]);
                hours.Add(UiText.Label("day.hours_unit", "day-row__unit"));   // "h" / "ч", localized on its own
                _plus[i] = new Button(() => Add(index)) { name = "day-plus-" + Keys[i], text = "+" };
                _plus[i].AddToClassList("day-row__step");
                _plus[i].AddToClassList("value");
                row.Add(_minus[i]);
                row.Add(hours);
                row.Add(_plus[i]);
                Add(row);
            }
            Sync();
        }

        static VisualElement Segment(string cls, int hours)
        {
            var seg = new VisualElement();
            seg.AddToClassList("day-bar__seg");
            seg.AddToClassList(cls);
            seg.style.flexGrow = hours;
            return seg;
        }

        /// <summary>Hours of the free twelve, in Keys order.</summary>
        public int[] Hours => (int[])_hours.Clone();
        /// <summary>The same plan as percent shares that sum to 100 (what MonthDecisions carries).</summary>
        public int[] Values => SharesFromHours(_hours);

        /// <summary>One more hour here: it comes out of "my time" while there is any, then from the fullest other slot.</summary>
        public void Add(int index)
        {
            int from = index != MyTime && _hours[MyTime] > 0 ? MyTime : Fullest(index);
            if (from < 0) return;
            _hours[from]--;
            _hours[index]++;
            Sync();
            Changed?.Invoke(Values);
        }

        /// <summary>One hour less here: it goes back to "my time" (an hour taken from my time goes to work).</summary>
        public void Remove(int index)
        {
            if (_hours[index] <= 0) return;
            _hours[index]--;
            _hours[index == MyTime ? Work : MyTime]++;
            Sync();
            Changed?.Invoke(Values);
        }

        int Fullest(int except)
        {
            int best = -1;
            for (int i = 0; i < _hours.Length; i++)
                if (i != except && _hours[i] > 0 && (best < 0 || _hours[i] > _hours[best])) best = i;
            return best;
        }

        void Sync()
        {
            for (int i = 0; i < _hours.Length; i++)
            {
                _hourLabels[i].text = _hours[i].ToString();
                _segments[i].style.flexGrow = _hours[i];
                _segments[i].style.display = _hours[i] > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _minus[i].SetEnabled(_hours[i] > 0);
                _plus[i].SetEnabled(_hours[i] < FreeHours);
            }
        }

        /// <summary>Percent shares (sum 100) → whole hours of the free twelve (sum 12), largest remainder first.</summary>
        public static int[] HoursFromShares(int[] shares) => Apportion(shares, FreeHours);

        /// <summary>Whole hours (sum 12) → percent shares (sum 100), largest remainder first.</summary>
        public static int[] SharesFromHours(int[] hours) => Apportion(hours, 100);

        static int[] Apportion(int[] weights, int total)
        {
            long sum = weights.Sum(w => (long)Math.Max(0, w));
            var result = new int[weights.Length];
            if (sum <= 0) { result[0] = total; return result; }
            var remainders = new double[weights.Length];
            int given = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                double exact = Math.Max(0, weights[i]) * (double)total / sum;
                result[i] = (int)Math.Floor(exact + 1e-9);
                remainders[i] = exact - result[i];
                given += result[i];
            }
            foreach (int i in Enumerable.Range(0, weights.Length).OrderByDescending(i => remainders[i]).ThenBy(i => i))
            {
                if (given >= total) break;
                result[i]++;
                given++;
            }
            return result;
        }
    }
}
