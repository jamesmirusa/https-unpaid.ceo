using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// A soft drop shadow under a floating panel (HUD, dock, popups). UI Toolkit has no box-shadow, so a 9-slice
    /// shadow sprite (tools/art/ui/make_shadow.swift) is laid behind the panel as its previous sibling and follows the
    /// panel's layout. Display and visibility follow the panel too.
    /// </summary>
    public static class Shadow
    {
        public const string Class = "drop-shadow";
        const float Spread = 40f, Drop = 14f;

        public static VisualElement Behind(VisualElement panel)
        {
            var parent = panel.parent;
            if (parent == null) return null;
            var s = new VisualElement { name = panel.name + "-shadow", pickingMode = PickingMode.Ignore };
            s.AddToClassList(Class);
            s.style.position = Position.Absolute;
            parent.Insert(parent.IndexOf(panel), s);
            void Sync()
            {
                var r = panel.layout;
                bool shown = panel.resolvedStyle.display != DisplayStyle.None && panel.resolvedStyle.visibility == Visibility.Visible;
                s.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                s.style.left = r.x - Spread;
                s.style.top = r.y - Spread + Drop;
                s.style.width = r.width + 2 * Spread;
                s.style.height = r.height + 2 * Spread;
                s.style.opacity = panel.resolvedStyle.opacity;
            }
            panel.RegisterCallback<GeometryChangedEvent>(_ => Sync());
            s.schedule.Execute(Sync).Every(100);   // display/opacity changes don't always move geometry
            return s;
        }
    }
}
