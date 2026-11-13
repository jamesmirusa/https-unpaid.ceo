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

        /// <summary>Stage 5B · what an option line is (tone, knowledge lock, price), by its line id; null = plain.</summary>
        public System.Func<string, UnpaidCeo.Game.Ui.DialogueOptionsView.Option?>? Describe { get; set; }
        /// <summary>Stage 5B · the scene's host and their trust, for the nameplate's hearts; null = no hearts.</summary>
        public System.Func<(string npc, int trust)?>? Host { get; set; }
        /// <summary>The UI language (prices on the answers).</summary>
        public System.Func<string>? Language { get; set; }
        /// <summary>Seconds per character while a line is typed (a tap shows it all at once).</summary>
        public const float TypeSeconds = 0.022f, TypeMax = 1.2f;

        VisualElement? _hearts;
        Label? _heartsValue;
        VisualElement? _heartIcon;
        bool _typing;
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
            _hearts = new VisualElement { name = "dialogue-hearts" };
            _hearts.AddToClassList("dialogue__hearts");
            _heartIcon = UnpaidCeo.Game.Ui.Kit.Icons.Game("trust", "dialogue__heart-icon");
            _hearts.Add(_heartIcon);
            _heartsValue = new Label { name = "dialogue-hearts-value" };
            _heartsValue.AddToClassList("dialogue__hearts-value");
            _hearts.Add(_heartsValue);
            _hearts.style.display = DisplayStyle.None;
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
            var plate = new VisualElement { name = "dialogue-nameplate" };
            plate.AddToClassList("dialogue__nameplate");
            plate.Add(_speaker);
            plate.Add(_hearts);
            _panel.Add(plate);
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
            RefreshHearts(!string.IsNullOrEmpty(speaker));
            string full = line.TextWithoutCharacterName.Text;
            _options?.Clear();
            _continue.style.display = DisplayStyle.Flex;
            Audio?.PlayVoice(line.Asset as AudioClip);

            var done = new YarnTaskCompletionSource();
            // the line types itself out (≤ 1.2 s); the first tap shows it all, the next one goes on
            void Advance() { if (_typing) { _typing = false; return; } done.TrySetResult(); }
            _typing = true;
            Type(full);
            _continue.clicked += Advance;
            _advance = Advance;
            using (token.NextContentToken.Register(Advance))
                await done.Task;
            _advance = null;
            _continue.clicked -= Advance;
            Audio?.StopVoice();
        }

        async void Type(string full)
        {
            if (_text == null) return;
            float per = Mathf.Min(TypeSeconds, TypeMax / Mathf.Max(1, full.Length)), start = Time.unscaledTime;
            while (_typing && this != null)
            {
                int n = Mathf.Min(full.Length, Mathf.FloorToInt((Time.unscaledTime - start) / per));
                _text.text = full.Substring(0, n);
                if (n >= full.Length) break;
                await YarnTask.Yield();
            }
            _typing = false;
            if (_text != null) _text.text = full;
        }

        void RefreshHearts(bool show)
        {
            var host = show ? Host?.Invoke() : null;
            if (_hearts == null) return;
            _hearts.style.display = host != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (host == null) return;
            int trust = host.Value.trust;
            _heartsValue!.text = UnpaidCeo.Core.Narrative.Relationships.Level(trust).ToString();
            _heartIcon!.style.backgroundImage = new StyleBackground(UnpaidCeo.Game.Ui.Kit.Icons.Load(
                UnpaidCeo.Game.Ui.Kit.Icons.GameFolder + (trust < 0 ? "trust_cold" : "trust")));
        }

        /// <summary>"Tony +2 ♥" over the panel for a second after an answer, with a tick and the heart's pulse.</summary>
        public void ShowTrust(string npcKey, int delta)
        {
            if (_panel == null || delta == 0) return;
            var f = new VisualElement { name = "trust-float", pickingMode = PickingMode.Ignore };
            f.AddToClassList("trust-float");
            f.EnableInClassList("trust-float--down", delta < 0);
            var name = UnpaidCeo.Game.Ui.UiText.Label("npc." + npcKey);
            f.Add(name);
            f.Add(new Label((delta > 0 ? " +" : " " + UnpaidCeo.Game.Ui.EffectChips.Minus) + System.Math.Abs(delta)));
            f.Add(UnpaidCeo.Game.Ui.Kit.Icons.Game(delta < 0 ? "trust_cold" : "trust"));
            _panel.Add(f);
            f.schedule.Execute(() => f.AddToClassList("trust-float--fly")).StartingIn(30);
            f.schedule.Execute(f.RemoveFromHierarchy).StartingIn(1100);
            UnpaidCeo.Game.Ui.Kit.UiSound.Play(delta > 0 ? UnpaidCeo.Game.Ui.Kit.UiSound.Success : UnpaidCeo.Game.Ui.Kit.UiSound.Closed);
            RefreshHearts(true);
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            OptionsShown?.Invoke();
            if (_options == null) return null;
            if (_continue != null) _continue.style.display = DisplayStyle.None;
            _options.Clear();

            var picked = new YarnTaskCompletionSource<DialogueOption?>();
            var shown = new System.Collections.Generic.List<DialogueOption>();
            var views = new System.Collections.Generic.List<UnpaidCeo.Game.Ui.DialogueOptionsView.Option>();
            foreach (var option in dialogueOptions)
            {
                if (!option.IsAvailable) continue;
                var v = Describe?.Invoke(option.Line.TextID) ?? new UnpaidCeo.Game.Ui.DialogueOptionsView.Option();
                v.Text = option.Line.TextWithoutCharacterName.Text;
                shown.Add(option);
                views.Add(v);
            }
            UnpaidCeo.Game.Ui.DialogueOptionsView.Build(_options, views, i => picked.TrySetResult(shown[i]), Language?.Invoke() ?? "en");

            DialogueOption? result;
            using (cancellationToken.NextContentToken.Register(() => picked.TrySetResult(null)))
                result = await picked.Task;
            _options.Clear();
            return result;
        }
    }
}
