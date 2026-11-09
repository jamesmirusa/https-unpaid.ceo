#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Audio;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Graphics;
using UnpaidCeo.Game.Hero;
using UnpaidCeo.Game.Narrative;
using UnpaidCeo.Game.Services;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Ui;
using Yarn.Unity;

namespace UnpaidCeo.Game.Bootstrap
{
    /// <summary>
    /// The game's single entry point (Boot scene): loads content and the Bronx, builds UI, audio and
    /// dialogue, then runs onboarding for a new player or resumes a save, and drives the month loop:
    /// Next month → report + timelapse → this month's scenes → save. Manual months come from the daily turn
    /// budget; offline autopilot runs on launch; daily missions count manual play.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public enum Phase { Loading, Onboarding, Playing, Failed }

        public static readonly string[] ContentFiles = UnpaidCeo.Core.Online.ContentPack.Files;
        public static string ContentAddress(string file) => "content/" + file;

        [SerializeField] YarnProject? yarnProject;

        public Phase Current { get; private set; } = Phase.Loading;
        public UiRoot? Ui { get; private set; }
        public OnboardingFlow? Onboarding { get; private set; }
        /// <summary>Offline until the background connection to Unity Gaming Services answers (it never holds up play).</summary>
        public IOnlineServices Online { get; private set; } = new OfflineServices();

        GameEngine? _engine;
        GameState? _state;
        string? _contentVersion;
        CloudSync? _sync;
        SubmissionQueue? _queue;
        RunController? _runs;
        RunBanner? _banner;
        CityView? _city;
        DayNeonController? _light;
        TimelapseDirector? _timelapse;
        AudioDirector? _audio;
        YarnBridge? _yarn;
        HeroView? _hero;
        PlayerMeta? _meta;
        AsyncOperationHandle<UnityEngine.ResourceManagement.ResourceProviders.SceneInstance> _district;
        bool _busy, _swapping;
        float _nextTick;
        int _ticks;

        void Start() => StartCoroutine(Boot());

        /// <summary>Addressable scene of the chapter's district: Bronx (1), Brooklyn (2), Queens (3).</summary>
        public static string DistrictAddress(int chapter) => "district_" + Chapters.District(chapter);

        static string DistrictMusic(int chapter) => AudioAddresses.DistrictMusic(Chapters.District(chapter));

        /// <summary>Loads the district for <paramref name="chapter"/>, then unloads the current one (behind the chapter card).</summary>
        public IEnumerator SwapDistrict(int chapter)
        {
            _swapping = true;
            var old = _district;
            yield return LoadDistrict(chapter);
            bool loaded = !old.IsValid() || !_district.Equals(old);
            if (loaded && old.IsValid()) yield return Addressables.UnloadSceneAsync(old);
            if (loaded)
            {
                if (_state != null) _city?.Sync(_state);
                if (_light != null && Ui != null) _light.Set(Ui.IsNeon ? TimeOfDay.Night : TimeOfDay.Day);
                PlaceHeroAtHome();
                if (_audio != null) StartCoroutine(_audio.LoadDayTheme(DistrictMusic(chapter)));
            }
            _swapping = false;
        }

        /// <summary>Loads a district additively; on success its city, light and timelapse become current.</summary>
        IEnumerator LoadDistrict(int chapter)
        {
            var handle = Addressables.LoadSceneAsync(DistrictAddress(chapter), LoadSceneMode.Additive);
            yield return handle;
            if (handle.Status != AsyncOperationStatus.Succeeded) { Debug.LogError($"{DistrictAddress(chapter)} failed to load"); yield break; }
            _district = handle;
            var scene = handle.Result.Scene;
            _city = InScene<CityView>(scene);
            _light = InScene<DayNeonController>(scene);
            _timelapse = InScene<TimelapseDirector>(scene);
        }

        static T? InScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        void PlaceHeroAtHome()
        {
            if (_hero != null && _city != null && _city.HomeAnchor != null)
                _hero.transform.SetPositionAndRotation(_city.HomeAnchor.position, _city.HomeAnchor.rotation);
        }

