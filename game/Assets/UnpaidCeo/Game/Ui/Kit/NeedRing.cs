using UnityEngine;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// A need as a small ring (AUTOPILOT §8): the need's icon inside a circular progress in the need's colour, drawn with
    /// Painter2D (a track, then the arc from twelve o'clock clockwise).
    /// </summary>
    public sealed class NeedRing : VisualElement
    {
        float _value;
        Color _color = Color.white, _track = new Color(1f, 1f, 1f, 0.16f);
        const float Stroke = 9f;

        public float Value
        {
            get => _value;
            set { float v = Mathf.Clamp01(value); if (Mathf.Approximately(v, _value)) return; _value = v; MarkDirtyRepaint(); }
        }

        public NeedRing(string id, string gameIcon, Color color)
        {
            name = "hud-ring-" + id;
            AddToClassList("need-ring");
            _color = color;
            pickingMode = PickingMode.Ignore;
            var icon = Icons.Game(gameIcon, "need-ring__icon");
            Add(icon);
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float radius = Mathf.Min(r.width, r.height) * 0.5f - Stroke * 0.5f;
            if (radius <= 0) return;
            var c = r.center;
            var p = ctx.painter2D;
            p.lineWidth = Stroke;
            p.lineCap = LineCap.Round;
            p.strokeColor = _track;
            p.BeginPath();
            p.Arc(c, radius, 0f, 360f);
            p.Stroke();
            if (_value <= 0.001f) return;
            p.strokeColor = _color;
            p.BeginPath();
            p.Arc(c, radius, -90f, -90f + 360f * _value);
            p.Stroke();
        }
    }
}
