using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// CEO Academy tab: unlocked lessons, the mentor's explanation and a 3-question quiz.
    /// Text is bound to the "Academy" string table; answers go through <see cref="GameEngine.AnswerQuiz"/>.
    /// </summary>
    public sealed class AcademyScreen
    {
        public const string Table = "Academy";

        /// <summary>After every quiz answer: lesson id and question index.</summary>
        public event System.Action<string, int> Answered;
        /// <summary>The player opened a lesson.</summary>
        public event System.Action<string> LessonViewed;
        /// <summary>Every question of a lesson is now answered correctly.</summary>
        public event System.Action<string> QuizPassed;

        readonly GameEngine _engine;
        readonly GameState _state;
        readonly Label _lessonTitle, _lessonBody, _quizTitle, _feedback;
        readonly ScrollView _list;
        readonly VisualElement _quiz;
        string _selected;

        public AcademyScreen(VisualElement root, GameEngine engine, GameState state)
        {
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("academy-title"), "academy.title", Table);
            UiText.Bind(root.Q<Label>("academy-disclaimer"), "academy.disclaimer", Table);
            _list = root.Q<ScrollView>("lesson-list");
            _lessonTitle = root.Q<Label>("lesson-title");
            _lessonBody = root.Q<Label>("lesson-body");
            _quizTitle = UiText.Bind(root.Q<Label>("quiz-title"), "academy.quiz.title", Table);
            _quiz = root.Q<VisualElement>("quiz");
            _feedback = root.Q<Label>("quiz-feedback");
            Refresh();
        }

        /// <summary>Redraws the lesson list and the selected lesson; call after a month ends.</summary>
        public void Refresh()
        {
            _list.Clear();
            foreach (var lesson in _engine.Content.LessonsInOrder)
            {
                bool unlocked = _state.LessonsSeen.ContainsKey(lesson.Id);
                if (_selected == null && unlocked) _selected = lesson.Id;
                var id = lesson.Id;
                var item = UiText.Bind(new Button(() => { _selected = id; Hide(_feedback); Refresh(); LessonViewed?.Invoke(id); }) { name = "lesson-" + id }, lesson.TitleKey, Table);
                item.SetEnabled(unlocked);
                item.AddToClassList("academy__item");
                item.EnableInClassList("academy__item--locked", !unlocked);
                item.EnableInClassList("academy__item--selected", id == _selected);
                _list.Add(item);
            }
            ShowSelected();
        }

        static void Hide(VisualElement e) => e.style.display = DisplayStyle.None;
        static void Show(VisualElement e) => e.style.display = DisplayStyle.Flex;

        void ShowSelected()
        {
            _quiz.Clear();
            if (_selected == null || !_engine.Content.Lessons.TryGetValue(_selected, out var lesson))
            {
                Hide(_lessonTitle);
                Hide(_quizTitle);
                Hide(_feedback);
                UiText.Bind(_lessonBody, "academy.locked", Table);
                return;
            }
            Show(_lessonTitle);
            Show(_quizTitle);
            UiText.Bind(_lessonTitle, lesson.TitleKey, Table);
            UiText.Bind(_lessonBody, lesson.BodyKey, Table);
            for (int q = 0; q < lesson.Quiz.Count; q++) _quiz.Add(Question(lesson, q));
        }

        VisualElement Question(LessonDef lesson, int q)
        {
            var box = new VisualElement();
            box.AddToClassList("academy__question");
            box.Add(UiText.Label(lesson.Quiz[q].QuestionKey, "academy__question-text", Table));

            bool answered = _state.QuizAnswers.TryGetValue(lesson.Id + "/" + q, out int chosen);
            for (int o = 0; o < lesson.Quiz[q].OptionKeys.Count; o++)
            {
                int option = o;
                var button = UiText.Bind(new Button(() => Answer(lesson, q, option)), lesson.Quiz[q].OptionKeys[o], Table);
                button.AddToClassList("academy__option");
                if (answered)
                {
                    button.EnableInClassList("academy__option--correct", o == lesson.Quiz[q].CorrectIndex);
                    button.EnableInClassList("academy__option--wrong", o == chosen && chosen != lesson.Quiz[q].CorrectIndex);
                }
                box.Add(button);
            }
            return box;
        }

        void Answer(LessonDef lesson, int q, int option)
        {
            bool correct = _engine.AnswerQuiz(_state, lesson.Id, q, option);
            UiText.Bind(_feedback, correct ? "academy.quiz.correct" : "academy.quiz.wrong", Table);
            Show(_feedback);
            ShowSelected();
            Answered?.Invoke(lesson.Id, q);
            bool passed = true;
            for (int i = 0; i < lesson.Quiz.Count; i++)
                passed &= _state.QuizAnswers.TryGetValue(lesson.Id + "/" + i, out int c) && c == lesson.Quiz[i].CorrectIndex;
            if (correct && passed) QuizPassed?.Invoke(lesson.Id);
        }
    }
}
