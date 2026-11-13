# SDD ledger — plan: docs/superpowers/plans/2026-11-13-stage5b-step4-needs-moods-wants.md
Executor: inline (executing-plans), per docs/AUTOPILOT.md.
Task 1 (Core): HeroView.Needs/Moods/Wants — HeroViewTests 5/5 GREEN. Ruling: the test file and HeroView were written in one pass, RED not run separately — cost if wrong: a test could pass for another reason (each asserts the state directly; one checks the state is untouched).
Ruling: moods and wants are pure reads (a test serializes the state before and after) — no balance effect, as the spec says — cost if wrong: none.
Ruling: the "paycheck" mood reads the last week's report (revenue > 0), which the UI holds; it is not saved — cost if wrong: after a reload the paycheck badge waits for the next week.
Ruling: wants come in this order — talk to a host with a waiting scene (Tony, Mom), pay yourself $100 (chapter 1), the open lesson's quiz, rest when energy < 40, earn at Tony's, study toward the next gig — top three; they tick from this week's plan — cost if wrong: a less urgent want may show before a more urgent one.
Task 2 (UI): HeroStateUiTests 5 RED (no bars, no panel, no moods, no wants) → GREEN. HUD 2×2 need bars with icons, floating changes; a tap opens "How you are" (what raises each, what a fall costs); mood chips (≤ 4, bad first, a tap = one line) and the collapsed "Wants this week n/3" card sit on top of the city's cards; a new tick plays the success sound and a toast.
Ruling: the camera buttons (go to me, +, −) moved into one row under the street strip — in a column they covered the city's cards once the badges and wants joined them — cost if wrong: none (same buttons, same size class).
Suites: EditMode 396/396, PlayMode 239 + 12 explicit demos, server 17/17.