        /// <summary>Connects in the background; screens see the result through <see cref="UiRoot.Online"/>.</summary>
        async void StartOnline()
        {
            var online = await OnlineBootstrap.Start();
            if (this == null) return;   // the boot scene went away meanwhile
            Online = online;
            if (Ui != null) Ui.Online = online;
            if (online.Available) await SyncOnline(online);
        }

        /// <summary>
        /// Once the game is loaded: tells the server we are here (pacing counts from the first visit), settles device vs
        /// cloud save — a longer cloud game is downloaded and the game restarts on it — then sends queued runs.
        /// </summary>
        async Task SyncOnline(IOnlineServices online)
        {
            while (this != null && Current == Phase.Loading) await Awaitable.NextFrameAsync();
            if (this == null || _state == null || Current == Phase.Failed) return;
            await online.Runs.Hello();
            _sync = new CloudSync(online.Saves, DeviceClock.NowUnix);
            var adopt = await _sync.Pull(CloudSync.Envelope(_state, _meta, DeviceClock.NowUnix()));
            if (this == null) return;
            if (adopt?.SaveJson != null && Adopt(adopt)) return;
            await FlushQueue();
            Ui?.RankingPanel.RefreshSoon();   // the duel-waiting card on the City screen
        }

        /// <summary>Makes the cloud save the device save and restarts on it; false if it cannot be read.</summary>
        bool Adopt(CloudSaveEnvelope cloud)
        {
            var loaded = SaveCodec.Decode(cloud.SaveJson!);
            if (loaded.State == null || (loaded.Status != LoadStatus.Ok && loaded.Status != LoadStatus.Migrated)) return false;
            SaveService.Save(loaded.State);
            if (cloud.MetaJson != null) SaveService.SaveMeta(SaveCodec.DecodeMeta(cloud.MetaJson));
            SceneManager.LoadScene(0);
            return true;
        }

        /// <summary>After the device save is written: the cloud copy follows (debounced).</summary>
        void PushCloud()
        {
            if (_sync != null && _state != null) _ = _sync.Push(CloudSync.Envelope(_state, _meta, DeviceClock.NowUnix()));
        }

        /// <summary>The career so far goes to the server for the capital and market boards (queued while offline).</summary>
        void SubmitCareer()
        {
            if (_state == null || _contentVersion == null || _queue == null) return;
            var start = new RunStart { Kind = RunStart.Career, Seed = _state.Seed, Hero = _state.Hero };
            _queue.EnqueueCareer(RunSubmission.From(start, _state, _contentVersion));
            _ = FlushQueue();
        }

        /// <summary>
        /// A new account keeps the same player and cloud save (write it now); signing in or out changes the player, so
        /// the cloud is read again — the longer game wins, a new guest's empty cloud is filled from this device.
        /// </summary>
        async void OnAccountChanged(AccountChange change)
        {
            if (!Online.Available || _state == null) return;
            if (change == AccountChange.SignedUp) { PushCloud(); if (_sync != null) await _sync.FlushPending(); }
            else if (change == AccountChange.SignedIn || change == AccountChange.SignedOut) await SyncOnline(Online);
        }

        async Task FlushQueue()
        {
            if (_queue != null && Online.Available) await _queue.Flush(Online.Runs);
        }

        /// <summary>The career screens' events (also after a run, when the career screens are rebuilt).</summary>
        void WireCareerUi()
        {
            Ui!.ThemeChanged += neon => { if (_light != null) _light.Set(neon ? TimeOfDay.Night : TimeOfDay.Day); };
            Ui.ResetRequested += () => { CloudSync.MarkReset(); SaveService.Delete(); SceneManager.LoadScene(0); };
            Ui.AccountChanged += OnAccountChanged;
            Ui.QuizAnswered += OnQuizAnswered;
            Ui.ChapterAdvanced += chapter => { SaveService.Save(_state!); AfterAction(); StartCoroutine(SwapDistrict(chapter)); };
            Ui.IndustryChosen += _ => { SaveService.Save(_state!); AfterAction(); };
            Ui.LoanTaken += () => SaveService.Save(_state!);
            Ui.RankingPanel.Challenge.Requested += StartRun;
            Ui.RankingPanel.Friends.DuelRequested += StartRun;
            Ui.RankingPanel.Friends.BadgeGranted += () => SaveService.SaveMeta(_meta!);
        }

