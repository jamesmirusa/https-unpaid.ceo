using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// CEO Academy tab (UI/Academy.uxml): unlocked lessons, the mentor's explanation and a 3-question quiz.
    /// All text comes from the "Academy" string table; answers go through <see cref="GameEngine.AnswerQuiz"/>.
    /// </summary>
    public sealed class AcademyScreen : IDisposable
    {
        public const string Table = "Academy";

        readonly GameEngine _engine;
        readonly GameState _state;
        readonly Label _title, _lessonTitle, _lessonBody, _quizTitle, _feedback, _disclaimer;
        readonly ScrollView _list;
        readonly VisualElement _quiz;
        StringTable _strings;
        string _selected;

        public AcademyScreen(VisualElement root, GameEngine engine, GameState state)
        {
            _engine = engine;
            _state = state;
            _title = root.Q<Label>("academy-title");
            _list = root.Q<ScrollView>("lesson-list");
            _lessonTitle = root.Q<Label>("lesson-title");
            _lessonBody = root.Q<Label>("lesson-body");
            _quizTitle = root.Q<Label>("quiz-title");
            _quiz = root.Q<VisualElement>("quiz");
            _feedback = root.Q<Label>("quiz-feedback");
            _disclaimer = root.Q<Label>("academy-disclaimer");
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            LoadStrings();
        }

        public void Dispose() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        void OnLocaleChanged(Locale _) => LoadStrings();

        // Async on purpose: WebGL cannot load string tables synchronously.
        void LoadStrings()
        {
            var op = LocalizationSettings.StringDatabase.GetTableAsync(Table);
            if (op.IsDone) OnLoaded(op.Result);
            else op.Completed += h => OnLoaded(h.Result);
        }

        void OnLoaded(StringTable table)
        {
            _strings = table;
            Refresh();
        }

        string T(string key) => _strings?.GetEntry(key)?.GetLocalizedString() ?? key;

        /// <summary>Redraws the lesson list and the selected lesson; call after a month ends.</summary>
        public void Refresh()
        {
            _title.text = T("academy.title");
            _disclaimer.text = T("academy.disclaimer");
            _list.Clear();
            foreach (var lesson in _engine.Content.LessonsInOrder)
            {
                bool unlocked = _state.LessonsSeen.ContainsKey(lesson.Id);
                if (_selected == null && unlocked) _selected = lesson.Id;
                var id = lesson.Id;
                var item = new Button(() => { _selected = id; _feedback.text = string.Empty; Refresh(); })
                {
                    text = unlocked ? T(lesson.TitleKey) : "🔒 " + T(lesson.TitleKey),
                };
                item.SetEnabled(unlocked);
                item.AddToClassList("academy__item");
                item.EnableInClassList("academy__item--selected", id == _selected);
                _list.Add(item);
            }
            ShowSelected();
        }

        void ShowSelected()
        {
            _quiz.Clear();
            if (_selected == null || !_engine.Content.Lessons.TryGetValue(_selected, out var lesson))
            {
                _lessonTitle.text = string.Empty;
                _lessonBody.text = T("academy.locked");
                _quizTitle.text = string.Empty;
                return;
            }
            _lessonTitle.text = T(lesson.TitleKey);
            _lessonBody.text = T(lesson.BodyKey);
            _quizTitle.text = T("academy.quiz.title");
            for (int q = 0; q < lesson.Quiz.Count; q++) _quiz.Add(Question(lesson, q));
        }

        VisualElement Question(LessonDef lesson, int q)
        {
            var box = new VisualElement();
            box.AddToClassList("academy__question");
            var label = new Label(T(lesson.Quiz[q].QuestionKey));
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            bool answered = _state.QuizAnswers.TryGetValue(lesson.Id + "/" + q, out int chosen);
            for (int o = 0; o < lesson.Quiz[q].OptionKeys.Count; o++)
            {
                int option = o;
                var button = new Button(() => Answer(lesson, q, option)) { text = T(lesson.Quiz[q].OptionKeys[o]) };
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
            _feedback.text = T(correct ? "academy.quiz.correct" : "academy.quiz.wrong");
            ShowSelected();
        }
    }
}
