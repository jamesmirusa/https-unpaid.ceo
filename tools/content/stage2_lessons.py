#!/usr/bin/env python3
"""Stage 2A, Task 9: append the nine chapter 2-3 lessons to lessons.json and their EN/RU text to Academy.tsv.

Idempotent: lessons and rows that already exist are replaced, not duplicated.
Facts checked 2026-11-08: NY LLC fees and publication (dos.ny.gov, NY LLC Law s.206), NYC minimum wage
$17.00 from 2026-01-01 (NYS DOL), SBA 7(a) up to $5M and microloans up to $50,000 (sba.gov), employer FICA
7.65%, IRS estimated-tax dates, self-employment tax 15.3%.
"""
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "Assets", "UnpaidCeo")
LESSONS = os.path.join(ROOT, "Content", "lessons.json")
TSV = os.path.join(ROOT, "Localization", "Source", "Academy.tsv")

# id, trigger fact, skill, title (en, ru), body (en, ru), questions: (question en, ru, [(option en, ru) x3], correct index)
L = [
    ("unit_economics", "industry_chosen", "finance",
     ("Unit economics", "Юнит-экономика"),
     ("Before you buy the first cart, truck or van, look at a single sale. A coffee cart sells a cup for $4, and milk, beans and the cup cost $1.20. That leaves $2.80, a 70% gross margin. Fixed costs arrive every month whether anyone buys or not: $1,200 for the permit, parking and insurance needs 429 cups just to be covered. That is your break-even point. Unit economics asks three questions. What does one sale leave you? How many sales cover the month? What does a customer cost to win, and what do they bring over time? If one sale loses money, selling more only loses it faster. The Company screen shows these numbers for your business every month.",
      "Прежде чем покупать первую тележку, фургон или грузовик, посмотри на одну продажу. Кофейная тележка продаёт чашку за 4 $, а молоко, зёрна и стаканчик стоят 1,20 $. Остаётся 2,80 $ — это валовая маржа 70%. Постоянные расходы приходят каждый месяц, даже если никто ничего не купил: 1 200 $ за разрешение, стоянку и страховку покроют только 429 чашек. Это твоя точка безубыточности. Юнит-экономика задаёт три вопроса. Сколько оставляет одна продажа? Сколько продаж покрывают месяц? Сколько стоит привлечь клиента и сколько он принесёт за всё время? Если одна продажа убыточна, рост продаж только ускоряет убытки. Экран «Компания» каждый месяц показывает эти цифры для твоего бизнеса."),
     [("A cup sells for $4 and costs $1.20 to make. What is the gross margin?", "Чашка стоит 4 $, а её себестоимость — 1,20 $. Какая валовая маржа?",
       [("30%", "30%"), ("70%", "70%"), ("$4", "4 $")], 1),
      ("Fixed costs are $1,200 a month and each sale leaves $2.80. How many sales do you need to break even?", "Постоянные расходы — 1 200 $ в месяц, каждая продажа оставляет 2,80 $. Сколько продаж нужно для безубыточности?",
       [("429", "429"), ("300", "300"), ("1,200", "1 200")], 0),
      ("Each sale loses 50 cents. What happens if you sell twice as much?", "Каждая продажа приносит 50 центов убытка. Что будет, если продавать вдвое больше?",
       [("You break even sooner", "Быстрее выйдешь в ноль"), ("Profit doubles", "Прибыль удвоится"), ("You lose money twice as fast", "Будешь терять деньги вдвое быстрее")], 2)]),

    ("pricing", "price_changed", "sales",
     ("Setting the price", "Как назначать цену"),
     ("Price is the fastest lever you have, and the easiest to get wrong. Raise it and each sale leaves more, but some customers walk away. Cut it and more people buy, but each sale leaves less. A resale store sells jackets that cost it $15. At $40 it earns $25 a jacket; at $30 only $15, so it must sell about two-thirds more jackets just to earn the same. How strongly demand reacts to price is called elasticity. Coffee by a subway exit barely notices a 25-cent rise; a jacket next to a cheaper shop does. Change prices in small steps, watch a full month of sales, and remember that a discount is easy to start and hard to take back.",
      "Цена — самый быстрый рычаг, и ошибиться с ним проще всего. Поднимешь — каждая продажа оставит больше, но часть клиентов уйдёт. Снизишь — купят больше людей, но каждая продажа оставит меньше. Магазин ресейла покупает куртки по 15 $. По 40 $ он зарабатывает 25 $ на куртке, по 30 $ — только 15 $, и чтобы заработать столько же, нужно продать примерно на две трети больше. Насколько спрос реагирует на цену, называется эластичностью. Кофе у выхода из метро почти не заметит подорожания на 25 центов, а куртку рядом с более дешёвым магазином — заметят. Меняй цену небольшими шагами, смотри продажи за целый месяц и помни: скидку легко ввести и трудно отменить."),
     [("A jacket costs you $15. At $40 you earn $25 each, at $30 only $15. How many more jackets must you sell at $30 to earn the same?", "Куртка обходится тебе в 15 $. По 40 $ ты зарабатываешь 25 $ с куртки, по 30 $ — 15 $. На сколько больше курток нужно продать по 30 $, чтобы заработать столько же?",
       [("About two-thirds more", "Примерно на две трети больше"), ("10% more", "На 10% больше"), ("The same number", "Столько же")], 0),
      ("What does elasticity measure?", "Что измеряет эластичность?",
       [("How fast prices rise every year", "Как быстро цены растут каждый год"), ("How much demand changes when the price changes", "Насколько меняется спрос при изменении цены"), ("How stretchy your products are", "Насколько тянутся твои товары")], 1),
      ("What is the safest way to test a new price?", "Как безопаснее всего проверить новую цену?",
       [("Halve it for a week", "Снизить вдвое на неделю"), ("Copy the most expensive competitor", "Скопировать самого дорогого конкурента"), ("Change it a little and watch a month of sales", "Немного изменить и смотреть продажи месяц")], 2)]),

    ("estimated_taxes", "break_even", "finance",
     ("Profit means taxes", "Прибыль — значит налоги"),
     ("Your unit just covered its costs. Congratulations: now there is profit, and profit is taxed. An employee has tax taken out of every paycheck; an owner has to send it in. In the US, owners usually pay estimated taxes four times a year: by April 15, June 15, September 15 and January 15. Self-employed people also owe self-employment tax, 15.3% for Social Security and Medicare on most of their profit, on top of income tax. Missed payments bring penalties and a painful bill in April. The habit that saves founders is simple: move a share of every month's profit into a separate tax account and don't touch it. The game sets aside a tax reserve for you each month; in real life nobody does it for you.",
      "Твоя точка только что покрыла свои расходы. Поздравляю: теперь есть прибыль, а прибыль облагается налогом. У наёмного работника налог удерживают из каждой зарплаты, а владелец платит его сам. В США владельцы обычно вносят налог авансом четыре раза в год: до 15 апреля, 15 июня, 15 сентября и 15 января. Самозанятые платят ещё и налог на самозанятость — 15,3% на соцстрахование и медицину с большей части прибыли — сверх подоходного налога. За пропущенные платежи начисляют штрафы, а в апреле приходит болезненный счёт. Привычка, которая спасает основателей, проста: каждый месяц откладывай часть прибыли на отдельный налоговый счёт и не трогай его. В игре налоговый резерв откладывается за тебя, в жизни этого никто не сделает."),
     [("How often do US business owners usually pay estimated taxes?", "Как часто владельцы бизнеса в США обычно платят налог авансом?",
       [("Once, in April", "Один раз, в апреле"), ("Four times a year", "Четыре раза в год"), ("Only when they sell the business", "Только при продаже бизнеса")], 1),
      ("What is the self-employment tax for Social Security and Medicare?", "Каков налог на самозанятость на соцстрахование и медицину?",
       [("15.3%", "15,3%"), ("1.5%", "1,5%"), ("50%", "50%")], 0),
      ("Which habit keeps a tax bill from becoming a crisis?", "Какая привычка не даёт налоговому счёту стать кризисом?",
       [("Spend the profit now and pay later", "Тратить прибыль сейчас, платить потом"), ("Ask customers to pay the tax", "Просить клиентов заплатить налог"), ("Move part of each month's profit to a separate tax account", "Откладывать часть прибыли каждого месяца на отдельный налоговый счёт")], 2)]),

    ("llc", "chapter2_start", "finance",
     ("Why an LLC", "Зачем нужна LLC"),
     ("Moving to Brooklyn means running a real business, and a real business needs a legal structure. As a sole proprietor, you and the business are the same person: if it owes money or gets sued, your own savings are on the line. A limited liability company, an LLC, puts a wall between the two. In New York, forming one costs $200 to file Articles of Organization with the Department of State. Then comes a New York quirk: within 120 days you must publish a notice in two newspapers chosen by the county clerk for six weeks in a row and file a $50 Certificate of Publication. In New York City the newspapers alone can cost over $1,000. Skip it, and the LLC loses its right to do business until you fix it.",
      "Переезд в Бруклин — это уже настоящий бизнес, а настоящему бизнесу нужна юридическая форма. Когда ты индивидуальный предприниматель, ты и бизнес — одно лицо: если бизнес должен денег или на него подали в суд, под ударом твои личные сбережения. Компания с ограниченной ответственностью, LLC, ставит стену между ними. В Нью-Йорке её регистрация стоит 200 $ за подачу Articles of Organization в Департамент штата. Дальше — нью-йоркская особенность: в течение 120 дней нужно публиковать объявление в двух газетах, которые выберет секретарь округа, шесть недель подряд, и подать Certificate of Publication за 50 $. В Нью-Йорке одни только газеты могут стоить больше 1 000 $. Пропустишь — LLC теряет право вести бизнес, пока ты это не исправишь."),
     [("What is the main point of an LLC?", "В чём главный смысл LLC?",
       [("Lower prices for customers", "Более низкие цены для клиентов"), ("It separates your personal money from the business's debts", "Она отделяет твои личные деньги от долгов бизнеса"), ("You never pay taxes", "Ты никогда не платишь налоги")], 1),
      ("What does New York charge to file Articles of Organization for an LLC?", "Сколько стоит в Нью-Йорке подача Articles of Organization для LLC?",
       [("$200", "200 $"), ("$20", "20 $"), ("$2,000", "2 000 $")], 0),
      ("What happens if a New York LLC skips the publication requirement?", "Что будет, если нью-йоркская LLC не выполнит требование о публикации?",
       [("Nothing at all", "Ничего"), ("It becomes a corporation", "Она станет корпорацией"), ("Its right to do business is suspended until it publishes", "Её право вести бизнес приостановят, пока она не опубликует объявление")], 2)]),

    ("hiring", "first_hire", "leadership",
     ("Your first hire", "Первый сотрудник"),
     ("Your first employee changes your job: now you lead, not only work. Hire for the gap that costs you the most, like the shifts you can't cover or the calls you can't take. In New York City the minimum wage in 2026 is $17 an hour, and hours over 40 a week are paid at time and a half. Before the first day you need an EIN, an I-9 form to check the right to work, tax forms, and workers' compensation and disability insurance, and you report the new hire to New York State within 20 days. A good first hire pays for themselves by freeing your time for selling and planning; a wrong one costs months. Before you post the job, write down what success looks like after 30 days.",
      "Первый сотрудник меняет твою работу: теперь ты руководишь, а не только работаешь. Нанимай туда, где пробел обходится дороже всего: смены, которые ты не закрываешь, звонки, на которые не успеваешь ответить. В Нью-Йорке минимальная оплата в 2026 году — 17 $ в час, а часы сверх 40 в неделю оплачиваются в полуторном размере. До первого рабочего дня нужны EIN, форма I-9 для проверки права на работу, налоговые формы, страховки от несчастных случаев на работе и по нетрудоспособности, а о новом сотруднике нужно сообщить штату Нью-Йорк в течение 20 дней. Хороший первый сотрудник окупается, освобождая твоё время для продаж и планов, неудачный стоит месяцев. Прежде чем публиковать вакансию, запиши, как выглядит успех через 30 дней."),
     [("What is the minimum wage in New York City in 2026?", "Какая минимальная оплата в Нью-Йорке в 2026 году?",
       [("$7.25", "7,25 $"), ("$17.00", "17,00 $"), ("$25.50", "25,50 $")], 1),
      ("An employee works 45 hours one week at $18 an hour. How are the 5 extra hours paid?", "Сотрудник отработал за неделю 45 часов по 18 $ в час. Как оплачиваются 5 лишних часов?",
       [("At $27 an hour, time and a half", "По 27 $ в час, в полуторном размере"), ("At $18, like the rest", "По 18 $, как остальные"), ("They are not paid", "Никак не оплачиваются")], 0),
      ("What should you write down before posting a job?", "Что стоит записать перед публикацией вакансии?",
       [("Your competitors' salaries", "Зарплаты у конкурентов"), ("A list of holidays", "Список праздников"), ("What success looks like after 30 days", "Как выглядит успех через 30 дней")], 2)]),

    ("payroll", "first_payroll", "operations",
     ("The real cost of payroll", "Сколько на самом деле стоит зарплата"),
     ("An employee paid $3,000 a month costs you more than $3,000. On top of the salary, the employer pays 7.65% for Social Security and Medicare, plus federal and state unemployment insurance, workers' compensation and disability insurance. Many small businesses budget 10 to 30% above wages for all of it. Payroll also keeps a schedule your customers don't: salaries leave on a fixed day even when clients pay late. That is why payroll is the cost that most often turns a profitable month into an empty account. In the game, every hire adds the salary plus 10% overhead to your fixed costs. Before hiring, check that gross profit covers the full cost, not just the salary.",
      "Сотрудник с зарплатой 3 000 $ в месяц обходится дороже 3 000 $. Сверх зарплаты работодатель платит 7,65% на соцстрахование и медицину, а ещё федеральное и штатное страхование от безработицы, страховки от несчастных случаев и по нетрудоспособности. Многие малые бизнесы закладывают на всё это 10–30% сверх зарплат. И у зарплаты свой график, которого нет у клиентов: она уходит в фиксированный день, даже если клиенты платят с опозданием. Поэтому именно зарплаты чаще всего превращают прибыльный месяц в пустой счёт. В игре каждый сотрудник добавляет к постоянным расходам зарплату и ещё 10% сверху. Перед наймом проверь, что валовая прибыль покрывает полную стоимость сотрудника, а не только зарплату."),
     [("Besides the salary, what share does an employer pay for Social Security and Medicare?", "Какую долю сверх зарплаты работодатель платит на соцстрахование и медицину?",
       [("7.65%", "7,65%"), ("0.5%", "0,5%"), ("50%", "50%")], 0),
      ("Why does payroll so often cause cash problems?", "Почему зарплаты так часто создают проблемы с деньгами?",
       [("Banks block salary payments", "Банки блокируют выплату зарплат"), ("Salaries leave on a fixed day even when clients pay late", "Зарплата уходит в фиксированный день, даже если клиенты платят с опозданием"), ("Employees can only be paid in cash", "Сотрудникам можно платить только наличными")], 1),
      ("A worker earns $3,000 a month. Which monthly budget is realistic?", "Сотрудник получает 3 000 $ в месяц. Какой месячный бюджет реалистичен?",
       [("Exactly $3,000", "Ровно 3 000 $"), ("$2,500, because of taxes", "2 500 $, из-за налогов"), ("About $3,300 to $3,900 with employer costs", "Около 3 300–3 900 $ с расходами работодателя")], 2)]),

    ("cash_gap_b2b", "receivable_gap", "finance",
     ("When clients pay later", "Когда клиенты платят позже"),
     ("Business clients rarely pay on the spot. A warehouse that hires your box truck may pay \"net 30\" or \"net 60\": thirty or sixty days after the invoice. Meanwhile fuel, insurance and your driver's pay go out every week. So a fast-growing B2B business can be profitable on paper and broke in the bank: the more jobs you win, the more cash sits in unpaid invoices, called accounts receivable. Tools that close the gap: send the invoice the day the job is done, ask for a deposit, offer a small discount for paying within 10 days, keep a reserve of one or two months of costs, or open a business line of credit. Never fund the gap with a loan shark.",
      "Бизнес-клиенты редко платят сразу. Склад, который нанимает твой грузовик, может платить «net 30» или «net 60» — через тридцать или шестьдесят дней после счёта. А топливо, страховка и зарплата водителя уходят каждую неделю. Поэтому быстро растущий B2B-бизнес может быть прибыльным на бумаге и пустым в банке: чем больше заказов ты берёшь, тем больше денег застревает в неоплаченных счетах — дебиторской задолженности. Что закрывает разрыв: выставляй счёт в день выполнения работы, проси предоплату, давай небольшую скидку за оплату в течение 10 дней, держи резерв на один-два месяца расходов или открой бизнес-кредитную линию. Никогда не закрывай разрыв займом у ростовщика."),
     [("A client pays \"net 30\". What does that mean?", "Клиент платит «net 30». Что это значит?",
       [("They pay 30% now", "Платит 30% сразу"), ("They pay within 30 days after the invoice", "Платит в течение 30 дней после счёта"), ("They pay $30 per job", "Платит 30 $ за заказ")], 1),
      ("You win more B2B jobs, but your bank balance keeps shrinking. What is the most likely reason?", "Ты берёшь всё больше B2B-заказов, а денег на счёте всё меньше. Какая причина вероятнее всего?",
       [("Cash is stuck in unpaid invoices while costs go out now", "Деньги застряли в неоплаченных счетах, а расходы идут сейчас"), ("Your prices are too high", "У тебя слишком высокие цены"), ("Clients pay too early", "Клиенты платят слишком рано")], 0),
      ("Which tool closes a cash gap without new expensive debt?", "Что закрывает кассовый разрыв без нового дорогого долга?",
       [("A loan shark", "Ростовщик"), ("Hiring more staff", "Нанять ещё сотрудников"), ("Invoicing the day the job is done and asking for a deposit", "Счёт в день выполнения работы и предоплата")], 2)]),

    ("cac_ltv", "marketing_started", "marketing",
     ("What a customer costs and brings", "Сколько стоит и сколько приносит клиент"),
     ("Marketing spends money to win customers, so measure what a customer costs and what one brings. Customer acquisition cost, CAC, is marketing spend divided by new customers: $500 of flyers that bring 100 new regulars is $5 each. Lifetime value, LTV, is what a customer leaves you over all their visits: a coffee that leaves $2.80, bought 20 times, is worth $56. A common rule of thumb is to keep LTV at least three times CAC. Below that, growth eats your cash; far above it, you may be spending too little to grow. Track both every month, as the Company screen does for you, and cut the channels that bring customers who never come back.",
      "Маркетинг тратит деньги на привлечение клиентов, поэтому считай, сколько стоит клиент и сколько он приносит. Стоимость привлечения, CAC, — это расходы на маркетинг, делённые на число новых клиентов: листовки за 500 $, которые привели 100 новых постоянных гостей, — это 5 $ за каждого. Пожизненная ценность, LTV, — сколько клиент оставит за все визиты: кофе, который оставляет 2,80 $, купленный 20 раз, — это 56 $. Распространённое правило: LTV должна быть хотя бы в три раза больше CAC. Ниже — рост съедает твои деньги, намного выше — возможно, ты слишком мало вкладываешь в рост. Следи за обоими показателями каждый месяц — экран «Компания» делает это за тебя — и отключай каналы, которые приводят клиентов, не возвращающихся снова."),
     [("$500 of ads brought 100 new customers. What is the CAC?", "Реклама за 500 $ привела 100 новых клиентов. Каков CAC?",
       [("$5", "5 $"), ("$500", "500 $"), ("$50,000", "50 000 $")], 0),
      ("A coffee leaves $2.80 and a regular buys 20 of them. What is the LTV?", "Кофе оставляет 2,80 $, постоянный гость покупает 20 чашек. Какая LTV?",
       [("$4", "4 $"), ("$28", "28 $"), ("$56", "56 $")], 2),
      ("Which LTV-to-CAC ratio is a common healthy target?", "Какое соотношение LTV к CAC считается здоровым ориентиром?",
       [("Below 1 to 1", "Меньше 1 к 1"), ("At least 3 to 1", "Не меньше 3 к 1"), ("Exactly 1 to 1", "Ровно 1 к 1")], 1)]),

    ("loans", "first_loan", "finance",
     ("Borrowing money", "Как брать кредит"),
     ("A loan buys time with money: you get cash now and pay back more later. The price is the interest rate, shown as an APR. At 12% a year, $10,000 costs about $100 of interest a month at first, and less as you repay it. Banks look at your credit, cash flow and reputation; a weaker record means a higher rate. In the US, the Small Business Administration backs loans made by banks and other lenders: SBA microloans go up to $50,000 and 7(a) loans up to $5 million, with interest capped by its rules. Borrow for things that earn, like a second cart or a truck, not to cover losses you don't understand. The payment is due whether sales come or not, so check it against your worst month, not your best.",
      "Кредит — это покупка времени за деньги: деньги сейчас, а вернуть больше потом. Цена — процентная ставка, годовая, APR. Под 12% годовых 10 000 $ стоят поначалу около 100 $ процентов в месяц и меньше по мере погашения. Банки смотрят на кредитную историю, денежный поток и репутацию: чем она слабее, тем выше ставка. В США Администрация малого бизнеса (SBA) гарантирует кредиты банков и других кредиторов: микрокредиты SBA — до 50 000 $, кредиты 7(a) — до 5 миллионов $, а ставки ограничены её правилами. Бери в долг на то, что зарабатывает, — вторую тележку или грузовик, — а не чтобы закрыть убытки, причины которых не понимаешь. Платёж нужно вносить, будут продажи или нет, поэтому проверяй его на своём худшем месяце, а не на лучшем."),
     [("What is the most an SBA microloan can be?", "Какой максимальный размер микрокредита SBA?",
       [("$5,000", "5 000 $"), ("$50,000", "50 000 $"), ("$5 million", "5 миллионов $")], 1),
      ("What is the best use of a business loan?", "На что лучше всего брать бизнес-кредит?",
       [("Covering losses you can't explain", "Закрыть убытки, которые не можешь объяснить"), ("A vacation after a good month", "Отпуск после удачного месяца"), ("Equipment that will earn more than the payments", "Оборудование, которое заработает больше платежей")], 2),
      ("Which month should you test a loan payment against?", "На каком месяце стоит проверять, потянешь ли платёж по кредиту?",
       [("Your worst month", "На худшем"), ("Your best month", "На лучшем"), ("An average of your best months", "На среднем из лучших")], 0)]),
]


