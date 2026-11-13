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

        /// <summary>Pads <paramref name="root"/> by the device's insets now and whenever the panel's size changes.</summary>
        public static void Keep(VisualElement root)
        {
            void Apply()
            {
                var panelRoot = root.panel?.visualTree;
                if (panelRoot == null) return;
                var size = new Vector2(panelRoot.layout.width, panelRoot.layout.height);
                if (float.IsNaN(size.x) || size.x <= 0) return;
                var e = Insets(Screen.safeArea, new Vector2(Screen.width, Screen.height), size);
                root.style.paddingTop = e.top;
                root.style.paddingBottom = e.bottom;
                root.style.paddingLeft = e.left;
                root.style.paddingRight = e.right;
            }
            root.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            root.schedule.Execute(Apply).Every(1000);   // rotation or a split view can change the safe area without a resize
        }
    }
}
