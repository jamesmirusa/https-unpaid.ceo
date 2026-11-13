using System;
using System.Linq;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>"Your company's story": decisions and their consequences, newest first.</summary>
    public sealed class JournalScreen
    {
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly VisualElement _list;
        readonly Label _empty;

        /// <summary>Resolves a Yarn line id to text in the current language (set by the dialogue bridge).</summary>
        public Func<string, string> LineText { get; set; }

        public JournalScreen(VisualElement root, GameEngine engine, GameState state)
        {
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("journal-title"), "journal.title");
            _empty = UiText.Bind(root.Q<Label>("journal-empty"), "journal.empty");
            _list = root.Q("journal-list");
            Refresh();
        }

        public void Refresh()
        {
            _list.Clear();
            _empty.style.display = _state.Journal.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var e in Enumerable.Reverse(_state.Journal))
            {
                var row = new VisualElement();
                row.AddToClassList("journal-entry");
                row.Add(UiText.ValueLabel(e.Month.ToString(), "journal-entry__month"));
                row.Add(Text(e));
                _list.Add(row);
            }
        }

        Label Text(JournalEntry e)
        {
            if (e.Args != null && e.Args.TryGetValue("storylet", out var storylet) && e.Args.TryGetValue("choice", out var choice))
            {
                var def = _engine.Content.Storylets.FirstOrDefault(s => s.Id == storylet);
                string line = null;
                if (def != null && def.ChoiceLines.TryGetValue(choice, out var lineId)) line = LineText?.Invoke(lineId);
                if (!string.IsNullOrEmpty(line)) return UiText.ValueLabel(line, "journal-entry__text");
                return UiText.Label("journal.decision", "journal-entry__text");
            }
            return UiText.Label(e.TextKey, "journal-entry__text");
        }
    }
}
