using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>Full-screen title card ("Chapter 2 · Brooklyn") shown while the next district streams in.</summary>
    public sealed class ChapterTransition
    {
        readonly Label _title, _subtitle;
        IVisualElementScheduledItem _timer;

        public VisualElement Root { get; }
        public bool IsPlaying { get; private set; }

        public ChapterTransition()
        {
            Root = new VisualElement { name = "chapter-transition" };
            Root.AddToClassList("chapter-card");
            _title = UiText.Label("chapter.2.title", "chapter-card__title");
            _subtitle = UiText.Label("chapter.2.subtitle", "chapter-card__subtitle");
            Root.Add(_title);
            Root.Add(_subtitle);
            Root.RegisterCallback<ClickEvent>(_ => Hide());
            Root.style.display = DisplayStyle.None;
        }

        /// <summary>Shows the card; it hides itself after <paramref name="seconds"/> (or on tap, or when <see cref="Hide"/> is called).</summary>
        public void Play(int chapter, float seconds = 3f)
        {
            UiText.Bind(_title, $"chapter.{chapter}.title");
            UiText.Bind(_subtitle, $"chapter.{chapter}.subtitle");
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            IsPlaying = true;
            _timer?.Pause();
            _timer = Root.schedule.Execute(Hide).StartingIn((long)(seconds * 1000));
        }

        public void Hide()
        {
            _timer?.Pause();
            Root.style.display = DisplayStyle.None;
            IsPlaying = false;
        }
    }
}
