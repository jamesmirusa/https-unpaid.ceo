# Stage 4 — the living city: places, weeks, the study gate, ladders (2026-11-11)

Owner's brief: stronger graphics and more comfortable screens; a map where you choose where to go — Home, Office,
Bank, Pawnshop, Bar and so on — each with a quality 3D interior and a character to talk to, take tasks from, follow a
story line with; buy property, open businesses and develop them step by step (equip, expand, automate); months become
weeks, with more exact progress and expense maths; every two weeks, to go on, the player must answer three academy
questions — that study is the hero's "study" stat — and the questions are playful demonstrations, not text to text.
"You got the idea, you may strengthen my concept." A mock-up was attached as optional help.

## The idea in one paragraph

The city is the menu. Everything the player can do is done somewhere and with someone: you rest at home, earn extra at
Tony's, meet people at the bar, borrow at the pawnshop or the bank, learn in the evening class. Time is the scarce
resource — a week has 168 hours, and the hero's week is what the player plans. Every second week the mentor stops the
hero at the door: three small hands-on problems, and only then the game goes on. What the player learns there is the
hero's knowledge, and knowledge is what unlocks the next rung of every ladder: a better gig, a first business, its
equipment, a second location, automation, property.

## 4A — Places (built now)

| Place | Host | What is done there |
|---|---|---|
| Home | Unpaid CEO, the mentor, on the fire escape | sleep, family, the journal |
| Tony's Bodega | Tony, bulldog | overtime: unload the truck for cash |
| Pizza Bar | Jay, otter, best friend | networking, a night off |
| Laundromat Office | Sam, beaver, future partner | the plan, the report, choosing a business |
| Pawnshop | Mr. Fins, shark | fast expensive money, the gray scheme |
| Bank | Ms. Lee, owl | the reserve; business loans from chapter 2 |
| CEO Academy | the mentor | the evening class, lessons |

- A place is a picture of its interior with the host in it (one cinematic stylized-3D render per place), the host's
  line of the month — each line is a small truth about money — the trust the host has in the hero, and big buttons.
- A button that plans a free action shows what it costs and gives, taken from the action's definition, and puts it
  into this month's plan: the same list the plan popup ticks. Nothing is duplicated and the server replay is untouched.
- Markers hang over the buildings in the 3D city; a tap walks in. The dock's "Places" lists every place, so a place
  without a building in the current district is still reachable.
- Every free action has a place (a test keeps it so). The cast is the game's own (npcs.json).

Not in 4A: walkable 3D rooms. A rendered interior loads in a moment on a phone and looks better than a real-time room
the web build could afford; real 3D stays in the city.

## 4B — Weeks and exact money

- The turn is a week. Four weeks are a month; the calendar shows "week 3 of March".
- Money moves when it really moves: gig pay weekly; rent on the first week of the month; phone, transit pass and
  insurance on their own weeks; loan payments monthly on their date; quarterly tax from the reserve; B2B invoices paid
  after their delay in weeks. The week's result lists what was paid this week and what falls due next week.
- "My week" replaces the percent sliders: 168 hours split into sleep, work, travel, chores, family, own time, study —
  in hours, with the consequences shown before the week is played (too little sleep costs next week's energy).
- Daily budget: 7 weeks a day, autopilot for missed days; chapter goals are re-tuned so the whole game still takes
  about a month of days.
- Core, save format (v3) and the server module change together; old saves convert month N to week 4N.

## 4C — The study gate

- Every second week the mentor asks three questions before "Next week" works again. A wrong answer is explained and
  the question comes back; nothing is lost but time. Passing gives knowledge; knowledge is the hero's study stat and
  level (Hustler → Operator → Owner → Boss → Investor).
- Questions are small machines, not text:
  - **Scales** — put coins on "revenue" and "costs" until the profit shown is right.
  - **Shelves** — sort cards: asset or liability, fixed or variable, need or want.
  - **Slider** — move the price until the break-even light turns green.
  - **Leak** — tap the bar in the waterfall that eats the most.
  - **What if** — choose, and watch six weeks of cash play out as bars.
  - **Order** — put the steps in order (pay yourself first; quote, deliver, invoice, collect).
- A bank of 60+ problems tied to the lessons of each chapter; a missed one returns two gates later.

## 4D — Ladders

- Business: **equip** (tools raise what one hour makes) → **expand** (a second shift, a second location) →
  **automate** (a system or a manager gives the owner's hours back) → **franchise**. Every rung shows price, payback
  in weeks, what it needs (knowledge level, a host's trust) and what it frees.
- Property: rent a room → buy a studio (down payment, mortgage) → rent it out → a building. Mr. Whiskerton's agency
  is the place; rent is income, repairs and vacancy are real.
- New places with their hosts: the agency (Mr. Whiskerton), the wholesale market (Rosa), the newsroom (Mia).

## 4E — Story lines

Each host has a line of five steps that ends in something the player keeps: Tony — from unloading his truck to
stocking his shelf, the first B2B client; Ms. Lee — reserve, credit history, first loan, credit line; Jay — the first
hire; Sam — from a table in the laundromat to a co-founder; Mr. Fins — the tempting line whose price arrives later;
the mentor — the exams and the chapter finals.

## Look

From the owner's mock-up: deep navy chrome, cream cards, one amber action per screen, large type, a picture at the
top of every screen, a segmented bar for time, an outline button for "not now". The cast stays the game's animals —
the raccoon is the brand. Screens are restyled as they are rebuilt, places first.

## Order

4A places (now) → 4C the gate on the current months (it does not need weeks) → 4B weeks → 4D ladders → 4E lines.
The gate comes before weeks because it is the part that teaches and it is independent of the calendar.
