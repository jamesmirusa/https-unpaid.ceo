using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Life;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>A set of percentage sliders that always add up to 100 (moving one rescales the others).</summary>
    public class ShareControl : VisualElement
    {
        readonly List<SliderInt> _sliders = new List<SliderInt>();
        readonly List<Label> _values = new List<Label>();
        int[] _shares;

        public event Action<int[]> Changed;
        public IReadOnlyList<string> Keys { get; }

        public ShareControl(IReadOnlyList<string> labelKeys, int[] initial)
        {
            Keys = labelKeys;
            _shares = (int[])initial.Clone();
            for (int i = 0; i < labelKeys.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("share-row");
                row.Add(UiText.Label(labelKeys[i], "share-row__label"));
                var slider = new SliderInt(0, 100) { name = "share-" + labelKeys[i] };
                slider.AddToClassList("share-row__slider");
                slider.RegisterValueChangedCallback(e => SetShare(index, e.newValue));
                row.Add(slider);
                var value = UiText.ValueLabel("", "share-row__value");
                row.Add(value);
                _sliders.Add(slider);
                _values.Add(value);
                Add(row);
            }
            Sync();
        }

        public int[] Values => (int[])_shares.Clone();

        public void SetShare(int index, int value)
        {
            _shares = TimeAllocation.Rebalance(_shares, index, value);
            Sync();
            Changed?.Invoke(Values);
        }

        public void SetAll(int[] shares)
        {
            _shares = (int[])shares.Clone();
            Sync();
        }

        void Sync()
        {
            for (int i = 0; i < _shares.Length; i++)
            {
                _sliders[i].SetValueWithoutNotify(_shares[i]);
                _values[i].text = _shares[i] + "%";
            }
        }
    }

    /// <summary>Work / Family / Rest / Study — the CEO's month (spec §3a).</summary>
    public sealed class TimeAllocationControl : ShareControl
    {
        public static readonly string[] LabelKeys = { "decisions.work", "decisions.family", "decisions.rest", "decisions.study" };

        public TimeAllocationControl(int work, int family, int rest, int study)
            : base(LabelKeys, new[] { work, family, rest, study })
        {
            name = "time-allocation";
        }
    }
}
