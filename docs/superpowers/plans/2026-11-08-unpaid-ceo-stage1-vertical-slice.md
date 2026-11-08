# Unpaid CEO 2.0 — Этап 1 «Вертикальный срез» — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Глава 1 (Бронкс) полностью играбельна в браузере: экономика, жизнь и последствия, сцены на Yarn Spinner 3, 3D-город, редактор героя, три режима графики, EN + RU, локальный сейв, превью-ссылка для владельца.

**Architecture:** Вся игровая логика — чистое детерминированное C#-ядро (`UnpaidCeo.Core`, без ссылок на UnityEngine) с EditMode-тестами; Unity-слой (`UnpaidCeo.Game`) только показывает состояние и передаёт решения игрока. Контент (отрасли, баланс, сцены, уроки) — JSON + Yarn-файлы, проверяемые тестами. Сборка и тесты запускаются на Mac владельца из терминала в batchmode.

**Tech Stack:** Unity 6 LTS (6000.3.x) · URP · UI Toolkit · Unity Localization · Addressables · glTFast (`com.unity.cloud.gltfast`) · Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`) · Yarn Spinner for Unity 3.x · Unity Test Framework (NUnit) · Higgsfield (арт) · ElevenLabs (голос и музыка).

**Spec:** `docs/superpowers/specs/2026-11-08-unpaid-ceo-2-design.md` (разделы 3, 3a, 5, 7, 8, 9, 11 — этап 1).

## Global Constraints

- Unity 6 LTS (6000.3.x), URP; проект лежит в `game/`; сборка Web: Brotli + **Decompression Fallback** (превью на GitHub Pages, где нет заголовка `Content-Encoding`).
- `UnpaidCeo.Core` — `noEngineReferences: true`; деньги — `Money` в целых центах (`long`); округление дробных центов `MidpointRounding.AwayFromZero`; штуки — вниз (floor).
- Случайность в ядре — только `Pcg32` с сохраняемым состоянием; `System.Random` и `UnityEngine.Random` в ядре запрещены.
- Налоговый резерв: 25% положительной операционной прибыли; при убытке 0.
- Личные расходы начинаются после первых $100 суммарного дохода.
- Автопилот: 1 игровой месяц за 4 реальных часа, максимум 2 месяца, без сцен.
- Цель главы 1: cash ≥ $1 000 **и** резерв ≥ одного месяца личных расходов.
- «Время CEO»: Работа / Семья / Отдых / Учёба в сумме 100; базовые очки действий — 3.
- Шкалы жизни 0–100; отношение NPC −100…+100; 0–2 сцены в месяц.
- Языки этапа 1: `en`, `ru`; весь видимый текст — из таблиц локализации, без строк в коде. Форматирование чисел — свои правила (не `CultureInfo`): en `,` и `.`; ru неразрывный пробел U+00A0 и `,`; минус U+2212.
- Режимы графики (FPS / рендер / текстуры / draw calls): Эко 30 / 0,75× / 512 / ≤100 · Оптимальный 60 / 1× / 1024 / ≤250 · Макс 60 / 1× (до 1,25× на ПК) / 2048 / ≤600; бенчмарк при первом запуске — 3 секунды.
- Web: первая загрузка ≤ 20 МБ сжатого (20 971 520 байт); память ≤ 400 МБ в iOS Safari (Эко/Оптимальный); 60 fps в «Оптимальном» на телефонах владельца.
- 3D: здание LOD0 2 000–8 000 треугольников + LOD1/LOD2; персонаж 15 000–20 000.
- ElevenLabs — только голос и музыка; звуковые эффекты — CC0 или Higgsfield-аудио.
- Перед каждой платной генерацией — смета кредитов и одобрение владельца.
- В игре видна плашка: «Деньги симулированные, это не финансовый совет».
- Бренд: #101521, #bbf15b; енот Unpaid CEO — наставник (элемент Higgsfield `64250b6d-541c-4a26-90bc-3cabeff8615e`).

## Review Focus

1. **Cash уходит в минус** — игра не падает, P&L показывает отрицательные строки со знаком «−», ставится факт `in_debt`; 2 месяца подряд ниже −$500 → факт `bankrupt` и доступна сцена-камбэк (тест — Task 8 и Task 11).
2. **Часы устройства переведены назад или игрок не заходил неделю** — автопилот считает 0…2 месяца, никогда не больше и не отрицательно (тест — Task 8).
3. **Битый или старый сейв** — копия в резерв, новая игра или миграция, без падения и без бесконечной перезагрузки (тест — Task 9).
4. **Большие и отрицательные суммы на RU и EN** — «$1.2M» / «1,2 млн $», «−$350» / «−350 $», текст не вылезает за кнопки (тест — Task 10 и Task 16).
5. **Нет подходящих сцен или сцены повторяются** — месяц идёт с 0 сцен; `once` не повторяется никогда; кулдаун соблюдается (тест — Task 7).

---

## Структура файлов

```
game/
  Packages/manifest.json
  Assets/UnpaidCeo/
    Core/                      UnpaidCeo.Core.asmdef (noEngineReferences)
      Primitives/  Money.cs  Pcg32.cs  CanonicalJson.cs  StateHash.cs
      Economy/     IndustryDef.cs  EconomyFormulas.cs  BusinessState.cs  BusinessSimulator.cs
      Life/        LifeStats.cs  TimeAllocation.cs  LifeBalance.cs  LifeSimulator.cs
      Narrative/   FactStore.cs  NpcRelations.cs  Conditions.cs  Effects.cs  StoryletDef.cs  StoryletSelector.cs
      Game/        GameState.cs  MonthDecisions.cs  MonthReport.cs  GameEngine.cs  DecisionLog.cs  Autopilot.cs  Chapters.cs
      Content/     ContentDb.cs  ContentValidator.cs
      Persistence/ SaveCodec.cs
      Format/      MoneyFormat.cs
      Platform/    GraphicsModeChooser.cs
    Core.Tests/                UnpaidCeo.Core.Tests.asmdef (EditMode) + Fixtures/
    Editor.Tests/              UnpaidCeo.Editor.Tests.asmdef (EditMode, проверка ассетов)
    Content/                   industries.json  balance.json  storylets_ch1.json  npcs.json  lessons.json  free_actions.json
    Yarn/                      Chapter1.yarnproject  *.yarn
    Game/                      UnpaidCeo.Game.asmdef — Bootstrap/ City/ Hero/ Ui/ Narrative/ Graphics/ Audio/ Services/
    Game.Tests/                UnpaidCeo.Game.Tests.asmdef (PlayMode)
    UI/                        *.uxml  Tokens.uss  Day.uss  Neon.uss
    Art/                       Imported/  Materials/  Shaders/UnpaidStylized.shadergraph
    Localization/              String tables UI (en, ru)
    Editor/                    BuildScript.cs  GlbImportPostprocessor.cs  AssetBudgetValidator.cs
