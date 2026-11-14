# Журнал: компактная верхняя панель (AUTOPILOT §8)

- [x] 1. CompactHudTests написан, красный (нет HudView/NeedRing/UiRoot.Hud).
- [x] 2–5. NeedRing, Hud.uxml, HudView, UiRoot, GameBootstrap, USS, строки — CompactHudTests 10/10.
- [x] Старые тесты: полоса мест → «каждое место есть в листе Мест»; дельта нужды — над кольцом (свёрнуто) / над полоской (открыто).
- [x] Скриншоты «было/стало»: симуляторы iPhone 13 mini (375 pt) и iPhone 18 Pro Max (430 pt), `game/TestResults/ios/{before,after}`.

Ruling: раскрытая часть — поверх города (position absolute), а не сдвигает его вниз — иначе при каждом тапе прыгали бы карточки и камера; ошибка стоит лишь перекрытия города на ≤ 5 с.
Ruling: кнопки «Дальше по главе» / «Выбрать бизнес» переехали в саму полосу, а текст цели — в раскрытую часть — главное действие главы не должно прятаться при сворачивании; ошибка — чуть теснее полоса, когда кнопка видна.
Ruling: «12 % экрана» меряю от верха безопасной зоны (часы и Dynamic Island — системные, их не убрать); на 13 mini полоса ≈ 58 pt из 812 (7 %) — ошибка в трактовке стоила бы лишь пересчёта, запас большой.
Ruling: тревога (тост) показывается один раз за неделю на каждую нужду/платёж, а пульс кольца — всё время, пока опасно — иначе тосты сыпались бы при каждом обновлении; ошибка — игрок может пропустить тост, но пульс остаётся.
Ruling: пульс — переключение класса раз в 0,6 с с переходом scale (в UI Toolkit нет бесконечных анимаций в USS) — дешево по ресурсам.
Ruling: событие PlaceFocusRequested оставлено (камера наводится на место через загрузчик) — полоса мест ушла, но API не ломаем.
Ruling: пока панель открыта, кнопки камеры, карточки и вывески прячутся (класс hud-open) — иначе рисовались поверх панели (порядок отрисовки UI Toolkit); панель всё равно сама сворачивается за 5 с.
- [x] Ревью свежим ревьюером: 10 замечаний. Исправлены: мусор на каждый кадр в NotifyCityMotion (один таймер, сдвигается), кнопки главы в полосе во время забега (RunMode), свайп вверх по полосе снова открывал панель, невидимые карточки ловили тапы в режиме прогулки, запомненная «открытая» панель не сворачивалась сама, пульс тикал без опасности. Полный PlayMode 262/262 (+16 демо), EditMode 399/399.
Ruling: не исправлял — несколько тостов в начале недели (очередь тостов уже есть, 2 с каждый — терпимо), панорама по улице сворачивает панель (так и задумано: «тап по городу»), общий сброс PlayerPrefs в тестах (сейчас утечки нет) — ошибка стоит лишь мелкого неудобства.
Ruling: в режиме прогулки карточки прячутся через visibility (не только прозрачность) — иначе невидимые кнопки ловили тапы; цена — исчезают сразу, без плавности.

## 2026-11-14 · server deploy after the owner's Unity Hub sign-in
- Ruling: ServerDeploy waits up to 90 s for the Hub sign-in before it reads the environments — an Editor started from the command line gets the account a few seconds late and failed with "no environment 'production'" — cost of a mistake: a slower failure (90 s) when nobody is signed in.
- Ruling: the log says only "signed in", not the account name — the account name is the owner's personal email — cost: none.
- Ruling: tests run with -buildTarget WebGL — after an iOS build the textures import at iOS sizes and the web art budget tests fail on a clean tree — cost: one platform reimport when the last build was iOS.
- Blocked: with the sign-in in place, listing the environments of project 4da0a5db-… answers 403 Forbidden (3 tries). The signed-in account has no rights on that Unity Cloud project: the owner checks it in the Unity Cloud dashboard (the project is in org "jamamir").
