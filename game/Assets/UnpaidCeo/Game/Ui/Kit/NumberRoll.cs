using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// A number that rolls to its new value (ease-out, ≤ 0.45 s) instead of jumping. <see cref="Target"/> is the final
    /// value at once; the label ends exactly on it. The first value is shown without rolling.
    /// </summary>
    public sealed class NumberRoll
    {
        public const float Seconds = 0.45f;
        readonly Label _label;
        readonly Func<long, string> _format;
        IVisualElementScheduledItem _job;
        long _from, _target;
        float _start;
        bool _hasValue;

        public NumberRoll(Label label, Func<long, string> format) { _label = label; _format = format; }

        public long Target => _target;
        public bool HasValue => _hasValue;
        public bool Rolling => _job != null && _job.isActive;

        /// <summary>Shows <paramref name="value"/>; returns the change from the previous target (0 the first time).</summary>
        public long Set(long value, bool animate = true)
        {
            if (!_hasValue || !animate || _label.panel == null)
            {
                long d = _hasValue ? value - _target : 0;
                _hasValue = true;
                _target = value;
                _job?.Pause();
                _label.text = _format(value);
                return d;
            }
            long delta = value - _target;
            if (delta == 0) { _label.text = _format(value); return 0; }
            _from = Current();
            _target = value;
            _start = Time.unscaledTime;
            _job?.Pause();
            _job = _label.schedule.Execute(Step).Every(16);
            Step();
            return delta;
        }

        /// <summary>Re-formats the final value (a language switch) without rolling.</summary>
        public void Reformat() { if (_hasValue && !Rolling) _label.text = _format(_target); }   // a roll in progress formats each step itself

        long Current()
        {
            if (!Rolling) return _target;
            float t = Mathf.Clamp01((Time.unscaledTime - _start) / Seconds);
            return Lerp(_from, _target, Ease(t));
        }

        void Step()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _start) / Seconds);
            _label.text = _format(t >= 1f ? _target : Lerp(_from, _target, Ease(t)));
            if (t >= 1f) _job?.Pause();
        }

        public static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        static long Lerp(long a, long b, float t) => a + (long)Math.Round((b - a) * (double)t);
    }
}
