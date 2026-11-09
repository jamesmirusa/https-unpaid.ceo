# Chapter 3 in Queens: the arcs grow up, and the rival goes after your team.
F = "ch3_queens"
CH3 = {"chapter": 3}

SCENES = [
    S("ch3_banker_review", F, "ch3", [CH3, {"fact": "met_banker"}, {"npc": "banker_lee", "op": ">=", "value": 5}], arc="banker", weight=6, intro=[
        ("Ms. Lee", "Two locations now. Congratulations. Let me ask the question nobody asks. How many months could you survive with zero sales?", "Уже две точки. Поздравляю. Задам вопрос, который никто не задаёт: сколько месяцев ты протянешь без единой продажи?"),
    ], choices=[
        C("reserve", "Move a thousand dollars into the reserve today.", "Сегодня же перевести тысячу долларов в резерв.", [reserve(100000), npc("banker_lee", 5), stat("energy", 2)],
          ("Ms. Lee", "That's how I know who to lend to. The ones who don't need it tomorrow.", "Вот так я и понимаю, кому давать в долг. Тем, кому он не нужен завтра.")),
        C("growth", "Every dollar goes into growth.", "Каждый доллар — в рост.", [npc("banker_lee", -3), demand(1.05, 2)],
          ("Ms. Lee", "Growth is great until the first bad month. Keep my number close.", "Рост — это прекрасно до первого плохого месяца. Держи мой номер под рукой.")),
    ]),
    S("ch3_rosa_contract", F, "ch3", [CH3, {"fact": "met_rosa"}, {"npc": "supplier_rosa", "op": ">=", "value": 5}], arc="supplier", weight=6, intro=[
        ("Rosa", "You're buying for two places now. Sign a twelve-month contract and I'll drop my prices eight percent.", "Теперь ты закупаешь на две точки. Подпиши договор на двенадцать месяцев, и я снижу цены на восемь процентов."),
        ("Rosa", "But a contract is a contract. Even in a slow month.", "Но договор есть договор. Даже в слабый месяц."),
    ], choices=[
        C("sign", "Sign the contract.", "Подписать договор.", [cash(60000), npc("supplier_rosa", 8), fact("rosa_contract")],
          ("Rosa", "Pleasure. First invoice comes with a free bag of something good.", "Приятно иметь дело. К первому счёту — бесплатный мешок чего-нибудь хорошего.")),
        C("flexible", "Stay flexible, buy month to month.", "Остаться гибким, покупать помесячно.", [stat("energy", 2), npc("supplier_rosa", -2)],
          ("Rosa", "Flexible costs extra. Your choice.", "Гибкость стоит дороже. Тебе решать.")),
    ]),
    S("ch3_mia_feature", F, "ch3", [CH3, {"fact": "met_mia"}, {"npc": "journalist_mia", "op": ">=", "value": 5}], arc="journalist", weight=6, intro=[
        ("Mia", "A citywide outlet picked up my story about you. They want numbers. Real ones, or the shiny ones?", "Общегородское издание подхватило мою историю о тебе. Им нужны цифры. Настоящие или красивые?"),
    ], choices=[
        C("real", "Give her the real numbers.", "Дать ей настоящие цифры.", [demand(1.2, 3), stat("reputation", 6), npc("journalist_mia", 5)],
          ("Mia", "The comments love you. Turns out people trust a founder who admits a bad month.", "Комментаторы в восторге. Оказывается, люди доверяют основателю, который признаёт плохой месяц.")),
        C("shiny", "Round everything up a little.", "Немного всё округлить в большую сторону.", [demand(1.25, 2), stat("honesty", -6), fact("mia_exaggeration")],
          ("", "The article glows. A competitor's accountant starts doing math.", "Статья сияет. Бухгалтер конкурента начинает считать.")),
    ]),
    S("ch3_sam_equity", F, "ch3", [CH3, {"fact": "met_sam"}, {"npc": "cofounder_sam", "op": ">=", "value": 5}], arc="cofounder", weight=6, intro=[
        ("Sam", "I've been helping on weekends for months. I want to go full time, for thirty percent of the company.", "Я уже несколько месяцев помогаю по выходным. Хочу работать полный день за тридцать процентов компании."),
        ("Unpaid CEO", "Equity is forever. Founders usually vest it over four years with a one-year cliff.", "Доля — это навсегда. Основатели обычно получают её постепенно за четыре года, первая часть — через год."),
    ], choices=[
        C("vesting", "Offer thirty percent, vesting over four years.", "Предложить тридцать процентов с вестингом на четыре года.", [npc("cofounder_sam", 10), stat("energy", 8), demand(1.1, 3), fact("sam_cofounder")],
          ("Sam", "Fair and smart. If I quit in six months, I shouldn't own a third. Deal.", "Честно и разумно. Если я уйду через полгода, мне не положена треть. Договорились.")),
        C("salary", "Offer a salary instead of equity.", "Предложить зарплату вместо доли.", [cash(-60000), npc("cofounder_sam", -4), stat("energy", 4)],
          ("Sam", "A job, not a company. I'll take it for now.", "Работа, а не компания. Пока соглашусь.")),
        C("no", "Say no. You'll stay the only owner.", "Отказаться. Останешься единственным владельцем.", [npc("cofounder_sam", -10), stat("energy", -4)],
          ("Sam", "Then I'll build my own. No hard feelings. Mostly.", "Тогда построю своё. Без обид. Почти.")),
    ]),
    S("ch3_night_market", F, "ch3", [CH3], weight=4, intro=[
        ("", "The Queens Night Market in Flushing Meadows has an open vendor spot this Saturday.", "На Ночном рынке Квинса во Флашинг-Медоуз в эту субботу освободилось место для продавца."),
    ], choices=[
        C("go", "Set up a stall and stay until midnight.", "Поставить прилавок и стоять до полуночи.", [cash(90000), stat("energy", -8), stat("connections", 4)],
          ("", "Ten thousand people, forty languages, one very long night.", "Десять тысяч человек, сорок языков, одна очень длинная ночь.")),
        C("rest", "Take the night off.", "Взять выходной вечер.", [stat("energy", 6), stat("relationships", 2)],
          ("", "You walk through it as a customer. Dumplings taste better when you're not selling.", "Ты гуляешь там как покупатель. Пельмени вкуснее, когда ты ничего не продаёшь.")),
    ]),
    S("ch3_burnout", F, "ch3", [CH3, {"stat": "energy", "op": "<=", "value": 40}], weight=8, intro=[
        ("Jay", "When did you last take a day off? Don't answer. I can see it on your face.", "Когда ты последний раз брал выходной? Не отвечай. По лицу видно."),
    ], choices=[
        C("break", "Take a full week off. The team covers.", "Взять целую неделю. Команда подстрахует.", [stat("energy", 15), stat("relationships", 6), demand(0.9, 1)],
          ("Jay", "Look at that. The business survived without you. That's called a business.", "Смотри-ка. Бизнес выжил без тебя. Это и называется бизнес.")),
        C("push", "Push through one more month.", "Дотянуть ещё месяц.", [stat("energy", -10), stat("relationships", -4), demand(1.05, 1)],
          ("Jay", "One more month. That's what you said last month.", "Ещё месяц. Ты то же самое говорил в прошлом месяце.")),
    ]),
    S("boss_fatcat_poach", F, "ch3", [CH3, {"fact": "chapter3_done"}], arc="rival", weight=60, intro=[
        ("", "Your two best people come in together. Mr. Whiskerton offered them twenty percent more to switch.", "Двое твоих лучших сотрудников приходят вместе. Мистер Вискертон предложил им на двадцать процентов больше, чтобы они перешли к нему."),
        ("Mr. Whiskerton", "Nothing personal. I just hire the best. You trained them so nicely.", "Ничего личного. Я просто нанимаю лучших. Ты их так хорошо обучил."),
    ], choices=[
        C("raise", "Match the offer with a raise.", "Перебить предложение повышением.", [cash(-300000), stat("reputation", 3), npc("fat_cat", -5)],
          ("", "They stay. Payroll grows, and so does their loyalty.", "Они остаются. Растут зарплаты, а вместе с ними — их преданность.")),
        C("refuse", "Refuse. Nobody gets a raise for a threat.", "Отказать. За угрозу повышений не бывает.", [staff(-2), stat("relationships", -3)],
          ("", "Two empty stations on Monday. The rest of the team noticed.", "В понедельник две пустых смены. Остальная команда это заметила.")),
        C("talk", "Talk about growth paths, offer a smaller raise.", "Поговорить о росте и предложить повышение поменьше.", [cash(-100000), staff(-1), stat("relationships", 3)],
          ("", "One stays for the future you described. One leaves for the money.", "Один остаётся ради будущего, которое ты описал. Другой уходит за деньгами.")),
    ]),
]
