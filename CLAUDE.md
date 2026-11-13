# Unpaid CEO 2.0 — правила проекта для Claude

Обучающая игра-тайкун: игрок, который хочет открыть своё дело, становится «CEO без зарплаты». Unity 6000.6.2f1
(URP 17, UI Toolkit), веб на https://unpaid.ceo (ветка `gh-pages`), сервер — Unity Gaming Services (Cloud Code, C#).
Открытая карта: герой ходит по городу и заходит в здания — это главное, не ломать.

Что делать дальше — **`docs/ROADMAP.md`**. Спеки — `docs/superpowers/specs/`, планы — `docs/superpowers/plans/`,
решения прошлых этапов — `docs/superpowers/rulings/`, арт — `tools/art/style-bible.md`.

## Владелец
- Отвечать по-русски, просто, без жаргона: владелец не программист. Результаты показывать качественными скриншотами.
- Постоянное разрешение: «делай всё сам до конца», «разрешаю всё». Это покрывает: коммиты и пуши в ветки `feat/…`,
  PR и их слияние в `design/unpaid-ceo-2-spec` после зелёных тестов и ревью, выкладку веба (`tools/deploy-web.sh`).
- Ветка по умолчанию — `design/unpaid-ceo-2-spec` (ветки `main` нет). Новый этап — новая ветка `feat/…`. Без force-push.
- Не останавливаться вопросом «продолжать?» между задачами. Спрашивать только про необратимое, про деньги/кредиты
  и про то, что может решить только владелец.

## Жёсткие правила
- **Кредиты:** перед любой генерацией (Higgsfield, ElevenLabs, Meshy) — показать примерный расход и ждать «да».
  Уже одобрено: ≈250 кредитов Higgsfield на игровые иконки этапа 5Б. Принцип — «максимум качества при максимальной
  экономии»; модель выбирать по реальным отзывам и аналитике, а не по догадкам.
- **ElevenLabs — только голос и музыка** (звуки интерфейса этапа 5Б — тоже аудио, согласованы в спеке).
  Никогда не делать в ElevenLabs фото и видео. Картинки — Higgsfield.
- **Секреты:** никогда не просить пароли, ключи и коды в чате и не вводить их самому. `ugs login`, вход Apple ID,
  подпись Xcode — владелец делает сам.
- **Скачивание:** сторонние файлы и программы — только с явного разрешения. Разрешено: набор иконок Phosphor (MIT).
  Не ставить сторонние бинарники для «реверс-инжиниринга» чужих игр.
- Почта владельца — личная, никуда не отправлять.

## Как устроен код
- Правила игры — только в Core (`game/Assets/UnpaidCeo/Core`). Сервер воспроизводит журнал хода; всё, что меняет
  игру, проверяется сервером. UI только показывает и вызывает Core.
- `ChoiceState` общий для комнат, экрана «Неделя» и мест: одно правило — один источник.
- Тексты только через таблицу строк EN/RU: `game/Assets/UnpaidCeo/Localization/Source/UI.tsv` (ключ\tEN\tRU),
  потом импорт (ниже). Никаких зашитых строк.
- Старые сохранения должны открываться.
- Вес веб-сборки ≤ 20 МБ (`tools/check-build-size.sh`).

## Процесс
- TDD: сначала тест, увидеть красный, потом код, зелёный. Перед коммитом — зелёные EditMode и PlayMode.
- Новая работа: superpowers — brainstorming → спека → writing-plans → executing-plans (журнал
  `.superpowers/sdd/<план>/progress.md`, добавлять `git add -f`). Каждое отступление — строка
  `Ruling: что решил — почему — чего стоит ошибка`. В конце — ревью всей ветки свежим ревьюером.
- Каждый шаг этапа заканчивается выкладкой на сайт и скриншотами для владельца.

## Команды (из корня репозитория)
- Тесты Unity: `tools/unity.sh test-editmode [фильтр]`, `tools/unity.sh test-playmode [фильтр]`;
  разбор падений: `python3 tools/test-failures.py editmode|playmode N`.
- Импорт строк: `tools/unity-run.sh UnpaidCeo.Editor.StringTableImporter.ImportAll strings`.
- Демо-скриншоты 1080×1920: `tools/unity.sh test-playmode WeekScreenDemo` → `game/TestResults/demo/*.png`.
- Веб: `tools/unity.sh build-web` → `tools/check-build-size.sh` → `tools/deploy-web.sh`; проверить, что на
  https://unpaid.ceo отдаётся новый `*.data.unityweb`. Локально: `python3 -m http.server 8765` в `game/Builds/Web`.
- Сервер: `cd server/cloudcode && ~/.dotnet/dotnet test`; выкладка `tools/server/deploy.sh` (нужен `ugs login`
  владельца); проверка конфигурации `python3 tools/server/check_ugs_config.py`.

## Никогда не коммитить
`game/Assets/AddressableAssetsData/link.xml` (и `.meta`), `addressables_content_state.bin`,
`game/Data/Plugins/lib_burst_generated.wasm`, `game/ProjectSettings/Packages/dev.yarnspinner/UnpaidCeo.Game-generated.ysls.json`,
`game/Assets/Resources*` (корневая папка Resources), сырые генерации в `tools/art/` (там только то, что уже в репозитории).
Добавлять файлы по именам, не `git add -A`.
