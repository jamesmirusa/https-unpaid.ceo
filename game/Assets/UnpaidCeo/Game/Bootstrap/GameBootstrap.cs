#nullable enable
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Audio;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Graphics;
using UnpaidCeo.Game.Hero;
using UnpaidCeo.Game.Narrative;
using UnpaidCeo.Game.Services;
using UnpaidCeo.Game.Ui;
using Yarn.Unity;

namespace UnpaidCeo.Game.Bootstrap
{
    /// <summary>
    /// The game's single entry point (Boot scene): loads content and the Bronx, builds UI, audio and
    /// dialogue, then runs onboarding for a new player or resumes a save, and drives the month loop:
    /// Next month → report + timelapse → this month's scenes → save.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public enum Phase { Loading, Onboarding, Playing, Failed }

        public static readonly string[] ContentFiles = { "industries", "balance", "free_actions", "storylets_ch1", "npcs", "lessons" };
        public static string ContentAddress(string file) => "content/" + file;

        [SerializeField] YarnProject? yarnProject;

        public Phase Current { get; private set; } = Phase.Loading;
        public UiRoot? Ui { get; private set; }
        public OnboardingFlow? Onboarding { get; private set; }

        GameEngine? _engine;
        GameState? _state;
        CityView? _city;
        DayNeonController? _light;
        TimelapseDirector? _timelapse;
        AudioDirector? _audio;
        YarnBridge? _yarn;
        HeroView? _hero;
        bool _busy;

        void Start() => StartCoroutine(Boot());

        IEnumerator Boot()
        {
            yield return LocalizationSettings.InitializationOperation;
            GraphicsModeService.Apply(GraphicsModeService.TryLoadSaved(out var mode) ? mode : GraphicsMode.Optimal);

            ContentDb? content = null;
            yield return LoadContent(db => content = db);
            if (content == null) { Fail("content failed to load"); yield break; }
            _engine = new GameEngine(content);

            var district = Addressables.LoadSceneAsync(CityView.BronxAddress, LoadSceneMode.Additive);
            yield return district;
            if (district.Status != AsyncOperationStatus.Succeeded) { Fail("Bronx failed to load"); yield break; }
            _city = FindFirstObjectByType<CityView>();
            _light = FindFirstObjectByType<DayNeonController>();
            _timelapse = FindFirstObjectByType<TimelapseDirector>();

            _audio = new GameObject("Audio").AddComponent<AudioDirector>();
            _audio.transform.SetParent(transform);
            StartCoroutine(_audio.LoadMusic(_light));
            BuildDialogue();
            _hero = new GameObject("Hero").AddComponent<HeroView>();
            _hero.gameObject.AddComponent<HeroStroll>();
            if (_city != null && _city.HomeAnchor != null) _hero.transform.SetPositionAndRotation(_city.HomeAnchor.position, _city.HomeAnchor.rotation);

            var saved = SaveService.Load();
            bool resume = saved?.State != null && (saved.Status == LoadStatus.Ok || saved.Status == LoadStatus.Migrated);
            var seed = resume ? saved!.State!.Seed : (ulong)DateTime.UtcNow.Ticks;
            _state = resume ? saved!.State! : _engine.NewGame(seed, new HeroAppearance());

            Ui = new GameObject("UI").AddComponent<UiRoot>();
            Ui.transform.SetParent(transform);
            Ui.Initialize(_engine, _state);
            Ui.LineText = id => _yarn != null ? _yarn.LineText(id) : null;
            yield return Ui.SetLanguage(UiRoot.PickLanguage());
            _yarn!.SetLanguage(Ui.Language);
            Ui.ThemeChanged += neon => { if (_light != null) _light.Set(neon ? TimeOfDay.Night : TimeOfDay.Day); };
            Ui.ResetRequested += () => { SaveService.Delete(); SceneManager.LoadScene(0); };
            _city?.Sync(_state);

            if (resume)
            {
                StartCoroutine(_hero.Apply(_state.Hero));
                StartPlaying();
                PlayScenes();
                yield break;
            }

            Current = Phase.Onboarding;
            Onboarding = new OnboardingFlow(_engine, _state, Ui, _hero, id => _yarn!.LineText(id), (int)(seed % int.MaxValue));
            Onboarding.LineShown += PlayLine;
            Onboarding.HeroChosen += a => StartCoroutine(_hero.Apply(a));
            Onboarding.Completed += report =>
            {
                _city?.Sync(_state);
                SaveService.Save(_state);
                StartPlaying();
                PlayScenes();
            };
            Onboarding.Start();
        }

