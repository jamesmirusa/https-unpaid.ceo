using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Graphics;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>Language, graphics mode, day/neon look and starting over.</summary>
    public sealed class SettingsScreen
    {
        static readonly (string code, string nativeName)[] LanguageNames = { ("en", "English"), ("ru", "Русский") };

        readonly UiRoot _ui;
        readonly Dictionary<string, Button> _languages = new Dictionary<string, Button>();
        readonly Dictionary<GraphicsMode, Button> _modes = new Dictionary<GraphicsMode, Button>();
        readonly Button _day, _neon;
        readonly Label _confirm;
        bool _armed;

        public event Action ResetRequested;

        public SettingsScreen(VisualElement root, UiRoot ui)
        {
            _ui = ui;
            UiText.Bind(root.Q<Label>("settings-title"), "settings.title");
            UiText.Bind(root.Q<Label>("settings-language-caption"), "settings.language");
            UiText.Bind(root.Q<Label>("settings-graphics-caption"), "settings.graphics");
            UiText.Bind(root.Q<Label>("settings-theme-caption"), "settings.theme");

            var languages = root.Q("settings-language");
            foreach (var (code, nativeName) in LanguageNames)
            {
                var b = UiText.Value(new Button(() => _ui.StartCoroutine(_ui.SetLanguage(code))), nativeName);
                b.AddToClassList("choice-button");
                _languages[code] = b;
                languages.Add(b);
            }

            var graphics = root.Q("settings-graphics");
            foreach (GraphicsMode mode in Enum.GetValues(typeof(GraphicsMode)))
            {
                var m = mode;
                var b = UiText.Bind(new Button(() => { GraphicsModeService.Save(m); GraphicsModeService.Apply(m); Refresh(); }),
                    "settings.graphics." + mode.ToString().ToLowerInvariant());
                b.AddToClassList("choice-button");
                _modes[mode] = b;
                graphics.Add(b);
            }

            var theme = root.Q("settings-theme");
            _day = UiText.Bind(new Button(() => { _ui.SetTheme(false); Refresh(); }), "settings.theme.day");
            _neon = UiText.Bind(new Button(() => { _ui.SetTheme(true); Refresh(); }), "settings.theme.neon");
            foreach (var b in new[] { _day, _neon }) { b.AddToClassList("choice-button"); theme.Add(b); }

            var reset = UiText.Bind(root.Q<Button>("settings-reset"), "settings.reset");
            _confirm = UiText.Bind(root.Q<Label>("settings-reset-confirm"), "settings.reset.confirm");
            reset.clicked += () =>
            {
                if (_armed) { _armed = false; ResetRequested?.Invoke(); }
                else _armed = true;
                Refresh();
            };
            Refresh();
        }

        public void Refresh()
        {
            foreach (var l in _languages) l.Value.EnableInClassList("choice-button--selected", l.Key == _ui.Language);
            foreach (var m in _modes) m.Value.EnableInClassList("choice-button--selected", m.Key == GraphicsModeService.Current);
            _day.EnableInClassList("choice-button--selected", !_ui.IsNeon);
            _neon.EnableInClassList("choice-button--selected", _ui.IsNeon);
            _confirm.style.display = _armed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
