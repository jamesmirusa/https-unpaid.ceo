#nullable enable
using System;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>On top of the screens during a run: what is being played, month n/total, and the way back to the career.</summary>
    public sealed class RunBanner
    {
        readonly Label _months, _result;
        readonly Button _exit;

        public VisualElement Root { get; }
        public string? ResultKey { get; private set; }
        public event Action? ExitRequested;

        public RunBanner(string titleKey)
        {
            Root = new VisualElement { name = "run-banner" };
            Root.AddToClassList("run-banner");
            Root.Add(UiText.Label(titleKey, "run-banner__title"));
            _months = UiText.ValueLabel("", "run-banner__months");
            _months.name = "run-months";
            Root.Add(_months);
            _result = new Label { name = "run-result" };
            _result.AddToClassList("run-banner__result");
            _result.style.display = DisplayStyle.None;
            Root.Add(_result);
            _exit = UiText.Bind(new Button(() => ExitRequested?.Invoke()) { name = "run-exit" }, "run.exit");
            _exit.AddToClassList("choice-button");
            Root.Add(_exit);
        }

        public void Show(int done, int total) => UiText.Value(_months, $"{done}/{total}");

        /// <summary>The run is over: the result (or "queued") and, optionally, the place.</summary>
        public void ShowResult(string key, string? place = null)
        {
            ResultKey = key;
            UiText.Bind(_result, key);
            _result.style.display = DisplayStyle.Flex;
            if (place != null) UiText.Value(_months, place);
        }
    }
}