        IEnumerator LoadContent(Action<ContentDb?> done)
        {
            var texts = new string?[ContentFiles.Length];
            for (int i = 0; i < ContentFiles.Length; i++)
            {
                var h = Addressables.LoadAssetAsync<TextAsset>(ContentAddress(ContentFiles[i]));
                yield return h;
                texts[i] = h.Status == AsyncOperationStatus.Succeeded ? h.Result.text : null;
            }
            if (Array.Exists(texts, t => t == null)) { done(null); yield break; }
            done(ContentDb.FromJson(new ContentFiles
            {
                Industries = texts[0], Balance = texts[1], FreeActions = texts[2],
                Storylets = texts[3], Npcs = texts[4], Lessons = texts[5],
            }));
        }

        void BuildDialogue()
        {
            var go = new GameObject("Dialogue");
            go.SetActive(false);
            go.transform.SetParent(transform);
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Dialogue Panel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/UnpaidTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
            panel.sortingOrder = 10;
            go.AddComponent<UIDocument>().panelSettings = panel;
            var presenter = go.AddComponent<UitkDialoguePresenter>();
            presenter.Audio = _audio;
            presenter.OptionsShown = () => _hero?.Think();
            var provider = go.AddComponent<BuiltinLocalisedLineProvider>();
            var runner = go.AddComponent<DialogueRunner>();
            // Assign before activation so DialogueRunner.Awake sees them (the serialized fields are not public).
            SetField(runner, "yarnProject", yarnProject);
            SetField(runner, "lineProvider", provider);
            runner.DialoguePresenters = new DialoguePresenterBase[] { presenter };
            runner.autoStart = false;
            _yarn = go.AddComponent<YarnBridge>();
            go.SetActive(true);
        }

        static void SetField(object target, string name, object? value) =>
            target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
                .SetValue(target, value);

        void PlayLine(string lineId)
        {
            if (_audio == null || Ui == null) return;
            StartCoroutine(PlayClip(AudioAddresses.Line(lineId, AudioAddresses.VoiceLanguage(Ui.Language))));
        }

        IEnumerator PlayClip(string address)
        {
            var h = Addressables.LoadAssetAsync<AudioClip>(address);
            yield return h;
            if (h.Status == AsyncOperationStatus.Succeeded) _audio!.PlayVoice(h.Result);
        }

        void StartPlaying()
        {
            Current = Phase.Playing;
            Ui!.NextMonthRequested += () => { if (!_busy) StartCoroutine(NextMonth()); };
        }

        IEnumerator NextMonth()
        {
            _busy = true;
            MonthReport report;
            try { report = _engine!.EndMonth(_state!, Ui!.CurrentDecisions()); }
            catch (Exception e) { Debug.LogException(e); _busy = false; yield break; }
            _city?.Sync(_state!);
            if (_timelapse != null)
            {
                _timelapse.Language = Ui.Language;
                _timelapse.Play(report);
                while (_timelapse.IsPlaying) yield return null;
            }
            Ui.ShowReport(report);
            _hero?.React(report);
            Ui.Open(UiRoot.Company);
            SaveService.Save(_state!);
            _busy = false;
            PlayScenes();
        }

        async void PlayScenes()
        {
            if (_yarn == null || _engine == null || _state == null) return;
            _busy = true;
            try
            {
                _engine.BeginMonth(_state);
                _yarn.Bind(_engine, _state);
                await _yarn.PlayPendingAsync();
                Ui?.RefreshHud();
                SaveService.Save(_state);
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { _busy = false; }
        }

        void Fail(string why)
        {
            Current = Phase.Failed;
            Debug.LogError("Boot failed: " + why);
        }
    }
}
