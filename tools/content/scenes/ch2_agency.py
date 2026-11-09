# AI agency: chatbots and automations for small businesses.
F, W = "ch2_agency", [{"industry": "ai_agency"}]

SCENES = [
    S("ch2_agency_leads", F, "ch2", W, weight=12, intro=[
        ("", "You have two ways to find clients this month. Paid ads on a professional network, or asking happy clients for referrals.", "В этом месяце есть два способа найти клиентов: платная реклама в профессиональной сети или рекомендации довольных клиентов."),
        ("", "The ads promise leads at forty dollars each. Referrals cost a thank-you dinner.", "Реклама обещает лиды по сорок долларов. Рекомендации стоят ужина в благодарность."),
    ], choices=[
        C("ads", "Run the ads and track every dollar.", "Запустить рекламу и считать каждый доллар.", [lesson("cac_ltv"), cash(-50000), demand(1.2, 2)],
          ("", "Twelve calls, two clients. Now you know exactly what one costs.", "Двенадцать звонков, два клиента. Теперь ты точно знаешь, сколько стоит один.")),
        C("referrals", "Ask your best clients for introductions.", "Попросить лучших клиентов о знакомствах.", [stat("connections", 5), demand(1.1, 3), cash(-8000)],
          ("", "Slower, but the new clients already trust you.", "Медленнее, зато новые клиенты уже тебе доверяют.")),
    ]),
    S("ch2_agency_scope", F, "ch2", W, weight=6, intro=[
        ("", "A bakery client loves the chatbot you built. Now they want it to take orders, manage stock and post on social media.", "Пекарне нравится твой чат-бот. Теперь они хотят, чтобы он принимал заказы, вёл склад и постил в соцсети."),
        ("", "For the same price, of course.", "За ту же цену, конечно."),
    ], choices=[
        C("quote", "Send a new quote for the extra work.", "Прислать новую смету на дополнительную работу.", [lesson("pricing"), cash(90000), stat("reputation", 2)],
          ("", "They grumble, then sign. Clear scope, clear money.", "Поворчали — и подписали. Понятный объём, понятные деньги.")),
        C("yes", "Say yes to keep them happy.", "Согласиться, чтобы они были довольны.", [stat("energy", -12), stat("relationships", -3), npc("best_friend", -2)],
          ("", "You work three weekends for free. They tell everyone how flexible you are.", "Три выходных ты работаешь бесплатно. Они всем рассказывают, какой ты гибкий.")),
    ]),
    S("ch2_agency_late", F, "ch2", W, weight=6, intro=[
        ("", "A real estate firm owes you four thousand dollars. The invoice is forty days old. Rent is due Friday.", "Агентство недвижимости должно тебе четыре тысячи. Счёту сорок дней. Аренда — в пятницу."),
    ], choices=[
        C("call", "Call them and agree on a date.", "Позвонить и договориться о дате.", [lesson("cash_gap_b2b"), cash(200000), stat("energy", -2)],
          ("", "Half now, half in two weeks. You also add a late fee clause to every new contract.", "Половина сейчас, половина через две недели. А в каждый новый договор ты добавляешь пункт о пени.")),
        C("wait", "Wait politely. They're a big client.", "Вежливо ждать. Клиент крупный.", [cash(-40000), stat("energy", -4)],
          ("", "You put rent on a credit card. They pay in month three.", "Аренду ты оплачиваешь кредиткой. Они платят на третий месяц.")),
    ]),
    S("ch2_agency_privacy", F, "ch2", W, weight=5, intro=[
        ("", "A clinic wants a bot trained on its patient emails. Faster, they say. Nobody will know.", "Клиника хочет бота, обученного на письмах пациентов. «Так быстрее, — говорят они. — Никто не узнает»."),
    ], choices=[
        C("refuse", "Refuse and propose a version without private data.", "Отказаться и предложить версию без личных данных.", [stat("honesty", 5), stat("reputation", 4), cash(-20000)],
          ("", "It takes longer. Their lawyer later thanks you in writing.", "Получается дольше. Позже их юрист благодарит тебя письменно.")),
        C("do", "Do it their way and bill them.", "Сделать по-ихнему и выставить счёт.", [cash(150000), stat("honesty", -8), fact("privacy_risk")],
          ("", "The invoice clears. You stop sleeping well.", "Счёт оплачен. Ты перестаёшь спокойно спать.")),
    ]),
    S("ch2_agency_tools", F, "ch2", W, weight=4, intro=[
        ("", "Your AI tool subscriptions crept up to six hundred dollars a month. Half of them you opened twice.", "Подписки на ИИ-инструменты доползли до шестисот долларов в месяц. Половину ты открывал дважды."),
    ], choices=[
        C("audit", "Cancel everything you don't use weekly.", "Отменить всё, чем не пользуешься каждую неделю.", [cash(30000), stat("energy", 2)],
          ("", "Three hundred a month back. Nothing breaks.", "Минус триста в месяц. Ничего не сломалось.")),
        C("keep", "Keep them. You might need them.", "Оставить. Вдруг пригодятся.", [cash(-60000)],
          ("", "You might. The bill definitely needs you.", "Может, и пригодятся. А вот счёт точно приходит.")),
    ]),
    S("ch2_agency_pitch", F, "ch2", W + [{"stat": "reputation", "op": ">=", "value": 55}], weight=5, intro=[
        ("", "A grocery chain with twelve stores invites you to pitch. They want automated inventory alerts.", "Сеть из двенадцати продуктовых магазинов зовёт тебя на питч. Им нужны автоматические оповещения по складу."),
    ], choices=[
        C("prep", "Spend a week building a working demo.", "Неделю делать рабочее демо.", [stat("energy", -8), demand(1.25, 3), stat("reputation", 4)],
          ("", "The demo flags a real shortage live in the meeting. You win the contract.", "Прямо на встрече демо находит реальную нехватку товара. Контракт твой.")),
        C("slides", "Pitch with slides and promises.", "Выступить со слайдами и обещаниями.", [stat("reputation", -2), stat("energy", 2)],
          ("", "They thank you politely and pick the team with a demo.", "Тебя вежливо благодарят и выбирают команду с демо.")),
    ]),
]
