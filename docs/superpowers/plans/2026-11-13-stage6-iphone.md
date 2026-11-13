# Stage 6 — iPhone build

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans.

**Spec:** `docs/superpowers/specs/2026-11-13-stage6-iphone-design.md` (pre-approved by the owner, docs/AUTOPILOT.md §5).
**Branch:** `feat/stage6-iphone` from `design/unpaid-ceo-2-spec` after 5B is merged.

## Tasks
1. **Safe areas (TDD).** `Game/Ui/Kit/SafeArea.cs`: pure `Insets(Rect safe, Vector2 screen, Vector2 panel)` → panel-px
   insets (top, bottom, left, right); `UiRoot` pads the app root by them and re-applies on resolution change. EditMode
   test: iPhone 18 Pro values (Dynamic Island top, home bar bottom) and a full-screen web rect → zero.
2. **Touch targets (TDD).** PlayMode test at 1179×2556 (a 6.1" iPhone): every visible Button in the city, the Week
   screen, a room and the radial is ≥ 132 panel px on its short side (44 pt) or listed as an intended small control.
   Fix what fails.
3. **Build.** `BuildScript.BuildIos` + `ConfigureIos` (bundle id, portrait, iOS 16, IL2CPP ARM64, Metal, ASTC, 60 fps,
   simulator SDK with `UC_IOS_SIM=1`), Addressables for iOS; `tools/unity.sh build-ios`. Then switch back so the web
   build is unaffected (`build-web` switches itself).
4. **Simulator.** `tools/ios/run-sim.sh`: `xcodebuild -sdk iphonesimulator` → `simctl install/launch` → screenshots
   into `game/TestResults/ios/`; memory via `simctl spawn … footprint` or Xcode's `xctrace` where available.
5. **Owner's iPhone.** One short request with exact steps (Apple ID in Xcode → Personal Team → Run); keep working.
6. **Checklist** for TestFlight/App Store, Sign in with Apple/Google (needs Apple Developer).
7. Suites, web build still OK, review, PR, merge, roadmap, report.