        /// <summary>
        /// Plays a challenge or duel run on the same screens: the career is saved and left untouched, the screens show the
        /// run's state, months use no daily turns. A run left half-way is continued from its slot.
        /// </summary>
        public void StartRun(RunStart start)
        {
            if (_runs == null || _state == null || Ui == null || Current != Phase.Playing || _busy || _runs.Current != null) return;
            start.Hero = _state.Hero;
            SaveService.Save(_state);
            ScenarioRun run;
            try { run = _runs.Begin(start); }
            catch (Exception e) { Debug.LogException(e); return; }
            ShowScreens(run.State, run);
            if (run.Finished) FinishRun();   // finished before but never sent
        }

        /// <summary>Back to the career (a run left half-way stays in its slot).</summary>
        public void EndRun()
        {
            _runs?.Leave();
            if (_state != null && Ui != null && Ui.RunMode) ShowScreens(_state, null);
        }

        /// <summary>Rebuilds the screens for the career or for a run.</summary>
        void ShowScreens(GameState shown, ScenarioRun? run)
        {
            string language = Ui!.Language;
            bool neon = Ui.IsNeon;
            Destroy(Ui.gameObject);
            Ui = new GameObject("UI").AddComponent<UiRoot>();
            Ui.transform.SetParent(transform);
            Ui.RunMode = run != null;
            Ui.Initialize(_engine!, shown);
            Ui.Online = Online;
            Ui.Meta = _meta!.Meta;
            Ui.LineText = id => _yarn != null ? _yarn.LineText(id) : null;
            StartCoroutine(Ui.SetLanguage(language));
            if (neon) Ui.SetTheme(true);
            if (run != null)
            {
                Ui.ThemeChanged += on => { if (_light != null) _light.Set(on ? TimeOfDay.Night : TimeOfDay.Day); };
                Ui.NextMonthRequested += () => { if (!_busy) StartCoroutine(NextRunMonth()); };
                Ui.LoanTaken += () => _runs?.Save();
                _banner = new RunBanner(run.Start.Kind == RunStart.Duel ? "run.duel" : "run.challenge");
                _banner.ExitRequested += EndRun;
                _banner.Show(run.MonthsDone, run.Months);
                Ui.Root.Add(_banner.Root);
                Ui.Open(UiRoot.Company);
            }
            else
            {
                _banner = null;
                WireCareerUi();
                Ui.NextMonthRequested += () => { if (!_busy && !_swapping) StartCoroutine(NextMonth()); };
                RefreshTurnsUi();
                RefreshMissionsUi();
                Ui.Open(UiRoot.Ranking);
            }
        }

        IEnumerator NextRunMonth()
        {
            if (_runs?.Current == null || Ui == null || _runs.Current.Finished) yield break;
            _busy = true;
            MonthReport report;
            try { report = _runs.PlayMonth(Ui.CurrentDecisions()); }
            catch (Exception e) { Debug.LogException(e); _busy = false; yield break; }
            Ui.ShowReport(report);
            Ui.Open(Ui.HomeTab);
            _banner?.Show(_runs.Current.MonthsDone, _runs.Current.Months);
            _busy = false;
            if (_runs.Current.Finished) FinishRun();
            yield break;
        }

        /// <summary>Sends the finished run (queued while offline) and shows the result; the banner leads back to the career.</summary>
        async void FinishRun()
        {
            if (_runs?.Current == null) return;
            var kind = _runs.Current.Start.Kind;
            var outcome = await _runs.Finish();
            if (this == null || outcome == null) return;
            if (outcome.Queued) { _banner?.ShowResult("run.queued"); return; }
            var result = outcome.Result;
            if (kind == RunStart.Challenge && result != null && result.Ok)
            {
                if (result.Rank >= 1 && result.Rank <= 10 && Achievements.Grant(_meta!.Meta, Achievements.ChallengeTop10)) SaveService.SaveMeta(_meta);
                _banner?.ShowResult("run.sent", result.Rank > 0 ? "#" + result.Rank : null);
            }
            else if (outcome.Duel != null && outcome.Duel.Error == null) OnDuelResult(outcome.Duel);
            else _banner?.ShowResult("run.refused");
        }