tools/
  unity.sh                     обёртка: test-editmode | test-playmode | build-web
  check-build-size.sh
  art/chapter1-assets.json     манифест ассетов: id, тип, промпт, модель, параметры, смета
```

---

### Task 1: Окружение, Unity-проект и запуск тестов из терминала

**Files:**
- Create: `game/` (Unity 6 LTS проект, шаблон URP 3D), `game/Packages/manifest.json`, `game/Assets/UnpaidCeo/Core/UnpaidCeo.Core.asmdef`, `.../Core.Tests/UnpaidCeo.Core.Tests.asmdef`, `.../Game/UnpaidCeo.Game.asmdef`, `.../Game.Tests/UnpaidCeo.Game.Tests.asmdef` (PlayMode), `.../Editor/UnpaidCeo.Editor.asmdef`, `.../Editor.Tests/UnpaidCeo.Editor.Tests.asmdef` (EditMode, только Editor), `tools/unity.sh`, `.gitignore` (Unity), `game/Assets/UnpaidCeo/Core.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: `tools/unity.sh test-editmode|test-playmode|build-web` — коды выхода 0/≠0, отчёт `game/TestResults/*.xml`.

- [ ] **Step 1: Проверить Mac.** Через desktop-commander (нативный терминал macOS): `ls /Applications/Unity/Hub/Editor/`, `xcodebuild -version`, `git --version`, `gh auth status`. Нет Unity 6 LTS — попросить владельца установить через Unity Hub и войти в аккаунт (лицензия Personal). Нет `gh` авторизации — синхронизация через облачный клон (stage → commit → push).
- [ ] **Step 2: Клонировать репозиторий** в `~/Projects/unpaid-ceo` и создать проект: `Unity -batchmode -createProject game -quit` с URP; добавить пакеты в `manifest.json`: URP, `com.unity.localization`, `com.unity.addressables`, `com.unity.cloud.gltfast`, `com.unity.nuget.newtonsoft-json`, `com.unity.test-framework`; Yarn Spinner for Unity 3.x — по инструкции docs.yarnspinner.dev «Installation for Unity».
- [ ] **Step 3: Написать smoke-тест** `SmokeTests.Core_assembly_loads()` — `Assert.Pass()`; asmdef `UnpaidCeo.Core` с `noEngineReferences: true` и ссылкой на Newtonsoft.
- [ ] **Step 4: Написать `tools/unity.sh`** — путь к Unity из `UNITY_PATH` или последней `6000.3.*` в Hub; `test-editmode` = `-batchmode -projectPath game -runTests -testPlatform EditMode -testResults game/TestResults/editmode.xml -logFile -`; `build-web` = `-executeMethod UnpaidCeo.Editor.BuildScript.BuildWeb -quit`.
- [ ] **Step 5: Запустить** `tools/unity.sh test-editmode` → Expected: код 0, в XML `result="Passed"`.
- [ ] **Step 6: Commit** `chore: scaffold Unity 6 project, assemblies and test runner`.

### Task 2: Примитивы ядра — Money, Pcg32, CanonicalJson, StateHash

**Files:**
- Create: `Core/Primitives/Money.cs`, `Pcg32.cs`, `CanonicalJson.cs`, `StateHash.cs`
- Test: `Core.Tests/Primitives/MoneyTests.cs`, `Pcg32Tests.cs`, `StateHashTests.cs`

**Interfaces:**
- Produces:
  - `readonly struct Money : IEquatable<Money>, IComparable<Money>` — `long Cents`; `static Money FromCents(long)`; `static Money FromDollars(decimal)`; `+`, `-`, унарный `-`, `<`, `>`; `Money MulRound(double factor)`; `static Money Zero`.
  - `sealed class Pcg32` — `Pcg32(ulong seed, ulong seq)`; `uint NextUInt()`; `int NextInt(int maxExclusive)` (без смещения, отбраковкой); `double NextDouble()` [0,1); `PcgState State { get; }`; `static Pcg32 FromState(PcgState)`; `struct PcgState { ulong S; ulong Inc; }`.
  - `static class CanonicalJson { string Serialize(object o); }` — свойства по алфавиту, без пробелов, InvariantCulture.
  - `static class StateHash { string Compute(object o); }` — SHA-256 hex (нижний регистр) от `CanonicalJson`.

- [ ] **Step 1: Тесты**

```csharp
[Test] public void MulRound_rounds_half_away_from_zero() {
  Assert.AreEqual(3, Money.FromCents(5).MulRound(0.5).Cents);    // 2.5 → 3
  Assert.AreEqual(-3, Money.FromCents(-5).MulRound(0.5).Cents);  // −2.5 → −3
  Assert.AreEqual(450, Money.FromDollars(4.50m).Cents); }
[Test] public void Pcg32_matches_reference_vector() {               // pcg32 demo: seed 42, seq 54
  var r = new Pcg32(42, 54);
  uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
  foreach (var e in expected) Assert.AreEqual(e, r.NextUInt()); }
[Test] public void Pcg32_state_roundtrip_continues_sequence() { /* 10 шагов, FromState(State) → следующие 5 значений совпадают */ }
[Test] public void StateHash_is_stable_and_sensitive() {
  Assert.AreEqual(StateHash.Compute(new { b = 1, a = 2 }), StateHash.Compute(new { a = 2, b = 1 }));
  Assert.AreNotEqual(StateHash.Compute(new { a = 2 }), StateHash.Compute(new { a = 3 })); }
```

