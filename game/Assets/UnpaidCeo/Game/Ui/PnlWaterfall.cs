using UnityEngine;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Waterfall of the month: revenue up, then each cost down (cost of goods, fixed, marketing, tax, living costs),
    /// and the resulting cash change as the last bar. Colours come from the theme's --positive / --negative.
    /// </summary>
    public sealed class PnlWaterfall : VisualElement
    {
        static readonly CustomStyleProperty<Color> PositiveProp = new CustomStyleProperty<Color>("--positive");
        static readonly CustomStyleProperty<Color> NegativeProp = new CustomStyleProperty<Color>("--negative");
        static readonly CustomStyleProperty<Color> TrackProp = new CustomStyleProperty<Color>("--track");

        Color _positive = new Color(0.18f, 0.62f, 0.36f), _negative = new Color(0.85f, 0.28f, 0.23f), _track = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        long[] _steps = new long[0];

        public PnlWaterfall()
        {
            name = "pnl-waterfall";
            style.flexGrow = 1;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(PositiveProp, out var p)) _positive = p;
                if (e.customStyle.TryGetValue(NegativeProp, out var n)) _negative = n;
                if (e.customStyle.TryGetValue(TrackProp, out var t)) _track = t;
                MarkDirtyRepaint();
            });
        }

        public void SetReport(MonthReport r)
        {
            _steps = r == null ? new long[0] : new[]
            {
                r.Revenue.Cents, -r.Cogs.Cents, -r.Fixed.Cents, -r.Marketing.Cents, -r.MistakeLoss.Cents, -r.Interest.Cents, -r.TaxReserve.Cents,
                -r.PersonalCosts.Cents, -r.Capex.Cents,
            };
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (_steps.Length == 0 || rect.width <= 0 || rect.height <= 0) return;

            long running = 0, min = 0, max = 0;
            foreach (var s in _steps) { running += s; min = System.Math.Min(min, running); max = System.Math.Max(max, running); }
            max = System.Math.Max(max, _steps[0]);
            float range = Mathf.Max(1, max - min);
            int bars = _steps.Length + 1;
            float slot = rect.width / bars, barWidth = slot * 0.62f;
            float Y(long v) => rect.yMax - (v - min) / range * rect.height;

            var p = ctx.painter2D;
            p.strokeColor = _track;
            p.lineWidth = 2;
            p.BeginPath();
            p.MoveTo(new Vector2(rect.xMin, Y(0)));
            p.LineTo(new Vector2(rect.xMax, Y(0)));
            p.Stroke();

            running = 0;
            for (int i = 0; i < bars; i++)
            {
                long from, to;
                if (i < _steps.Length) { from = running; to = running + _steps[i]; running = to; }
                else { from = 0; to = running; }
                float x = rect.xMin + i * slot + (slot - barWidth) / 2;
                float top = Mathf.Min(Y(from), Y(to)), bottom = Mathf.Max(Y(from), Y(to));
                if (bottom - top < 2) bottom = top + 2;
                p.fillColor = to >= from ? _positive : _negative;
                p.BeginPath();
                p.MoveTo(new Vector2(x, top));
                p.LineTo(new Vector2(x + barWidth, top));
                p.LineTo(new Vector2(x + barWidth, bottom));
                p.LineTo(new Vector2(x, bottom));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
