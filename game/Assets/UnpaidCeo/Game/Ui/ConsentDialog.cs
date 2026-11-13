#nullable enable
using UnityEngine.UIElements;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Asked once (after onboarding): may the game send anonymous play statistics? Nothing is recorded before a yes; the
    /// answer can be changed any time in Settings.
    /// </summary>
    public sealed class ConsentDialog
    {
        readonly UiRoot _ui;
        VisualElement _panel;

        public VisualElement Root { get; }

        public ConsentDialog(UiRoot ui)
        {
            _ui = ui;
            Root = new VisualElement { name = "consent" };
            Root.AddToClassList("modal");
            var panel = _panel = new VisualElement();
            panel.AddToClassList("modal__panel");
            panel.Add(UiText.Label("consent.title", "section-title"));
            panel.Add(UiText.Label("consent.text", "hint"));
            var row = new VisualElement();
            row.AddToClassList("choice-row");
            var allow = UiText.Bind(new Button(() => Choose(true)) { name = "consent-allow" }, "consent.allow");
            var deny = UiText.Bind(new Button(() => Choose(false)) { name = "consent-deny" }, "consent.deny");
            foreach (var b in new[] { allow, deny }) { b.AddToClassList("choice-button"); row.Add(b); }
            panel.Add(row);
            Root.Add(panel);
            Root.style.display = DisplayStyle.None;
        }

        public void Ask()
        {
            if (!AnalyticsConsent.Asked) { Root.style.display = DisplayStyle.Flex; Kit.PopIn.Play(_panel); }
        }

        public void Choose(bool on)
        {
            AnalyticsConsent.Set(on);
            _ui.Online.Analytics.SetConsent(on);
            Root.style.display = DisplayStyle.None;
        }
    }
}
