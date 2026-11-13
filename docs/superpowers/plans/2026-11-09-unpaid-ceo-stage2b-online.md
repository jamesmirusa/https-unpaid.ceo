# Stage 2B — Accounts, cloud save, rankings and async multiplayer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guest-first accounts, cloud save, server-validated leagues and rankings, a weekly challenge, friends with async duels, weekly industry "market wars", Remote Config flags and analytics — and the game stays fully playable offline.

**Architecture:** Run validation lives in the pure C# core (`Core/Online`), so the client and a Cloud Code C# module that compiles the same core sources replay a submitted decision log and agree on the result; only the server writes leaderboards. The Unity side talks to Unity Gaming Services through `IOnlineServices` with three implementations: UGS, offline (project not linked, no network, flag off) and a test fake. Challenge and duel runs are separate `GameState`s started from a `RunStart` and played on the same screens.

**Tech Stack:** Unity 6000.6 (C#, UI Toolkit, Addressables), Unity Gaming Services packages (Core, Authentication, Cloud Save, Leaderboards, Cloud Code, Remote Config, Analytics, Friends), Cloud Code C# module on .NET 8 (xUnit tests), UGS config-as-code deployed with the `ugs` CLI.

**Spec:** `docs/superpowers/specs/2026-11-08-unpaid-ceo-2-design.md` (§2 accounts, §3.7 competition, §7.1 Services, §7.2 UGS, §7.7 analytics, §11 stage 2, §15 out of scope). Stage-2A rulings: `docs/superpowers/rulings/2026-11-08-stage2a-rulings.md`.

**Sibling plans:** 2C 17 languages · 2D payments (Stripe, Telegram Stars) · 2E launch (landing, `/play` on unpaid.ceo, Telegram Mini App and Telegram/Google sign-in, Season 01 Veteran badge — it needs the game on the old site's origin).

## Global Constraints

- Accounts: "Гость сразу + вход по желанию (облачный сейв, рейтинг)" — the game starts as an anonymous guest; signing in is optional and never blocks play.
- "Cloud Code — серверная проверка: рейтинговый результат пересчитывается из журнала решений тем же ядром." Clients never write leaderboards (an Access Control policy denies it).
- "Unity Player Accounts в браузере не поддерживаются" — web sign-in in this plan is UGS username/password; Google/Apple/Telegram come later (2E, stage 3).
- "Мультиплеер в реальном времени" is out of scope: duels and market wars are asynchronous.
- "Ключи хранятся только в секретах окружения" — no secret in the repo, in chat or in the build; `ugs login` is run by the owner in Terminal.
- First load (Bronx) ≤ 20 MB compressed (`tools/check-build-size.sh`, Build/* ≤ 20 MiB).
- Analytics events (§7.7): `tutorial_step`, `month_completed`, `chapter_unlocked`, `industry_chosen`, `business_opened`, `bankrupt`, `lesson_viewed`, `quiz_passed`, `signup`, `purchase`, `ad_rewarded`, `graphics_mode`, `language` — recorded only after consent.
- Leagues (§3.7): Rookie → Hustler → Boss → Tycoon → Legend, by capital (net worth).
- Weekly challenge (§3.7): the same scenario and seed for everyone, ranked by result ("вырасти фуд-трак с $10k за 12 месяцев").
- UI text: EN and RU in `Localization/Source/UI.tsv` (the other 15 languages arrive with 2C).
- Pacing (2A): at most `DailyManualMonths` (6) + `AutopilotMaxMonths` (2) career months per local day.

## Review Focus

1. No network, an unlinked project (empty `Application.cloudProjectId`) or UGS down: the game plays exactly as in 2A, online sections show a calm notice, nothing throws, submissions wait in a queue. Test: Task 6 `Offline_services_never_throw_and_report_unavailable`, Task 7 `Queued_submission_is_retried_when_online`.
2. A fresh install that signs in to an account with a longer game must download it, never upload the empty newcomer over it; a newer but shorter save never wins. Test: Task 7 `Fresh_install_does_not_overwrite_cloud_progress`.
3. A tampered submission (edited cash, a log whose action fails, a wrong state hash, another content version, more months than real time allows) is rejected and no leaderboard changes. Test: Task 4 `Tampered_runs_are_rejected_and_boards_untouched`.
4. A guest who creates an account keeps progress and rank (same player id); signing out returns to a new guest without deleting the device save. Test: Task 8 `Signing_up_keeps_the_guest_progress`.
5. A weekly challenge started before Monday 00:00 UTC and finished after scores for the week it started (1-hour grace), later it is refused politely. Test: Task 4 `Challenge_finished_after_the_reset_scores_within_grace_only`.

---

### Task 1: Strict replay and scenario starts (Core)

**Files:**
- Create: `game/Assets/UnpaidCeo/Core/Online/RunStart.cs`, `Core/Online/ScenarioDef.cs`
- Modify: `Core/Game/GameEngine.cs` (Replay, TakeLoan, StartScenario)
- Test: `game/Assets/UnpaidCeo/Core.Tests/Online/StrictReplayTests.cs`

**Interfaces:**
- Produces: `sealed class RunStart { string Kind; ulong Seed; HeroAppearance Hero; string ContextId; string IndustryId; }` with `const string Career = "career", Challenge = "challenge", Duel = "duel"`.
- Produces: `sealed class ScenarioDef { ulong Seed; string IndustryId; Money StartCash; int Months; }`.
- Produces: `GameState GameEngine.StartScenario(ScenarioDef d, HeroAppearance hero)` — NewGame(d.Seed, hero), then chapter 2 in Brooklyn, `Cash = d.StartCash`, the chosen industry with 0 units, facts `onboarding_done` and `chapter2_start`; nothing logged.
- Produces: `static GameState GameEngine.Start(ContentDb c, RunStart r, Func<RunStart, ScenarioDef> scenarioFor)` (career → NewGame).
- Produces: `static ReplayResult GameEngine.ReplayStrict(ContentDb c, RunStart start, Func<RunStart, ScenarioDef> scenarioFor, IReadOnlyList<DecisionLogEntry> log)`; `sealed class ReplayResult { GameState State; MonthReport LastReport; int FailedAt = -1; string Error; bool Ok => FailedAt < 0; }` (`LastReport` = the report of the last replayed `endMonth`; GameState keeps no report history).
- Changes: `TakeLoan` returns false before chapter 2 (2A deferred minor; the UI already hides it).

- [ ] **Step 1: Write the failing tests** — `Strict_replay_matches_the_live_game` (play 30 months with the 2A bots' plan through `GameEngine`, `ReplayStrict` gives the same `StateHash.Compute(state)`), `Strict_replay_stops_at_the_first_refused_action` (append a `takeLoan` larger than `MaxLoan`, `FailedAt` = its index, `Error` = `"refused:takeLoan"`), `Strict_replay_reports_a_bad_payload` (corrupt JSON in an `endMonth` → `Error` starts with `"bad_payload"`), `Loans_need_chapter_2` (chapter 1 `TakeLoan` → false, nothing logged), `Scenario_start_is_chapter_2_with_the_chosen_industry` (cash $10,000 → 1_000_000 cents, chapter 2, district brooklyn, one business, `BeginMonth` offers no chapter-1 scene).
- [ ] **Step 2: Run** `tools/unity.sh test-editmode StrictReplayTests` — Expected: FAIL (types missing).
- [ ] **Step 3: Implement** the types and methods above; `ReplayStrict` reuses `Replay`'s switch but treats a `false` return or an exception as failure at that index; `Replay` stays as is for local loads.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): strict replay and scenario starts`.

### Task 2: Run verifier, content version, submission types (Core)

**Files:**
- Create: `Core/Online/RunSubmission.cs`, `Core/Online/RunVerifier.cs`, `Core/Online/ContentVersion.cs`
- Test: `Core.Tests/Online/RunVerifierTests.cs`

**Interfaces:**
- Consumes: Task 1 `ReplayStrict`, `RunStart`, `ScenarioDef`.
- Produces: `sealed class RunSubmission { RunStart Start; List<DecisionLogEntry> Log; string StateHash; string ContentVersion; }` and `sealed class SubmitResult { bool Ok; string Error; long NetWorthCents; string League; int Rank = -1; }`.
- Produces: `static string ContentVersion.Of(IEnumerable<string> contentJson)` — SHA-256 of the files' canonical JSON in `GameBootstrap.ContentFiles` order (client) / the same order from embedded resources (server).
- Produces: `static VerifyResult RunVerifier.Verify(ContentDb c, string serverContentVersion, RunSubmission s, Func<RunStart, ScenarioDef> scenarioFor)`; `sealed class VerifyResult { bool Ok; string Error; GameState State; long NetWorthCents; int Months; string IndustryId; long LastMonthRevenueCents; }`. Errors: `"content_version"`, `"replay:<FailedAt>:<Error>"`, `"hash_mismatch"`.
- Produces: `static RunSubmission RunSubmission.From(RunStart start, GameState s, string contentVersion)`.

- [ ] **Step 1: Write the failing tests** — `Honest_run_verifies_with_net_worth_and_months`, `Edited_cash_fails_the_hash`, `Other_content_version_is_refused`, `Revenue_of_the_last_month_is_reported` (equals the player's last `MonthReport` revenue for the chosen business), `Content_version_is_stable_and_order_sensitive`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode RunVerifierTests` — Expected: FAIL.
- [ ] **Step 3: Implement**; `LastMonthRevenueCents` is the chosen business's revenue in `ReplayResult.LastReport.BusinessResults` (0 when the run has no business or no month yet).
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): run verifier and content version`.

### Task 3: Leagues, pacing, weekly challenge, duels, achievements (Core)

**Files:**
- Create: `Core/Online/Leagues.cs`, `Core/Online/Pacing.cs`, `Core/Online/WeeklyChallenge.cs`, `Core/Online/Duels.cs`, `Core/Online/Achievements.cs`
- Modify: `Core/Game/DailyMissions.cs` (`MetaProgress`: `List<string> Badges`, `int CleanCashMonths`)
- Test: `Core.Tests/Online/CompetitionRulesTests.cs`

**Interfaces:**
- Produces: `static string Leagues.For(long netWorthCents)` with thresholds Rookie < $10,000 ≤ Hustler < $100,000 ≤ Boss < $1,000,000 ≤ Tycoon < $10,000,000 ≤ Legend; `static readonly string[] Leagues.All`.
- Produces: `static int Pacing.MaxCareerMonths(long firstSeenUnix, long nowUnix, Balance b) => 2 + (b.DailyManualMonths + b.AutopilotMaxMonths) * (days + 1)` where `days = max(0, (now − firstSeen) / 86400)`.
- Produces: `static int WeeklyChallenge.WeekIndex(long unixSeconds) => (int)Math.Floor((unixSeconds / 86400.0 + 3) / 7)` (weeks start Monday 00:00 UTC); `static ScenarioDef WeeklyChallenge.For(int week, Balance b)` — industry `b.Chapter2Industries[week mod count]`, `Seed = SplitMix64((ulong)week ^ 0x5EED_2B)`, `StartCash` $10,000, `Months` 12; `const int AttemptsPerWeek = 3`; `const int GraceSeconds = 3600`.
- Produces: `static ScenarioDef Duels.For(string duelId, string industryId)` — `Seed = Fnv1a64(duelId)`, $10,000, 6 months; `const int ExpiryDays = 7`.
- Produces: `static ScenarioDef Scenarios.For(RunStart r, Balance b)` dispatching challenge/duel (used as `scenarioFor` everywhere).
- Produces: `static IReadOnlyList<string> Achievements.Update(MetaProgress m, GameState s, MonthReport r)` returning newly earned ids from `"break_even_3"` (break-even ≤ 3 months after `industry_chosen`), `"clean_cash_12"` (12 month ends in a row with cash ≥ 0 after the first unit opened — the spec's "месяц без кассового разрыва" holds in most single months, so the badge asks for a year), `"duel_winner"`, `"challenge_top10"` (the last two granted by Tasks 10–11 through `Achievements.Grant(MetaProgress m, string id)`).

- [ ] **Step 1: Write the failing tests** — `League_thresholds` (cases 0 → Rookie, 999_999 → Rookie, 1_000_000 → Hustler, 10_000_000 → Boss, 100_000_000 → Tycoon, 1_000_000_000 → Legend), `Pacing_allows_eight_months_a_day_plus_onboarding` (same day → 10, after 2 days → 26, a clock before first-seen → 10), `Week_index_turns_on_monday_utc` (Sunday 23:59:59 UTC vs Monday 00:00 UTC differ by 1), `Weekly_challenge_is_the_same_for_everyone_and_rotates_industries`, `Duel_seed_comes_from_the_duel_id`, `Break_even_within_three_months_earns_the_badge_once`, `Twelve_clean_months_earn_the_badge_and_a_negative_month_resets`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode CompetitionRulesTests` — Expected: FAIL.
- [ ] **Step 3: Implement**; `SplitMix64` and `Fnv1a64` go in `Core/Primitives/Hashes.cs` if no equivalent exists in `Core/Primitives`.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): leagues, pacing, weekly challenge, duels, achievements`.

### Task 4: Cloud Code module (server, .NET 8)

**Files:**
- Create: `server/cloudcode/UnpaidCeo.CloudCode/UnpaidCeo.CloudCode.csproj` (net8.0; `<Compile Include="../../../game/Assets/UnpaidCeo/Core/**/*.cs" />`; `<EmbeddedResource Include="../../../game/Assets/UnpaidCeo/Content/*.json" LogicalName="content/%(Filename)%(Extension)" />`; NuGet `Com.Unity.Services.CloudCode.Core`, `Com.Unity.Services.CloudCode.Apis`, `Newtonsoft.Json` 13)
- Create: `server/cloudcode/UnpaidCeo.CloudCode/GameServer.cs` (all rules, no SDK types), `Ports.cs` (`IPlayerStore`, `IDuelStore`, `IBoardWriter`, `IClock`), `UgsAdapters.cs` (Cloud Save protected player data, Cloud Save custom items, Leaderboards), `Endpoints.cs` (`[CloudCodeFunction]` methods), `ModuleConfig.cs` (`ICloudCodeSetup`)
- Create: `server/cloudcode/UnpaidCeo.CloudCode.Tests/` (xUnit, in-memory ports), `tools/server/test.sh` (`dotnet test server/cloudcode`)
- Prerequisite (owner-approved install): .NET 8 SDK user-level via Microsoft's `dotnet-install.sh` into `~/.dotnet`.

**Interfaces:**
- Consumes: Tasks 1–3 (`RunVerifier`, `Scenarios.For`, `Leagues`, `Pacing`, `WeeklyChallenge`, `Duels`).
- Produces endpoints (names are the Cloud Code function names): `Hello() → HelloResult { long ServerUnix; int Week; ScenarioDef Challenge; }` (records protected `first_seen` once); `SubmitCareer(RunSubmission) → SubmitResult` (writes `capital` = net worth dollars and `market_<industryId>` = last-month revenue dollars); `SubmitChallenge(RunSubmission) → SubmitResult` (`weekly_challenge`, attempts in protected `challenge_attempts_<week>` ≤ 3); `CreateDuel(string opponentId, string industryId) → DuelView`; `SubmitDuel(string duelId, RunSubmission) → DuelView`; `ListDuels() → DuelView[]`.
- Produces (Core/Online, shared): `sealed class DuelView { string Id; string Opponent; string OpponentName; string IndustryId; long CreatedUnix; long? MyNetWorthCents; long? TheirNetWorthCents; string State; }` with `State` ∈ `"waiting_me","waiting_them","won","lost","draw","expired"`; `sealed class HelloResult`.
- Rejections reuse `VerifyResult.Error` plus `"too_many_months"`, `"wrong_kind"`, `"week_ended"`, `"attempts_used"`, `"not_in_duel"`, `"duel_expired"`.

- [ ] **Step 1: Install .NET 8** (`~/.dotnet/dotnet --list-sdks` shows 8.0.x) and scaffold the projects; `tools/server/test.sh` — Expected: builds, 0 tests.
- [ ] **Step 2: Write the failing tests** — `Honest_career_writes_capital_and_market_boards`, `Tampered_runs_are_rejected_and_boards_untouched` (edited cash, failing action, wrong hash, other content version, months > `Pacing.MaxCareerMonths` — each leaves the fake boards empty), `Challenge_counts_three_attempts_a_week_and_keeps_the_best`, `Challenge_finished_after_the_reset_scores_within_grace_only` (submitted 30 min after Monday 00:00 for last week → last week's board; 2 h after → `"week_ended"`), `Duel_resolves_when_both_finished` (win/lose/draw), `Duel_expires_after_seven_days`, `Only_the_two_players_can_submit_to_a_duel`, `Server_and_client_content_versions_match` (the embedded content hashes equal `ContentVersion.Of` of the Unity files).
- [ ] **Step 3: Run** `tools/server/test.sh` — Expected: FAIL.
- [ ] **Step 4: Implement** `GameServer` against the ports, then thin `UgsAdapters`/`Endpoints` (player id from `IExecutionContext.PlayerId`; protected player data for `first_seen`/attempts/duel ids; custom items collection `duels` for duel records; scores via the Leaderboards API with the service token).
- [ ] **Step 5: Run** `tools/server/test.sh` — Expected: all pass; `dotnet publish -c Release` succeeds.
- [ ] **Step 6: Commit** `feat(server): Cloud Code module validating runs with the game core`.

### Task 5: UGS configuration as code

**Files:**
- Create: `server/ugs/capital.lb` (sort desc, keepBest, tiers Rookie 0 · Hustler 10000 · Boss 100000 · Tycoon 1000000 · Legend 10000000), `weekly_challenge.lb` (keepBest, reset `0 0 * * 1` UTC, archive on), six `market_<industryId>.lb` (keepBest, weekly reset), `server/ugs/flags.rc` (`online_enabled`, `challenge_enabled`, `duels_enabled`, `market_wars_enabled` all `true`), `server/ugs/no-client-scores.ac` (Deny `Write` on `urn:ugs:leaderboards:/v1/projects/*/leaderboards/*/scores/players/*` for players), `server/ugs/UnpaidCeo.ccmr` (points at the Task 4 project)
- Create: `tools/server/check_ugs_config.py` (parses every file; the industry list equals `balance.json` `chapter2Industries`; tiers equal `Leagues`), `tools/server/deploy.sh` (`ugs deploy server/ugs -e production`; refuses to run without `ugs status` logged in)

**Interfaces:**
- Consumes: Task 3 thresholds, Task 4 project, `balance.json`.
- Produces: leaderboard ids `capital`, `weekly_challenge`, `market_<industryId>`; flag keys above.
- Decision: Remote Config carries flags only. Balance changes ship with a build (spec §7.2 lists balance in Remote Config), because a run is valid only against the content version the server embeds; overriding balance remotely would make every honest submission fail `content_version`.

- [ ] **Step 1: Write the failing check** `tools/server/check_ugs_config.py` — Expected: FAIL (files missing).
- [ ] **Step 2: Add the files**; run `python3 tools/server/check_ugs_config.py` — Expected: `ugs config ok`.
- [ ] **Step 3: Commit** `feat(server): UGS leaderboards, flags and access policy as code`.

### Task 6: Online services layer (Unity)

**Files:**
- Modify: `game/Packages/manifest.json` (UGS packages: `com.unity.services.authentication`, `.cloudsave`, `.leaderboards`, `.cloudcode`, `com.unity.remote-config`, `com.unity.services.analytics`, `com.unity.services.friends` — newest versions that resolve for Unity 6000.6), `Game/UnpaidCeo.Game.asmdef` references
- Create: `Game/Services/Online/IOnlineServices.cs` (interfaces + `OnlineFlags`), `UgsOnlineServices.cs`, `OfflineServices.cs`, `OnlineBootstrap.cs`
- Create: `Game.Tests/Fakes/FakeOnlineServices.cs`; Test: `Game.Tests/OnlineServicesTests.cs`

**Interfaces:**
- Produces: `interface IOnlineServices { bool Available; IOnlineAuth Auth; ICloudSaves Saves; IRunBackend Runs; ILeaderboards Boards; IFriends Friends; OnlineFlags Flags; IGameAnalytics Analytics; }`.
- `IOnlineAuth { bool SignedIn; bool IsGuest; string PlayerId; string PlayerName; Task<AuthResult> SignInAnonymously(); Task<AuthResult> SignUp(string user, string pass); Task<AuthResult> SignIn(string user, string pass); Task<AuthResult> SetName(string name); Task SignOut(); }`, `enum AuthResult { Ok, InvalidCredentials, NameTaken, WeakPassword, Offline, Error }`.
- `ICloudSaves { Task<CloudSaveEnvelope> Load(); Task<bool> Save(CloudSaveEnvelope e); }`, `sealed class CloudSaveEnvelope { string SaveJson; string MetaJson; int Months; long UpdatedUnix; }` (Cloud Save key `save_v2`).
- `IRunBackend { Task<HelloResult> Hello(); Task<SubmitResult> SubmitCareer(RunSubmission s); Task<SubmitResult> SubmitChallenge(RunSubmission s); Task<DuelView> CreateDuel(string opponentId, string industryId); Task<DuelView> SubmitDuel(string duelId, RunSubmission s); Task<DuelView[]> ListDuels(); }`.
- `ILeaderboards { Task<RankEntry[]> Top(string board, int limit); Task<RankEntry> Mine(string board); }`, `sealed class RankEntry { int Rank; string PlayerId; string Name; double Score; string Tier; }`.
- `IFriends { Task<FriendView[]> List(); Task<FriendView[]> Requests(); Task<bool> AddByName(string name); Task<bool> Accept(string playerId); Task<bool> Remove(string playerId); }`, `sealed class FriendView { string PlayerId; string Name; }`.
- `IGameAnalytics { bool Consented; void SetConsent(bool on); void Record(string name, IDictionary<string, object> args = null); }`.
- `sealed class OnlineFlags { bool Online = true, Challenge = true, Duels = true, MarketWars = true; }`.
- `static Task<IOnlineServices> OnlineBootstrap.Start(float timeoutSeconds = 8)` → `OfflineServices` when `Application.cloudProjectId` is empty, initialization fails or times out, or Remote Config `online_enabled` is false; otherwise signs in anonymously (reusing the cached session) and fetches flags.

- [ ] **Step 1: Install the packages** (batch mode resolves; `tools/unity.sh test-editmode` still green) and write the failing tests — `Offline_services_never_throw_and_report_unavailable` (every method of `OfflineServices` completes with `Offline`/empty results), `Unlinked_project_starts_offline` (empty cloud project id → `Available == false` within the timeout), `Fake_services_round_trip` (the fake used by Tasks 7–12: sign-up, save/load, boards, friends, duels).
- [ ] **Step 2: Run** `tools/unity.sh test-playmode OnlineServicesTests` — Expected: FAIL.
- [ ] **Step 3: Implement**; `GameBootstrap.Boot` starts `OnlineBootstrap.Start` in parallel with content loading and never waits for it before the city is playable; `UiRoot.Online` exposes the services to screens.
- [ ] **Step 4: Run** `tools/unity.sh test-playmode` and `tools/unity.sh build-web` + `tools/check-build-size.sh` — Expected: all pass; Build/* ≤ 20 MiB.
- [ ] **Step 5: Commit** `feat(online): services layer with offline fallback`.

### Task 7: Cloud save sync and submission queue

**Files:**
- Create: `Game/Services/Online/CloudSync.cs`, `Game/Services/Online/SubmissionQueue.cs`
- Modify: `Game/Bootstrap/GameBootstrap.cs` (pull at boot after sign-in, push after each month and on pause, career submission after each month)
- Test: `Game.Tests/CloudSyncTests.cs`

**Interfaces:**
- Consumes: Task 6 `ICloudSaves`, `IRunBackend`; Task 2 `RunSubmission.From`.
- Produces: `static SyncChoice CloudSync.Choose(CloudSaveEnvelope local, CloudSaveEnvelope cloud)` → `UseLocal`/`UseCloud`/`Equal` by months played, then `UpdatedUnix`; `Task CloudSync.Pull(...)`, `Task CloudSync.Push(...)` (debounced to one write per 20 s).
- Produces: `sealed class SubmissionQueue` persisted under PlayerPrefs `unpaidceo.queue.v1` (one pending career submission, the latest replaces the older; challenge/duel entries kept), `Task Flush(IRunBackend b)`.

- [ ] **Step 1: Write the failing tests** — `Fresh_install_does_not_overwrite_cloud_progress` (local month 1 vs cloud month 40 → `UseCloud`, and Pull replaces the local save), `Newer_but_shorter_save_loses`, `Push_is_debounced`, `Queued_submission_is_retried_when_online` (offline → queued; services become available → flushed once), `Only_the_latest_career_submission_is_kept`.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode CloudSyncTests` — Expected: FAIL.
- [ ] **Step 3: Implement** and wire into `GameBootstrap`.
- [ ] **Step 4: Run** `tools/unity.sh test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(online): cloud save sync and submission queue`.

### Task 8: Account panel

**Files:**
- Create: `Game/Ui/AccountPanel.cs`; Modify: `Game/Ui/SettingsScreen.cs`, `Resources/UI/Settings.uxml`, `Localization/Source/UI.tsv` (keys `account.*`)
- Test: `Game.Tests/AccountPanelTests.cs`

**Interfaces:**
- Consumes: Task 6 `IOnlineAuth`; Task 7 `CloudSync`.
- Produces: Settings → Account: guest status and player name; "Create account" (username 3–20 characters, password 8–30 with upper, lower, digit and symbol — UGS rules, checked before sending), "Sign in", "Change name", "Sign out"; each `AuthResult` maps to one localized message; offline shows `account.offline`.

- [ ] **Step 1: Write the failing tests** — `Signing_up_keeps_the_guest_progress` (fake: guest id G plays 3 months, signs up → same id G, cloud save holds month 3), `Weak_password_is_explained_before_sending`, `Name_taken_shows_its_message`, `Signing_out_returns_to_a_new_guest_and_keeps_the_device_save`, `Offline_account_panel_says_so`.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode AccountPanelTests` — Expected: FAIL.
- [ ] **Step 3: Implement**; run `UnpaidCeo.Editor.StringTableImporter.ImportAll` after editing UI.tsv.
- [ ] **Step 4: Run** `tools/unity.sh test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(ui): account panel (guest, sign up, sign in, name)`.

### Task 9: Ranking screen — leagues, global board, market wars, badges

**Files:**
- Create: `Game/Ui/RankingScreen.cs`; Modify: `Resources/UI/Ranking.uxml` (replace the "soon" placeholder), `Game/Ui/UiRoot.cs`, `Localization/Source/UI.tsv` (`ranking.*`, `league.*`, `badge.*`)
- Test: `Game.Tests/RankingScreenTests.cs`

**Interfaces:**
- Consumes: Task 3 `Leagues`, `Achievements`; Task 6 `ILeaderboards`, `OnlineFlags`.
- Produces: sections League (my league from local net worth, progress to the next threshold), Top 20 on `capital` with my rank, Market wars (top 10 on `market_<my industry>` and my share = my score ÷ sum of the top 10; hidden in chapter 1 or when the flag is off), Badges (`MetaProgress.Badges`), and one offline notice replacing the online sections.

- [ ] **Step 1: Write the failing tests** — `League_card_shows_my_league_and_progress`, `Top_board_highlights_me`, `Market_wars_show_my_industry_only_from_chapter_2`, `Offline_ranking_shows_one_notice_and_the_league`, `Badges_list_what_I_earned`.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode RankingScreenTests` — Expected: FAIL.
- [ ] **Step 3: Implement**; `GameBootstrap` calls `Achievements.Update` after each month and saves the meta.
- [ ] **Step 4: Run** `tools/unity.sh test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(ui): ranking with leagues, market wars and badges`.

### Task 10: Weekly challenge runs

**Files:**
- Create: `Game/Bootstrap/ScenarioRun.cs`, `Game/Ui/RunBanner.cs`; Modify: `Game/Bootstrap/GameBootstrap.cs`, `Game/Ui/RankingScreen.cs` (challenge card), `Game/Services/SaveService.cs` (slot `unpaidceo.run.v1`), `UI.tsv` (`challenge.*`)
- Test: `Game.Tests/ScenarioRunTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 (`StartScenario`, `Scenarios.For`, `WeeklyChallenge`), Task 6 `IRunBackend`, Task 7 `SubmissionQueue`.
- Produces: `sealed class ScenarioRun { RunStart Start; GameState State; int Months; bool Finished; }`; `GameBootstrap.StartRun(RunStart start)` swaps the screens to the run's state (no daily turns used, autopilot off, career state untouched and saved), `RunBanner` shows "Weekly challenge · month n/12 · Exit"; after month 12 the result screen submits via `SubmitChallenge` (queued when offline) and `GameBootstrap.EndRun()` restores the career; leaving early keeps the run for later in the run slot.
- Produces: the Ranking challenge card — this week's industry and start, attempts left (3 − used), my best, top 10 of `weekly_challenge`; a finished top-10% result grants `challenge_top10`.

- [ ] **Step 1: Write the failing tests** — `Challenge_run_uses_no_daily_turns_and_leaves_the_career_alone`, `Twelfth_month_submits_once_and_returns_to_the_career`, `Leaving_and_resuming_a_run_keeps_its_months`, `Offline_challenge_result_is_queued`, `Challenge_card_shows_attempts_left`.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode ScenarioRunTests` — Expected: FAIL.
- [ ] **Step 3: Implement**.
- [ ] **Step 4: Run** `tools/unity.sh test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(online): weekly challenge runs`.

### Task 11: Friends and async duels

**Files:**
- Create: `Game/Ui/FriendsPanel.cs`; Modify: `Game/Ui/RankingScreen.cs` (Friends & duels section), `Game/Ui/UiRoot.cs` (City screen card "a duel is waiting"), `UI.tsv` (`friends.*`, `duel.*`)
- Test: `Game.Tests/FriendsAndDuelsTests.cs`

**Interfaces:**
- Consumes: Task 6 `IFriends`, `IRunBackend`; Task 10 `StartRun`; Task 3 `Duels`, `Achievements.Grant`.
- Produces: add a friend by player name (the name shown in the Account panel, `Name#1234`), accept/decline requests, challenge a friend (pick one of the six industries) → `CreateDuel`; a duel is a 6-month `ScenarioRun` (`RunStart.Kind = Duel`, `ContextId` = duel id) submitted with `SubmitDuel`; the duel list shows each `DuelView.State`; a first win grants `duel_winner`; signed-out guests see "Create an account to add friends".

- [ ] **Step 1: Write the failing tests** — `Adding_a_friend_by_name_sends_a_request`, `Accepting_a_request_lists_the_friend`, `Duel_plays_six_months_and_submits`, `Duel_list_shows_won_lost_waiting_expired`, `Guest_is_asked_to_create_an_account_first`, `First_duel_win_grants_the_badge`.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode FriendsAndDuelsTests` — Expected: FAIL.
- [ ] **Step 3: Implement**.
- [ ] **Step 4: Run** `tools/unity.sh test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(online): friends and async duels`.

### Task 12: Analytics with consent

**Files:**
- Create: `Game/Services/Online/AnalyticsEvents.cs`, `Game/Ui/ConsentDialog.cs`; Modify: call sites in `GameBootstrap`, `UiRoot`, `IndustryScreen`, `AcademyScreen`, `SettingsScreen`, `OnboardingFlow`; `UI.tsv` (`consent.*`)
- Test: `Game.Tests/AnalyticsTests.cs`

**Interfaces:**
- Consumes: Task 6 `IGameAnalytics`.
- Produces: `static class AnalyticsEvents` with the 13 §7.7 names as constants and one helper per event that the call sites use (`TutorialStep(int step)`, `MonthCompleted(int month, int chapter)`, `ChapterUnlocked(int chapter)`, `IndustryChosen(string id)`, `BusinessOpened(string id, int units)`, `Bankrupt(int month)`, `LessonViewed(string id)`, `QuizPassed(string id)`, `Signup(string method)`, `GraphicsMode(string mode)`, `Language(string code)`; `purchase` and `ad_rewarded` are declared for 2D); a one-time consent dialog after onboarding; a Settings toggle; nothing is recorded or sent before consent.

- [ ] **Step 1: Write the failing tests** — `Nothing_is_recorded_before_consent`, `Each_event_fires_once_with_its_arguments` (play onboarding and one month with the fake: `tutorial_step` × steps, `month_completed` once), `Consent_can_be_withdrawn_in_settings`, `All_spec_events_are_declared` (the 13 names).
- [ ] **Step 2: Run** `tools/unity.sh test-playmode AnalyticsTests` — Expected: FAIL.
- [ ] **Step 3: Implement**.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` and `tools/unity.sh test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(online): analytics events with consent`.

### Task 13: Link, deploy and verify live (owner-gated)

**Files:**
- Modify: `game/ProjectSettings/ProjectSettings.asset` (cloud project id and organization, set by linking), `docs/superpowers/rulings/` (2B rulings file at the end)

**Interfaces:**
- Consumes: everything above.
- Owner steps (cannot be done by the agent): create the UGS project "Unpaid CEO" in the Unity Dashboard and link it (or send the Project ID and Organization ID — neither is secret); create a service account with Cloud Code, Leaderboards, Cloud Save, Remote Config and Access Control roles and run `ugs login` in Terminal on the Mac (the key never enters the chat).

- [ ] **Step 1: Link** and confirm `Application.cloudProjectId` is set in a build; install the UGS CLI (`npm install -g ugs`, owner-approved).
- [ ] **Step 2: Deploy** `tools/server/deploy.sh` — Expected: module, 8 leaderboards, flags and policy deployed to `production`.
- [ ] **Step 3: Verify live** in the web build: guest sign-in, create account, cloud save round-trip on a second browser, a career submission appears on `capital`, a client-side leaderboard write is denied, a challenge run and a duel between two test accounts resolve; record evidence in the ledger.
- [ ] **Step 4: Build, size check, deploy the gh-pages preview**, final whole-branch review, rulings file, commit `release: stage 2B online`.