- [ ] **Step 2: Запустить** `tools/unity.sh test-editmode` → Expected: FAIL (типы не определены).
- [ ] **Step 3: Реализовать** — PCG32 XSH-RR 64/32 по эталону pcg-random.org (`srandom`: state=0, inc=(seq<<1)|1, step, state+=seed, step).
- [ ] **Step 4: Запустить** → Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): money, pcg32, canonical json and state hash`.

### Task 3: Экономические формулы

**Files:**
- Create: `Core/Economy/EconomyFormulas.cs`
- Test: `Core.Tests/Economy/EconomyFormulasTests.cs`

**Interfaces:**
- Produces: `static class EconomyFormulas` —
  `double PriceFactor(Money price, Money refPrice, double elasticity)` = (price/ref)^(−e);
  `double MarketingFactor(Money spend, double a, Money k)` = 1 + a·ln(1 + spend/k);
  `int Demand(int baseDemand, params double[] factors)` = floor(base × Π factors);
  `int UnitsSold(int demand, int capacity)` = min;
  `double GrossMargin(Money price, Money unitCost)`;
  `Money TaxReserve(Money operatingProfit, double rate)` (0 при ≤ 0);
  `int BreakEvenUnits(Money fixedMonthly, Money price, Money unitCost)` = ceil(fixed / (price − unitCost)), `int.MaxValue` если price ≤ unitCost;
  `Money LoanPayment(Money principal, double apr, int months)` — аннуитет, при apr = 0 → principal/months.

- [ ] **Step 1: Тесты (значения посчитаны вручную)**

```csharp
[Test] public void Demand_example_matches_hand_calculation() {
  double m = EconomyFormulas.MarketingFactor(Money.FromDollars(500), 0.25, Money.FromDollars(500)); // 1 + 0.25·ln2
  Assert.AreEqual(1.173287, m, 1e-6);
  Assert.AreEqual(1173, EconomyFormulas.Demand(1000, 1.0, m, 1.0, 1.0, 1.0));
  Assert.AreEqual(353, EconomyFormulas.Demand(1000, EconomyFormulas.PriceFactor(Money.FromCents(900), Money.FromCents(450), 1.5))); }
[Test] public void Margin_breakeven_tax() {
  Assert.AreEqual(0.7333, EconomyFormulas.GrossMargin(Money.FromCents(450), Money.FromCents(120)), 1e-4);
  Assert.AreEqual(304, EconomyFormulas.BreakEvenUnits(Money.FromDollars(1000), Money.FromCents(450), Money.FromCents(120)));
  Assert.AreEqual(int.MaxValue, EconomyFormulas.BreakEvenUnits(Money.FromDollars(1000), Money.FromCents(100), Money.FromCents(120)));
  Assert.AreEqual(28_500, EconomyFormulas.TaxReserve(Money.FromCents(114_000), 0.25).Cents);
  Assert.AreEqual(0, EconomyFormulas.TaxReserve(Money.FromCents(-5_000), 0.25).Cents); }
[Test] public void Loan_payment_12pct_12m() {
  Assert.AreEqual(88_849, EconomyFormulas.LoanPayment(Money.FromDollars(10_000), 0.12, 12).Cents);
  Assert.AreEqual(50_000, EconomyFormulas.LoanPayment(Money.FromDollars(6_000), 0.0, 12).Cents); }
