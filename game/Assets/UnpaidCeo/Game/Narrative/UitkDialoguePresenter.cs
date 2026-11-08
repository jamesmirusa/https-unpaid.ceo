#nullable enable
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Yarn.Unity;

namespace UnpaidCeo.Game.Narrative
{
    /// <summary>
    /// Yarn Spinner 3 dialogue presenter drawn with UI Toolkit: speaker, line, a continue button
    /// and one button per option. Styling comes from USS classes (dialogue, dialogue__speaker, …).
    /// </summary>
    public sealed class UitkDialoguePresenter : DialoguePresenterBase
    {
        [SerializeField] UIDocument? document;

        /// <summary>Plays each line's voice clip (Yarn's localised line asset) and stops it on continue.</summary>
        public UnpaidCeo.Game.Audio.AudioDirector? Audio { get; set; }

        VisualElement? _panel;
        Label? _speaker;
        Label? _text;
        Button? _continue;
        VisualElement? _options;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return;
            _panel = new VisualElement { name = "dialogue" };
            _panel.AddToClassList("dialogue");
            _speaker = new Label { name = "dialogue-speaker" };
            _speaker.AddToClassList("dialogue__speaker");
            _text = new Label { name = "dialogue-text" };
            _text.AddToClassList("dialogue__text");
            _continue = new Button { name = "dialogue-continue", text = "▶" };
            _continue.AddToClassList("dialogue__continue");
            _options = new VisualElement { name = "dialogue-options" };
            _options.AddToClassList("dialogue__options");
            _panel.Add(_speaker);
            _panel.Add(_text);
            _panel.Add(_options);
            _panel.Add(_continue);
            document.rootVisualElement.Add(_panel);
            Show(false);
        }

        void Show(bool visible)
        {
            if (_panel != null) _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            Show(true);
            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            _options?.Clear();
            Show(false);
            return YarnTask.CompletedTask;
        }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            if (_text == null || _speaker == null || _continue == null) return;
            string? speaker = line.CharacterName;
            _speaker.text = speaker ?? string.Empty;
            _speaker.style.display = string.IsNullOrEmpty(speaker) ? DisplayStyle.None : DisplayStyle.Flex;
            _text.text = line.TextWithoutCharacterName.Text;
            _options?.Clear();
            _continue.style.display = DisplayStyle.Flex;
            Audio?.PlayVoice(line.Asset as AudioClip);

            var done = new YarnTaskCompletionSource();
            void Advance() => done.TrySetResult();
            _continue.clicked += Advance;
            using (token.NextContentToken.Register(Advance))
                await done.Task;
            _continue.clicked -= Advance;
            Audio?.StopVoice();
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            if (_options == null) return null;
            if (_continue != null) _continue.style.display = DisplayStyle.None;
            _options.Clear();

            var picked = new YarnTaskCompletionSource<DialogueOption?>();
            foreach (var option in dialogueOptions)
            {
                if (!option.IsAvailable) continue;
                var chosen = option;
                var button = new Button(() => picked.TrySetResult(chosen)) { text = option.Line.TextWithoutCharacterName.Text };
                button.AddToClassList("dialogue__option");
                _options.Add(button);
            }

            DialogueOption? result;
            using (cancellationToken.NextContentToken.Register(() => picked.TrySetResult(null)))
                result = await picked.Task;
            _options.Clear();
            return result;
        }
    }
}