        /// <summary>A submitted duel run: the result now, or "waiting for the opponent".</summary>
        void OnDuelResult(DuelView duel)
        {
            if (duel.State == DuelView.Won && Achievements.Grant(_meta!.Meta, Achievements.DuelWinner)) SaveService.SaveMeta(_meta);
            _banner?.ShowResult("duel.state." + duel.State);
        }

        IEnumerator Boot()
        {
            _queue = new SubmissionQueue();
            StartOnline();
            yield return LocalizationSettings.InitializationOperation;
            GraphicsModeService.Apply(GraphicsModeService.TryLoadSaved(out var mode) ? mode : GraphicsMode.Optimal);

            ContentDb? content = null;
            yield return LoadContent(db => content = db);
            if (content == null) { Fail("content failed to load"); yield break; }
            _engine = new GameEngine(content);

            var saved = SaveService.Load();
            bool resume = saved?.State != null && (saved.Status == LoadStatus.Ok || saved.Status == LoadStatus.Migrated);
            var seed = resume ? saved!.State!.Seed : (ulong)DateTime.UtcNow.Ticks;
            _state = resume ? saved!.State! : _engine.NewGame(seed, new HeroAppearance());
            _meta = SaveService.LoadMeta();

            yield return LoadDistrict(_state.Chapter);
            if (_city == null) { Fail(DistrictAddress(_state.Chapter) + " failed to load"); yield break; }

            _audio = new GameObject("Audio").AddComponent<AudioDirector>();
            _audio.transform.SetParent(transform);
            StartCoroutine(_audio.LoadMusic(() => _light != null ? _light.NightAmount : 0f, DistrictMusic(_state.Chapter)));
            BuildDialogue();
            _hero = new GameObject("Hero").AddComponent<HeroView>();
            _hero.gameObject.AddComponent<HeroStroll>();
            PlaceHeroAtHome();

            Ui = new GameObject("UI").AddComponent<UiRoot>();
            Ui.transform.SetParent(transform);
            Ui.Initialize(_engine, _state);
            Ui.Online = Online;
            Ui.Meta = _meta.Meta;
            Ui.LineText = id => _yarn != null ? _yarn.LineText(id) : null;
            yield return Ui.SetLanguage(UiRoot.PickLanguage());
            _yarn!.SetLanguage(Ui.Language);
            _runs = new RunController(_engine, _queue!, () => Online, _contentVersion!);
            WireCareerUi();
            _city?.Sync(_state);

            if (resume)
            {
                StartCoroutine(_hero.Apply(_state.Hero));
                RunLaunchAutopilot();
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
                TurnBudget.Refresh(_meta.Turns, DeviceClock.NowUnix(), DeviceClock.UtcOffsetMinutes(), _engine.Content.Balance);
                DailyMissions.Sync(_meta.Meta, _meta.Turns, _state, _engine.Content);
                SaveService.SaveMeta(_meta);
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
            _contentVersion = ContentVersion.Of(texts!);
            done(UnpaidCeo.Core.Online.ContentPack.Load(texts!));
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
            Ui!.NextMonthRequested += () => { if (!_busy && !_swapping) StartCoroutine(NextMonth()); };
            RefreshTurnsUi();
            RefreshMissionsUi();
        }

        /// <summary>Offline months since the last visit (no manual turns used), then today's refill.</summary>
        void RunLaunchAutopilot()
        {
            DailyMissions.Sync(_meta!.Meta, _meta.Turns, _state!, _engine!.Content);
            var reports = TurnBudget.OnLaunch(_engine, _state!, _meta.Turns, DeviceClock.NowUnix(), DeviceClock.UtcOffsetMinutes());
            DailyMissions.SkipLog(_meta.Meta, _state!);
            if (reports.Count > 0)
            {
                foreach (var r in reports) Achievements.Update(_meta.Meta, _state!, r);
                _city?.Sync(_state!);
                Ui!.ShowReport(reports[reports.Count - 1]);
                SaveService.Save(_state!);
                PushCloud();
                SubmitCareer();
            }
            SaveService.SaveMeta(_meta);
        }

        IEnumerator NextMonth()
        {
            _busy = true;
            TurnBudget.Refresh(_meta!.Turns, DeviceClock.NowUnix(), DeviceClock.UtcOffsetMinutes(), _engine!.Content.Balance);
            if (!TurnBudget.TryConsume(_meta.Turns)) { RefreshTurnsUi(); _busy = false; yield break; }
            MonthReport report;
            try { report = _engine!.EndMonth(_state!, Ui!.CurrentDecisions()); }
            catch (Exception e) { Debug.LogException(e); _meta.Turns.ManualLeft++; _busy = false; yield break; }
            Achievements.Update(_meta.Meta, _state!, report);   // badges; the meta is saved with the action below
            _city?.Sync(_state!);
            if (_timelapse != null)
            {
                _timelapse.Language = Ui.Language;
                _timelapse.Play(report);
                while (_timelapse.IsPlaying) yield return null;
            }
            Ui.ShowReport(report);
            _hero?.React(report);
            Ui.Open(Ui.HomeTab);
            SaveService.Save(_state!);
            PushCloud();
            AfterAction();
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
                if (this == null || !isActiveAndEnabled) return; // destroyed mid-scene (reset deleted the save): do not write it back
                Ui?.AfterScenes();
                SaveService.Save(_state);
                PushCloud();
                SubmitCareer();   // the month and its scenes are complete: a stable point to rank
                AfterAction();
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { _busy = false; }
        }

        void OnQuizAnswered(string lessonId, int question)
        {
            if (_meta == null || _state == null || _engine == null) return;
            DailyMissions.RecordQuiz(_meta.Meta, _meta.Turns, _state.Seed, _engine.Content, lessonId, question);
            AfterAction();
        }

        /// <summary>After a manual action: count it toward today's missions, update the turn bar, save the meta record.</summary>
        void AfterAction()
        {
            if (_meta == null || _state == null || _engine == null) return;
            DailyMissions.Sync(_meta.Meta, _meta.Turns, _state, _engine.Content);
            RefreshTurnsUi();
            RefreshMissionsUi();
            SaveService.SaveMeta(_meta);
        }

        void RefreshTurnsUi()
        {
            if (Ui == null || _meta == null || _engine == null) return;
            Ui.SetTurns(_meta.Turns.ManualLeft, _engine.Content.Balance.DailyManualMonths,
                TurnBudget.SecondsToRefill(_meta.Turns, DeviceClock.NowUnix(), DeviceClock.UtcOffsetMinutes()));
        }

        void RefreshMissionsUi()
        {
            if (Ui == null || Ui.RunMode || _meta == null || _engine == null || _state == null) return;
            var views = new List<MissionView>();
            foreach (var m in DailyMissions.For(_meta.Turns.Day, _state.Seed, _engine.Content))
                views.Add(new MissionView(m.TitleKey, DailyMissions.Progress(_meta.Meta, _meta.Turns, m.Id), m.Target,
                    DailyMissions.IsDone(_meta.Meta, _meta.Turns, m.Id)));
            Ui.SetMissions(views, _meta.Turns.Streak, _meta.Meta.CeoPoints);
        }

        /// <summary>Once a second: the countdown, and the midnight refill while the game stays open.</summary>
        void Update()
        {
            if (Current != Phase.Playing || _meta == null || _engine == null || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            int day = _meta.Turns.Day;
            TurnBudget.Refresh(_meta.Turns, DeviceClock.NowUnix(), DeviceClock.UtcOffsetMinutes(), _engine.Content.Balance);
            RefreshTurnsUi();
            if (_meta.Turns.Day != day) RefreshMissionsUi();
            // Keep the last-seen time fresh for offline autopilot (the web player may close without warning).
            if (_meta.Turns.Day != day || ++_ticks % 30 == 0) SaveService.SaveMeta(_meta);
        }

        void OnApplicationPause(bool paused) { if (paused) { SaveMetaNow(); _ = _sync?.FlushPending(); } }

        void OnApplicationQuit() { SaveMetaNow(); _ = _sync?.FlushPending(); }

        void SaveMetaNow()
        {
            if (_meta == null || _engine == null || Current != Phase.Playing) return;
            TurnBudget.Refresh(_meta.Turns, DeviceClock.NowUnix(), DeviceClock.UtcOffsetMinutes(), _engine.Content.Balance);
            SaveService.SaveMeta(_meta);
        }

        void Fail(string why)
        {
            Current = Phase.Failed;
            Debug.LogError("Boot failed: " + why);
        }
    }
}
