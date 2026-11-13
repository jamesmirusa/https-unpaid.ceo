using UnityEngine;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// Stage 6 · keeps the interface out of the notch, the Dynamic Island and the home bar (Screen.safeArea), in UI
    /// Toolkit panel units. On the web and on desktops the safe area is the whole screen: nothing changes.
    /// </summary>
    public static class SafeArea
    {
        public struct Edges { public float top, bottom, left, right; }

        /// <param name="safe">Screen.safeArea in screen pixels (origin bottom-left).</param>
        /// <param name="screen">Screen size in pixels.</param>
        /// <param name="panel">The panel's size in its own units (after ScaleWithScreenSize).</param>
        public static Edges Insets(Rect safe, Vector2 screen, Vector2 panel)
        {
            if (screen.x <= 0 || screen.y <= 0) return default;
            float sx = panel.x / screen.x, sy = panel.y / screen.y;
            return new Edges
            {
                top = Mathf.Max(0f, screen.y - safe.yMax) * sy,
                bottom = Mathf.Max(0f, safe.y) * sy,
                left = Mathf.Max(0f, safe.x) * sx,
                right = Mathf.Max(0f, screen.x - safe.xMax) * sx,
            };
        }

        public const string BottomClass = "safe-bottom", TopClass = "safe-top";
        const string BaseKey = "safe-base";

        /// <summary>
        /// Pads <paramref name="root"/> by <paramref name="e"/> (flex children follow), and lifts the panels pinned to an
        /// edge (classes <see cref="BottomClass"/>, <see cref="TopClass"/>): absolute children ignore the parent's padding.
        /// Idempotent: each panel keeps its own offset from the stylesheet and gets the inset added once.
        /// </summary>
        public static void Apply(VisualElement root, Edges e)
        {
            root.style.paddingTop = e.top;
            root.style.paddingBottom = e.bottom;
            root.style.paddingLeft = e.left;
            root.style.paddingRight = e.right;
            foreach (var p in root.Query(className: BottomClass).ToList()) Lift(p, e.bottom, false);
            foreach (var p in root.Query(className: TopClass).ToList()) Lift(p, e.top, true);
        }

        static void Lift(VisualElement p, float inset, bool top)
        {
            if (!(p.userData is float baseOffset))
            {
                float resolved = top ? p.resolvedStyle.top : p.resolvedStyle.bottom;
                if (float.IsNaN(resolved)) return;   // not laid out yet: the next pass lifts it
                baseOffset = resolved;
                p.userData = baseOffset;
            }
            if (top) p.style.top = baseOffset + inset; else p.style.bottom = baseOffset + inset;
        }

        /// <summary>Pads <paramref name="root"/> by the device's insets now and whenever the panel's size changes.</summary>
        public static void Keep(VisualElement root)
        {
            void Refresh()
            {
                var panelRoot = root.panel?.visualTree;
                if (panelRoot == null) return;
                var size = new Vector2(panelRoot.layout.width, panelRoot.layout.height);
                if (float.IsNaN(size.x) || size.x <= 0) return;
                Apply(root, Insets(Screen.safeArea, new Vector2(Screen.width, Screen.height), size));
            }
            root.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            root.schedule.Execute(Refresh).Every(1000);   // panels built later (a room, the hero creator) get lifted on the next pass
        }
    }
}