```

- [ ] **Step 2: Запустить** → FAIL. **Step 3: Реализовать.** **Step 4: Запустить** → PASS.
- [ ] **Step 5: Commit** `feat(core): economy formulas`.

### Task 4: Контент — схема, загрузка, баланс главы 1

**Files:**
- Create: `Core/Economy/IndustryDef.cs`, `Core/Life/LifeBalance.cs`, `Core/Content/ContentDb.cs`, `Core/Content/ContentValidator.cs`, `Content/industries.json`, `Content/balance.json`
- Test: `Core.Tests/Content/ContentDbTests.cs`, `Core.Tests/Fixtures/*.json`

**Interfaces:**
- Produces:
  - `enum IndustryKind { Gig, Business }`; `sealed class IndustryDef` — `Id, Kind, Price, RefPrice, UnitCost, BaseDemand, Elasticity, MarketingA, MarketingK, CapacityPerUnit, CapacityPerWorkPoint, FixedMonthly, Capex, ReceivableDelayMonths, RequiredStudy`.
  - `sealed class Balance` — `TaxRate (0.25), BillsStartAfter ($100), PersonalCosts {Room, Food, Transit, Essentials}, Chapter1CashGoal ($1 000), DebtBankruptLine (−$500), EventsPerMonthWeights [1,2,1], AutopilotHoursPerMonth (4), AutopilotMaxMonths (2), TutorialGigPay ($40), Life : LifeBalance, Sources: [{field, url, checkedOn}]`.
  - `sealed class ContentDb` — `Industries`, `Balance`, `Storylets`, `Npcs`, `Lessons`, `FreeActions`; `static ContentDb FromJson(ContentFiles files)` (отсутствующий файл = пустой список, файлы сцен, NPC, уроков и действий появятся в Tasks 11–12); `ValidationResult Validate()` (`IsValid`, `Errors: List<string>`).
  - `sealed class FreeActionDef` — `Id, int ActionPoints, Money Cost, List<Effect> Effects (тип из Task 7, парсинг подключается там же), string JournalKey`.
- Глава 1 содержит три гига: `gig_delivery`, `gig_resale`, `gig_ai_freelance` (последний требует учёбы).

- [ ] **Step 1: Тесты** — `Loads_fixture_content` (3 гига, `Balance.TaxRate == 0.25`, `BillsStartAfter.Cents == 10_000`); `Validate_reports_unknown_industry_and_negative_price` (ошибки содержат id); `Real_content_is_valid` (загрузка `Assets/UnpaidCeo/Content/*.json` → `IsValid`).
- [ ] **Step 2: Запустить** → FAIL.
- [ ] **Step 3: Реализовать загрузчик и валидатор.** Значения `balance.json` (аренда комнаты в Бронксе, еда, проезд, ставка гигов) собрать из открытых источников (NYC, SBA, площадки гиг-работы) и записать в `Sources` с датой проверки.
- [ ] **Step 4: Запустить** → PASS. **Step 5: Commit** `feat(content): schema, loader, validator, chapter 1 balance`.

### Task 5: Симуляция бизнеса и месяца (деньги)

**Files:**
- Create: `Core/Economy/BusinessState.cs`, `Core/Economy/BusinessSimulator.cs`
- Test: `Core.Tests/Economy/BusinessSimulatorTests.cs`

**Interfaces:**
- Consumes: `EconomyFormulas`, `IndustryDef`, `Money`.
- Produces:
  - `sealed class BusinessState` — `Id, IndustryId, LotId, Money Price, int Units (точки/единицы), Money Marketing, List<Money> Receivables`.
  - `readonly struct BusinessInputs(double ReputationFactor, double SeasonFactor, double EventFactor, int WorkShare /*0..100*/)`.
  - `sealed class BusinessMonthResult` — `UnitsSold, Revenue, Cogs, GrossProfit, Fixed, Marketing, OperatingProfit, CashCollected`.
  - `static BusinessMonthResult BusinessSimulator.Simulate(BusinessState b, IndustryDef d, BusinessInputs i)` — гиг: мощность = `CapacityPerWorkPoint × WorkShare`; бизнес: `CapacityPerUnit × Units`; дебиторка сдвигает поступление на `ReceivableDelayMonths`.

- [ ] **Step 1: Тесты** — `Cart_month_matches_hand_calculation` (данные из Task 3: 800 продано при мощности 800, выручка 360 000¢, COGS 96 000¢, постоянные 100 000¢, маркетинг 50 000¢ → операционная 114 000¢); `B2B_receivables_arrive_after_delay` (задержка 1: месяц 1 `CashCollected == 0`, месяц 2 — выручка месяца 1); `Gig_capacity_scales_with_work_share` (WorkShare 50 → половина от 100).
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(core): business month simulation`.

### Task 6: Жизнь — шкалы, баланс времени, очки действий

**Files:**
- Create: `Core/Life/LifeStats.cs`, `TimeAllocation.cs`, `LifeSimulator.cs`
- Test: `Core.Tests/Life/LifeSimulatorTests.cs`

**Interfaces:**
- Produces:
  - `sealed class LifeStats` — `Energy, Relationships, Reputation, Honesty, Connections` (0..100, сеттеры обрезают).
  - `readonly struct TimeAllocation(int Work, int Family, int Rest, int Study)`; `static bool TryCreate(int w,int f,int r,int s, out TimeAllocation t)` — false если сумма ≠ 100 или есть отрицательное.
  - `sealed class LifeBalance` — `RestBaseline, RestCoef, WorkSafe, BurnCoef, FamilyBaseline, FamilyCoef, StudyCoef, BaseActionPoints (3), BurnoutThreshold, BurnoutErrorChance`.
  - `readonly struct LifeDelta(int Energy, int Relationships, int StudyXp)`.
  - `static class LifeSimulator` — `LifeDelta Apply(LifeStats s, TimeAllocation t, LifeBalance b)`: energy += round(RestCoef·(Rest−RestBaseline) − BurnCoef·max(0, Work−WorkSafe)); relationships += round(FamilyCoef·(Family−FamilyBaseline)); xp = round(StudyCoef·Study). `int ActionPoints(LifeStats s, int skillBonus, LifeBalance b)` = Base + skillBonus − (Energy < BurnoutThreshold ? 1 : 0), минимум 1. `double ErrorChance(LifeStats s, LifeBalance b)`.

