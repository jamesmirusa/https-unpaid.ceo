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

        /// <summary>Raised when the player is about to choose (the hero plays its think clip).</summary>
        public System.Action? OptionsShown { get; set; }

        VisualElement? _backdrop;
        VisualElement? _panel;
        System.Action? _advance;
        Label? _speaker;
        Label? _text;
        Button? _continue;
        VisualElement? _options;
        VisualElement? _consequences;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return;
            _panel = new VisualElement { name = "dialogue" };
            _panel.AddToClassList("dialogue");
            _panel.AddToClassList("theme-day");   // the colour tokens (--panel, --accent) live on the theme class; this document has no .app root
            _speaker = new Label { name = "dialogue-speaker" };
            _speaker.AddToClassList("dialogue__speaker");
            _text = new Label { name = "dialogue-text" };
            _text.AddToClassList("dialogue__text");
            // A word from the string table: the UI font has no glyph for a play symbol, and a full-width button is hard to miss.
            _continue = UnpaidCeo.Game.Ui.UiText.Bind(new Button { name = "dialogue-continue" }, "dialogue.continue");
            _continue.AddToClassList("dialogue__continue");
            _options = new VisualElement { name = "dialogue-options" };
            _options.AddToClassList("dialogue__options");
            _consequences = new VisualElement { name = "dialogue-consequences" };
            _consequences.AddToClassList("dialogue__consequences");
            _consequences.style.display = DisplayStyle.None;
            _panel.Add(_speaker);
            _panel.Add(_text);
            _panel.Add(_consequences);
            _panel.Add(_options);
            _panel.Add(_continue);
            // A scene is a modal: the backdrop covers the game (nothing under it can be pressed) and a tap on it continues.
            _backdrop = new VisualElement { name = "dialogue-backdrop" };
            _backdrop.AddToClassList("dialogue-backdrop");
            _backdrop.RegisterCallback<ClickEvent>(_ => _advance?.Invoke());
            _backdrop.Add(_panel);
            document.rootVisualElement.Add(_backdrop);
            UnpaidCeo.Game.Ui.Kit.Feel.Attach(document.rootVisualElement);   // scene buttons answer the tap like the rest
            Show(false);
        }

        void Show(bool visible)
        {
            if (_backdrop != null) _backdrop.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            UnpaidCeo.Game.City.CameraRig.SceneOpen = visible;   // taps continue the scene, they do not drag the city
        }

        void OnDestroy() => UnpaidCeo.Game.City.CameraRig.SceneOpen = false;

        public override YarnTask OnDialogueStartedAsync()
        {
            Show(true);
            return YarnTask.CompletedTask;
        }

        /// <summary>What the choice just made did (cash, stats, trust), shown under the line until the scene ends.</summary>
        public void ShowConsequences(System.Collections.Generic.IEnumerable<UnpaidCeo.Core.Narrative.Effect> effects, string lang)
        {
            if (_consequences == null) return;
            int n = UnpaidCeo.Game.Ui.EffectChips.Fill(_consequences, effects, lang);
            _consequences.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            _options?.Clear();
            _consequences?.Clear();
            if (_consequences != null) _consequences.style.display = DisplayStyle.None;
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
            _advance = Advance;
            using (token.NextContentToken.Register(Advance))
                await done.Task;
            _advance = null;
            _continue.clicked -= Advance;
            Audio?.StopVoice();
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            OptionsShown?.Invoke();
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