def main():
    with open(LESSONS, encoding="utf-8") as f:
        data = json.load(f)
    ids = {l[0] for l in L}
    data["lessons"] = [x for x in data["lessons"] if x["id"] not in ids]
    rows = []
    for lid, fact, skill, title, body, quiz in L:
        k = f"lesson.{lid}"
        entry = {"id": lid, "triggerFact": fact, "titleKey": k + ".title", "bodyKey": k + ".body", "quiz": [],
                 "skillReward": {"skill": skill, "xp": 5}}
        rows += [(k + ".title",) + title, (k + ".body",) + body]
        for qi, (qen, qru, options, correct) in enumerate(quiz):
            assert len(options) == 3 and 0 <= correct < 3
            qk = f"{k}.q{qi}"
            entry["quiz"].append({"questionKey": qk, "optionKeys": [f"{qk}.a{i}" for i in range(3)], "correctIndex": correct})
            rows.append((qk, qen, qru))
            rows += [(f"{qk}.a{i}", oen, oru) for i, (oen, oru) in enumerate(options)]
        data["lessons"].append(entry)
    # Keep the file's one-lesson-per-line layout.
    with open(LESSONS, "w", encoding="utf-8") as f:
        f.write('{ "lessons": [\n' + ",\n".join("  " + json.dumps(x, ensure_ascii=False) for x in data["lessons"]) + "\n] }\n")

    with open(TSV, encoding="utf-8") as f:
        lines = f.read().rstrip("\n").split("\n")
    keys = {r[0] for r in rows}
    lines = [ln for ln in lines if ln.split("\t", 1)[0] not in keys]
    for r in rows:
        assert all("\t" not in c and "\n" not in c for c in r), r[0]
        lines.append("\t".join(r))
    with open(TSV, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    words = {lid: len(body[0].split()) for lid, _, _, _, body, _ in L}
    print("lessons:", len(data["lessons"]), "rows added:", len(rows), "body words:", words)


if __name__ == "__main__":
    main()