- [ ] **Step 1: Тесты** (фикстура: RestBaseline 25, RestCoef 0.4, WorkSafe 60, BurnCoef 0.5, FamilyBaseline 15, FamilyCoef 0.5, StudyCoef 0.2, BurnoutThreshold 30) — `Rest40_Work70` → Energy +1 (6 − 5); `Family5` → Relationships −5; `Clamp_at_bounds` (Energy 98 + 10 → 100; 2 − 10 → 0); `Burnout_costs_one_action_point_never_below_one`; `TryCreate_rejects_sum_not_100_and_negatives`.
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(core): life stats and time balance`.

### Task 7: Нарратив — факты, отношения, условия, эффекты, выбор сцен

**Files:**
- Create: `Core/Narrative/FactStore.cs`, `NpcRelations.cs`, `Conditions.cs`, `Effects.cs`, `StoryletDef.cs`, `StoryletSelector.cs`
- Test: `Core.Tests/Narrative/*Tests.cs`

**Interfaces:**
- Produces:
  - `FactStore` — `Set(string id, int month)`, `Clear(string id)`, `Has(string id)`, `int? MonthSet(string id)`.
  - `NpcRelations` — `int Get(string npc)` (по умолчанию 0), `Add(string npc, int delta)` с обрезкой −100…+100.
  - `NarrativeContext` — `Month, Chapter, Cash, LifeStats, FactStore, NpcRelations, IReadOnlyCollection<string> IndustryIds`.
  - `abstract class Condition { bool Evaluate(NarrativeContext c); static Condition Parse(JObject j); }` — типы JSON: `{"fact":"x"}`, `{"notFact":"x"}`, `{"stat":"energy","op":">=","value":40}`, `{"npc":"tony","op":">=","value":20}`, `{"cash":{"op":"<","value":0}}` (центы), `{"sinceFact":"x","min":3,"max":12}`, `{"chapter":1}`, `{"industry":"gig_resale"}`.
  - `abstract class Effect { static Effect Parse(JObject j); }` — `{"cash":-5000}`, `{"stat":"honesty","delta":-10}`, `{"npc":"tony","delta":15}`, `{"setFact":"x"}`, `{"clearFact":"x"}`, `{"reserve":10000}`, `{"lesson":"revenue_vs_profit"}`. Применение — `EffectApplier` в Task 8.
  - `sealed class StoryletDef` — `Id, YarnNode, Arc, List<Condition> When, int Weight, bool Once, int CooldownMonths, Dictionary<string, List<Effect>> Choices`.
  - `static IReadOnlyList<StoryletDef> StoryletSelector.Pick(IReadOnlyList<StoryletDef> all, NarrativeContext c, IReadOnlyDictionary<string,int> lastShown, IReadOnlyCollection<string> seenOnce, Pcg32 rng, int[] eventsPerMonthWeights)` — сначала количество (0/1/2 по весам), затем взвешенный выбор без повторов внутри месяца.

- [ ] **Step 1: Тесты** — `Conditions_parse_and_evaluate` (по одному на тип); `SinceFact_window` (факт в месяце 2: месяц 4 при min 3 → false, месяц 5 → true, месяц 15 при max 12 → false); `Npc_clamped` (+150 → 100); **`No_eligible_storylets_returns_empty`**; **`Once_never_repeats_over_100_months`**; **`Cooldown_respected`** (кулдаун 3: показ в месяце 1 → недоступна в 2–3, доступна в 4); `Same_seed_same_picks` (два `Pcg32(7,1)` → одинаковые списки за 24 месяца).
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(core): narrative facts, conditions, effects, storylet selection`.

### Task 8: GameEngine — месяц целиком, журнал решений, главы, автопилот

**Files:**
- Create: `Core/Game/GameState.cs`, `MonthDecisions.cs`, `MonthReport.cs`, `GameEngine.cs`, `DecisionLog.cs`, `Autopilot.cs`, `Chapters.cs`, `Core/Narrative/EffectApplier.cs`
- Test: `Core.Tests/Game/GameEngineTests.cs`, `AutopilotTests.cs`, `ReplayTests.cs`

**Interfaces:**
- Consumes: Tasks 2–7.
- Produces:
  - `sealed class GameState` — `Version (1), Seed, Month, Chapter, Cash, Reserve, LifetimeEarnings, LifeStats Life, int StudyXp, FactStore Facts, NpcRelations Npcs, List<BusinessState> Businesses, HeroAppearance Hero, Dictionary<string,int> StoryletLastMonth, HashSet<string> StoryletsSeen, PcgState Rng, List<JournalEntry> Journal, List<DecisionLogEntry> Log, int MonthsInDeepDebt, long LastSeenUnix, MonthDecisions LastDecisions`.
  - `sealed class HeroAppearance` — `Species ("raccoon"|"fox"|"cat"), Outfit (0|1), ColorIndex`.
  - `sealed class MonthDecisions` — `TimeAllocation Time; List<LeverChange> Levers; List<string> FreeActions`.
  - `sealed class DecisionLogEntry` — `Month, Kind ("tutorialGig"|"storyletChoice"|"endMonth"), Payload (canonical JSON)`.
  - `sealed class JournalEntry` — `Month, Kind ("decision"|"consequence"), TextKey, Args`.
  - `sealed class GameEngine(ContentDb c)` — `GameState NewGame(ulong seed, HeroAppearance hero)`; `void TutorialGig(GameState s)` (+`TutorialGigPay`); `IReadOnlyList<StoryletDef> BeginMonth(GameState s)`; `void ChooseStoryletOption(GameState s, string storyletId, string choiceId)`; `MonthReport EndMonth(GameState s, MonthDecisions d)`; `ChapterStatus Chapter(GameState s)`; `static GameState Replay(ContentDb c, ulong seed, HeroAppearance hero, IEnumerable<DecisionLogEntry> log)`.
  - `static class Autopilot` — `int MonthsToRun(long lastSeenUnix, long nowUnix, Balance b)`; `IReadOnlyList<MonthReport> Run(GameEngine e, GameState s, int months)` (повторяет `LastDecisions`, без сцен).
  - `sealed class MonthReport` — `Month, Revenue, Cogs, GrossProfit, Fixed, Marketing, OperatingProfit, TaxReserve, NetProfit, PersonalCosts, CashStart, CashEnd, List<string> Storylets, LifeDelta Life, List<string> Lessons, bool BurnoutMistake`.
  - Правила `EndMonth`: свободные действия из `ContentDb.FreeActions` тратят очки `LifeSimulator.ActionPoints` (лишние отклоняются) и применяют эффекты; `ReputationFactor = 0.8 + 0.004 × Reputation`; если `rng.NextDouble() < ErrorChance` — выручка месяца × 0.9 и запись в журнал (`BurnoutMistake = true`).
  - Факты, которые ставит движок: `first_loss`, `in_debt`, `bankrupt`, `chapter1_done`.

- [ ] **Step 1: Тесты**
  - `Bills_start_after_100_lifetime` — доход $90 → `PersonalCosts == 0`; доход $120 → полные личные расходы.
  - **`Negative_cash_sets_in_debt_and_bankrupt_after_two_months`** — cash −$600 два `EndMonth` подряд → `Has("bankrupt")`; движок не бросает исключений; `MonthReport.CashEnd.Cents < 0`.
  - `Chapter1_goal_requires_cash_and_reserve` — cash $1 000 без резерва → не пройдена; + резерв ≥ месячных личных расходов → пройдена и `chapter1_done`.
  - `Storylet_choice_applies_effects_and_journal` — выбор добавляет `JournalEntry` и меняет NPC.
  - `Free_actions_limited_by_action_points` — 3 очка, действия на 4 очка → применены только помещающиеся, остальные отклонены; эффекты применены.
  - `Burnout_mistake_cuts_revenue_10pct` — Energy ниже порога и rng-сид, дающий срабатывание → выручка × 0.9, `BurnoutMistake == true`.
  - **`Autopilot_months`** — 3 ч 59 мин → 0; 4 ч → 1; 9 ч → 2; 100 ч → 2; −5 ч → 0.
  - `Replay_reproduces_state_hash` — 12 месяцев со сценами и выбором → `StateHash(Replay(...)) == StateHash(original)`.
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(core): game engine, decision log, chapters, autopilot`.

### Task 9: Сохранения

**Files:**
- Create: `Core/Persistence/SaveCodec.cs`, `Game/Services/SaveService.cs`
- Test: `Core.Tests/Persistence/SaveCodecTests.cs`

**Interfaces:**
- Produces: `static class SaveCodec` — `string Encode(GameState s)`; `LoadResult Decode(string json)`; `LoadResult { GameState State; LoadStatus Status (Ok|Migrated|CorruptReset); string Backup }`. `SaveService` (Unity): PlayerPrefs `unpaidceo.save.v1`, при `CorruptReset` пишет `unpaidceo.save.backup`.

- [ ] **Step 1: Тесты** — `Roundtrip_preserves_hash`; **`Garbage_returns_CorruptReset_with_backup`** (`"{not json"` → статус, `Backup == "{not json"`, `State == null`); **`Future_version_is_not_loaded`** (`Version: 99` → CorruptReset, без исключения); `Missing_optional_fields_migrate` (сейв без `Journal` → `Migrated`, пустой журнал).
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat: versioned save codec with corrupt-save fallback`.

### Task 10: Форматирование денег EN/RU

**Files:**
- Create: `Core/Format/MoneyFormat.cs`
- Test: `Core.Tests/Format/MoneyFormatTests.cs`

**Interfaces:**
- Produces: `static class MoneyFormat` — `string Full(Money m, string lang)`, `string Compact(Money m, string lang)` (порог компактности $10 000; K/M/B — «тыс.»/«млн»/«млрд»).

- [ ] **Step 1: Тесты**

```csharp
[TestCase(123456789, "en", "$1,234,567.89")]
[TestCase(123456789, "ru", "1 234 567,89 $")]
[TestCase(-35000, "en", "−$350.00")]
[TestCase(-35000, "ru", "−350,00 $")]
public void Full(long cents, string lang, string expected) => Assert.AreEqual(expected, MoneyFormat.Full(Money.FromCents(cents), lang));
[TestCase(120000000, "en", "$1.2M")]   [TestCase(120000000, "ru", "1,2 млн $")]
[TestCase(1530000, "en", "$15.3K")]    [TestCase(95000, "en", "$950")]
[TestCase(100000000000, "en", "$1B")]  [TestCase(-35000, "en", "−$350")]
public void Compact(long cents, string lang, string expected) => Assert.AreEqual(expected, MoneyFormat.Compact(Money.FromCents(cents), lang));
```

- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(core): locale-safe money formatting`.

### Task 11: Сцены главы 1 на Yarn Spinner 3

**Files:**
- Create: `Content/storylets_ch1.json`, `Content/npcs.json`, `Content/free_actions.json`, `Yarn/Chapter1.yarnproject`, `Yarn/ch1_*.yarn`, `Game/Narrative/YarnBridge.cs`, `Game/Narrative/UitkDialoguePresenter.cs`
- Test: `Core.Tests/Content/StoryContentTests.cs`

**Interfaces:**
- Consumes: `StoryletDef`, `GameEngine.ChooseStoryletOption`.
- Produces: Yarn-команда `<<choose "choiceId">>` в каждой опции → `YarnBridge` вызывает `ChooseStoryletOption(state, currentStoryletId, choiceId)`; `UitkDialoguePresenter : DialoguePresenterBase` (Yarn 3, см. docs «Custom Dialogue Presenters») рисует реплики и опции в UI Toolkit; строки Yarn — через Unity Localised Line Provider (en, ru).
- Объём: ~40 сцен; арки «Сосед Тони», «Ростовщик», «Лучший друг»; 6 NPC: mentor, tony, loan_shark, best_friend, mom, fat_cat; обучающие сцены на факты `first_loss`, `in_debt`, `bankrupt` (камбэк); свободные действия главы 1: сверхурочно, отдых, учёба, нетворкинг, время с семьёй, хобби, «серая» схема, кредит у ростовщика, перевод в резерв.

- [ ] **Step 1: Тесты контента** — `Every_storylet_has_yarn_node` (поиск `title: <YarnNode>` в `Yarn/*.yarn`); `Yarn_choose_ids_match_json_choices` (множества равны для каждой сцены); `All_effects_and_conditions_parse`; `Npc_ids_exist`; `Every_line_has_ru_translation` (таблица ru без пустых строк); `Bankrupt_comeback_reachable` (контекст с `bankrupt` → хотя бы одна подходящая сцена).
- [ ] **Step 2: Запустить** → FAIL.
- [ ] **Step 3: Написать контент** EN, затем RU (глоссарий терминов в `Content/glossary.md`), и `YarnBridge` + `UitkDialoguePresenter`.
- [ ] **Step 4: Запустить** → PASS. **Step 5: Commit** `feat(narrative): chapter 1 storylets, arcs and Yarn bridge`.

### Task 12: Наставник и 5 уроков CEO Academy

**Files:**
- Create: `Content/lessons.json`, `Game/Ui/AcademyScreen.cs`, `UI/Academy.uxml`
- Test: `Core.Tests/Game/LessonTriggerTests.cs`

**Interfaces:**
- Produces: `LessonDef` — `Id, TriggerFact, TitleKey, BodyKey, Quiz[3] {QuestionKey, OptionKeys[3], CorrectIndex}, SkillReward`. Уроки: `revenue_vs_profit` (первый доход), `reserve` (первая оплата счетов), `first_loss`, `cash_gap` (`in_debt`), `break_even` (переход к главе 2). `MonthReport.Lessons` — уроки, сработавшие в этом месяце (каждый — один раз).
- [ ] **Step 1: Тесты** — `Lesson_fires_once_on_trigger`; `Quiz_correct_answer_grants_skill_xp`; `Lesson_keys_exist_in_en_and_ru`.
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat: mentor lessons and academy quizzes`.

### Task 13: Арт главы 1 — библия стиля, пилот, генерация, импорт

**Files:**
- Create: `tools/art/style-bible.md`, `tools/art/chapter1-assets.json`, `game/Assets/UnpaidCeo/Editor/GlbImportPostprocessor.cs`, `Editor/AssetBudgetValidator.cs`, `Art/Shaders/UnpaidStylized.shadergraph`, `Art/Materials/*`
- Test: `Editor.Tests/AssetBudgetTests.cs` (EditMode)

**Interfaces:**
- Produces: `AssetBudgetValidator.Check(GameObject prefab, AssetKind kind) → List<string>` (здание: LOD0 2 000–8 000 треугольников + LOD1/LOD2; персонаж: 15 000–20 000); каждый импортированный GLB получает материал `UnpaidStylized` (палитра + LUT дня/неона).
- Список этапа 1 в манифесте: енот-наставник; 3 вида героя (raccoon, fox, cat) × 2 образа; 6 NPC; ~12 зданий Бронкса; ~15 предметов; интерьер комнаты.

- [ ] **Step 1: Пилот (смета ≈ 100 кр.).** Концепт одного здания (Nano Banana Pro, 2 кр.) → Tripo H3.1 (12) / Tripo P2 (37) / Meshy 7 (47,5). Показать владельцу смету → после «да» сгенерировать, сравнить в Unity в стилизованном шейдере, зафиксировать выбор в `style-bible.md`.
- [ ] **Step 2: Тест бюджетов** — `All_imported_buildings_within_budget`, `All_characters_within_budget` → FAIL (ассетов ещё нет / валидатор не написан).
- [ ] **Step 3: Партия этапа 1 (смета ≈ 600 кр., точная — из манифеста через `get_cost`).** Показать смету → после «да» генерировать пачками (`generate_image_batch` → `generate_3d`), персонажи — Meshy 7 (quad, A-pose, rig, анимации idle/walk/celebrate/think).
- [ ] **Step 4: Импорт** — glTFast, `GlbImportPostprocessor` (материал, LOD через упрощение меша, коллайдер), `AssetBudgetValidator`.
- [ ] **Step 5: Запустить** `tools/unity.sh test-editmode` → PASS. **Step 6: Commit** `feat(art): chapter 1 assets, stylized shader, import pipeline`.

### Task 14: 3D-город Бронкс, день/неон, таймлапс месяца, комната героя

**Files:**
- Create: `Game/City/CityView.cs`, `LotView.cs`, `CameraRig.cs`, `DayNeonController.cs`, `TimelapseDirector.cs`, `Game/City/RoomScene.cs`; сцены `Scenes/Bronx.unity` (Addressable группа `district_bronx`), `Scenes/Room.unity`
- Test: `Game.Tests/CitySmokeTests.cs` (PlayMode)

**Interfaces:**
- Consumes: `GameState.Businesses[].LotId`, `MonthReport`.
- Produces: `CityView.Sync(GameState s)` — здание на лоте появляется при открытии бизнеса; `TimelapseDirector.Play(MonthReport r, float seconds = 6f)` — 5–8 с, всплывающая выручка над лотами; `DayNeonController.Set(TimeOfDay t)`; `CameraRig` — пан, зум, tilt-shift (DOF только в Оптимальном и Максе).

- [ ] **Step 1: Тесты** — `Bronx_scene_loads_via_addressables`; `Opening_business_spawns_building_on_lot`; `Timelapse_finishes_within_8_seconds`.
- [ ] **Step 2–4:** FAIL → реализовать → PASS (`tools/unity.sh test-playmode`). **Step 5: Commit** `feat(city): Bronx district, day/neon, month timelapse, room`.

### Task 15: Редактор героя

**Files:**
- Create: `Game/Hero/HeroCreator.cs`, `Game/Hero/HeroView.cs`, `UI/HeroCreator.uxml`
- Test: `Game.Tests/HeroCreatorTests.cs`

**Interfaces:**
- Produces: `HeroCreator.Result → HeroAppearance`; `HeroView.Apply(HeroAppearance)` (меш вида, образ, цвет); по умолчанию — случайный вид, кнопка «Готово» сразу активна (онбординг ≤ 20 секунд).
- [ ] **Step 1: Тесты** — `Default_hero_ready_in_one_tap`; `All_species_and_outfits_load` (3 × 2).
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(hero): character creator`.

### Task 16: Интерфейс (UI Toolkit), темы, локализация EN/RU

**Files:**
- Create: `UI/Tokens.uss`, `Day.uss`, `Neon.uss`, `Hud.uxml`, `BottomNav.uxml`, `Company.uxml`, `Decisions.uxml`, `Journal.uxml`, `Settings.uxml`; `Game/Ui/UiRoot.cs`, `PnlWaterfall.cs` (кастомный `VisualElement`), `DecisionsScreen.cs`, `TimeAllocationControl.cs`, `JournalScreen.cs`, `SettingsScreen.cs`; `Localization/UI` (en, ru)
- Test: `Game.Tests/UiSmokeTests.cs`

**Interfaces:**
- Consumes: `GameEngine`, `MoneyFormat`, `MonthReport`, `JournalEntry`.
- Produces: нижнее меню Город · Компания · Решения · Academy · Рейтинг (вкладка «Рейтинг» на этапе 1 — заглушка «скоро»); HUD (cash, месяц, энергия); кнопка «Следующий месяц»; `TimeAllocationControl` (4 ползунка, сумма всегда 100); плашка «Деньги симулированные, это не финансовый совет» в «Компании» и Academy; язык — по `Application.systemLanguage`/браузеру, переключатель в настройках.

- [ ] **Step 1: Тесты** — `All_tabs_open_without_errors` (en и ru); `Time_sliders_always_sum_100`; **`No_text_overflow_in_ru`** (для каждого `Label`/`Button` в ru: ширина текста ≤ ширины элемента после layout; скриншоты `TestResults/ui_ru_*.png`); `Disclaimer_visible_on_company_screen`; `No_hardcoded_strings` (все `Label.text` берутся из таблицы — проверка по `LocalizedString` биндингам).
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(ui): screens, day/neon themes, en/ru localization`.

### Task 17: Три режима графики и бенчмарк

**Files:**
- Create: `Settings/URP_Eco.asset`, `URP_Optimal.asset`, `URP_Max.asset` (+ уровни Quality), `Core/Platform/GraphicsModeChooser.cs` (чистая функция), `Game/Graphics/GraphicsModeService.cs`, `Game/Graphics/DeviceBenchmark.cs`
- Test: `Core.Tests/Platform/GraphicsModeChooserTests.cs` (EditMode) + `Game.Tests/GraphicsBudgetTests.cs` (PlayMode)

**Interfaces:**
- Produces: `enum GraphicsMode { Eco, Optimal, Max }` (в Core); `static GraphicsMode GraphicsModeChooser.Choose(float avgFrameMs, bool isMobile, int gpuMemMb)` — `> 33` мс → Eco; `< 12` мс и не мобильный и ≥ 2 048 МБ → Max; иначе Optimal; `GraphicsModeService.Apply(GraphicsMode m)` (уровень Quality, `targetFrameRate` 30/60/60, render scale, пост-эффекты); бенчмарк 3 с при первом запуске, выбор сохраняется.

- [ ] **Step 1: Тесты** — `Choose_table` (40 мс/mobile → Eco; 20 мс → Optimal; 10 мс/desktop/4096 → Max; 10 мс/mobile → Optimal); `DrawCalls_within_budget_per_mode` (сцена Бронкса с 12 зданиями, 60 кадров, `ProfilerRecorder "Draw Calls Count"` ≤ 100/250/600).
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat(graphics): eco/optimal/max modes with auto benchmark`.

### Task 18: Голос и музыка (ElevenLabs), звуковые эффекты

**Files:**
- Create: `Art/Audio/Voice/{en,ru}/*.mp3`, `Art/Audio/Music/day.mp3`, `neon.mp3`, `Art/Audio/Sfx/*` (CC0, лицензии в `Art/Audio/Sfx/LICENSES.md`), `Game/Audio/AudioDirector.cs`
- Test: `Core.Tests/Content/VoiceCoverageTests.cs`

**Interfaces:**
- Produces: озвучены реплики наставника главы 1 и короткие реплики NPC (EN, RU) через Yarn voice-over (ассет-таблицы локализации); музыка день/неон переключается вместе с `DayNeonController`.
- [ ] **Step 1: Проверить баланс ElevenLabs** и посчитать символы реплик главы 1 → показать смету (ориентир ~1 кредит за символ) → одобрение владельца.
- [ ] **Step 2: Тест** — `Every_mentor_line_has_voice_in_en_and_ru` → FAIL.
- [ ] **Step 3: Генерация** — дизайн голоса енота (`creative_design_voice` → сохранить), Толстого Кота; озвучка EN и RU; 2 музыкальные темы.
- [ ] **Step 4: Запустить** → PASS. **Step 5: Commit** `feat(audio): mentor voice en/ru, music, sfx`.

### Task 19: Онбординг — первые 60 секунд

**Files:**
- Create: `Yarn/ch1_onboarding.yarn`, `Game/Bootstrap/OnboardingFlow.cs`
- Test: `Game.Tests/OnboardingTests.cs`

**Interfaces:**
- Consumes: `HeroCreator`, `GameEngine.TutorialGig`, `UiRoot`.
- Produces: новая игра → герой (1 тап) → комната, наставник → гиг доставки (+$40) → реплика «выручка ≠ прибыль» → подсветка «Следующий месяц» → отчёт месяца 1. Предложение регистрации — не на этапе 1 (аккаунтов ещё нет).
- [ ] **Step 1: Тест** — `New_player_reaches_month1_report_in_at_most_6_taps` (симулированные нажатия; cash после гига == $40; отчёт показан).
- [ ] **Step 2–4:** FAIL → реализовать → PASS. **Step 5: Commit** `feat: 60-second onboarding`.

### Task 20: Web-сборка, бюджет размера, превью и проверка владельцем

**Files:**
- Create: `game/Assets/UnpaidCeo/Editor/BuildScript.cs`, `tools/check-build-size.sh`, `game/Assets/WebGLTemplates/UnpaidCeo/index.html` (фон #101521, логотип, прогресс-бар)

**Interfaces:**
- Produces: `UnpaidCeo.Editor.BuildScript.BuildWeb()` — Web, Brotli, Decompression Fallback, «Disk Size with LTO», шаблон `UnpaidCeo`, Addressables собираются до плеера; выход `game/Builds/Web`. Превью: ветка `gh-pages` → `https://jamesmirusa.github.io/https-unpaid.ceo/`.
- [ ] **Step 1: Собрать** `tools/unity.sh build-web` → Expected: код 0, есть `game/Builds/Web/index.html`.
- [ ] **Step 2: Проверить размер** `tools/check-build-size.sh game/Builds/Web` → Expected: сумма `Build/*` ≤ 20 971 520 байт, иначе FAIL со списком крупнейших файлов.
- [ ] **Step 3: Выложить превью** — запушить `game/Builds/Web` в `gh-pages`; включить Pages для ветки (`gh api repos/jamesmirusa/https-unpaid.ceo/pages`).
- [ ] **Step 4: Проверка на устройствах** — Chrome/Safari/Firefox на Mac; телефоны владельца (iPhone Safari, Android Chrome): загрузка ≤ 10 с на 4G, 60 fps в «Оптимальном», память ≤ 400 МБ (Safari Web Inspector), автоподбор режима, переключение языка.
- [ ] **Step 5: Плейтест владельца** — владелец проходит главу 1 и одобряет; замечания → задачи.
- [ ] **Step 6: Commit** `build: web build script, size budget, preview deploy`.
