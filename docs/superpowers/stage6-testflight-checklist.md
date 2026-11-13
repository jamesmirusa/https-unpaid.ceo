# Чек-лист: TestFlight и App Store (на потом)

Делаем, когда владелец решит оплатить Apple Developer Program ($99 в год). Пароли, ключи и подпись — только владелец.

## Нужно от владельца
- [ ] Apple Developer Program на свой Apple ID (developer.apple.com → Enroll).
- [ ] В Xcode → Settings → Accounts — войти тем же Apple ID; в проекте выбрать команду (Signing & Capabilities).
- [ ] App Store Connect → My Apps → «+» → новое приложение: имя «Unpaid CEO», bundle id `ceo.unpaid.game`, язык EN, SKU.

## Делаю я (после этого)
- [ ] `tools/unity.sh build-ios` (сборка для устройства) → Xcode → Product → Archive → Distribute → App Store Connect.
- [ ] Иконки всех размеров, экран запуска, описание EN/RU, скриншоты 6,9" и 6,5" (из `tools/ios/run-sim.sh`).
- [ ] Privacy: анкета «Данные» (аналитика анонимная, с согласием — как в игре), ссылка на политику конфиденциальности.
- [x] Encryption: `ITSAppUsesNonExemptEncryption = NO` пишется в Info.plist при каждой сборке (`Editor/IosPostprocess.cs`).
- [ ] Privacy manifest: проверить `PrivacyInfo.xcprivacy` в Xcode-проекте (Unity 6 кладёт свой; UGS Analytics,
      PlayerPrefs = UserDefaults — «required reason API»), дополнить при необходимости.
- [ ] Важно: `tools/unity.sh build-ios` каждый раз пересоздаёт `game/Builds/iOS` (иначе Unity падает на Xcode 27) —
      настройки подписи в Xcode после пересборки выбрать снова (Team).
- [ ] iPad: сейчас сборка только для iPhone; для iPad — макеты, проверка и скриншоты 13".
- [ ] TestFlight: внутренние тестеры (владелец), потом внешние.
- [ ] **Sign in with Apple** — возможность в Signing & Capabilities (не работает в бесплатной Personal Team), подключение
      в Unity Authentication (Apple), кнопка в «Настройки → Аккаунт».
- [ ] **Google Sign-In** — OAuth-клиент iOS в Google Cloud Console (владелец), URL scheme в Info.plist, Unity
      Authentication (Google).
- [ ] Отдельными решениями владельца: покупки (Unity IAP), реклама (LevelPlay), пуши.
