# Cleaning service: apartments and small offices.
F, W = "ch2_cleaning", [{"industry": "cleaning"}]

SCENES = [
    S("ch2_clean_first_hire", F, "ch2", W, weight=12, intro=[
        ("", "You clean four apartments a day and turn down three more. Your back has opinions about it.", "Ты убираешь четыре квартиры в день и отказываешь ещё трём. У твоей спины на этот счёт своё мнение."),
        ("Unpaid CEO", "You can keep doing every job yourself, or you can start building a company. Pick one.", "Можно и дальше делать всё самому, а можно начать строить компанию. Выбирай."),
    ], choices=[
        C("hire", "Write a job post and do it by the book.", "Написать вакансию и оформить всё по правилам.", [lesson("hiring"), stat("energy", 4), cash(-15000)],
          ("Unpaid CEO", "Payroll, insurance, a real contract. Now you're a boss with paperwork.", "Зарплата, страховка, настоящий договор. Теперь ты начальник с бумагами.")),
        C("solo", "Keep it solo a while longer.", "Ещё побыть одному.", [stat("energy", -8), stat("relationships", -2)],
          ("Unpaid CEO", "Your knees will remind you of this conversation.", "Колени напомнят тебе этот разговор.")),
    ]),
    S("ch2_clean_vase", F, "ch2", W, weight=6, intro=[
        ("", "A crash from the living room. A client's vase lies in pieces. She says it cost four hundred dollars.", "Грохот из гостиной. Ваза клиентки разбита вдребезги. Она говорит, что ваза стоила четыреста долларов."),
    ], choices=[
        C("pay", "Apologize and pay for it.", "Извиниться и заплатить.", [cash(-40000), stat("reputation", 4), stat("honesty", 3)],
          ("", "She books you every week after that. You buy liability insurance the same day.", "После этого она заказывает тебя каждую неделю. В тот же день ты покупаешь страховку ответственности.")),
        C("doubt", "Ask for a receipt first.", "Сначала попросить чек.", [stat("reputation", -4), cash(-15000)],
          ("", "She finds a receipt for a hundred and fifty. She also finds another cleaner.", "Она находит чек на сто пятьдесят. И находит другую уборщицу.")),
    ]),
    S("ch2_clean_office", F, "ch2", W, weight=6, intro=[
        ("", "A coworking space wants nightly cleaning on a twelve-month contract. Great money, paid net 30.", "Коворкинг хочет ежевечернюю уборку по договору на год. Отличные деньги, оплата через 30 дней."),
        ("", "You'd need someone working every night of the week.", "Понадобится человек, который работает каждую ночь недели."),
    ], choices=[
        C("sign", "Sign it and staff the night shift.", "Подписать и поставить ночную смену.", [demand(1.25, 4), stat("energy", -6), lesson("payroll")],
          ("", "Steady revenue. Steady salaries, too, that leave before the client pays.", "Стабильная выручка. И стабильные зарплаты, которые уходят раньше, чем платит клиент.")),
        C("pass", "Pass. Homes only for now.", "Отказаться. Пока только квартиры.", [stat("energy", 4), stat("relationships", 2)],
          ("", "You keep your evenings. Someone else gets the contract.", "Вечера остаются твоими. Контракт достаётся кому-то другому.")),
    ]),
    S("ch2_clean_cash", F, "ch2", W, weight=5, intro=[
        ("", "A new cleaner asks to be paid cash, off the books. Cheaper for both of us, she says.", "Новая уборщица просит платить ей наличными, без оформления. «Дешевле для нас обоих», — говорит она."),
    ], choices=[
        C("books", "Pay her on payroll like everyone else.", "Платить через зарплату, как всем.", [stat("honesty", 5), cash(-20000)],
          ("", "It costs more. If she gets hurt on the job, she's covered.", "Это дороже. Если она травмируется на работе, она защищена.")),
        C("cash", "Pay her cash.", "Платить наличными.", [cash(20000), stat("honesty", -8), fact("cash_payroll")],
          ("", "You save a little every week and worry about it every night.", "Ты экономишь немного каждую неделю и переживаешь об этом каждую ночь.")),
    ]),
    S("ch2_clean_green", F, "ch2", W, weight=4, intro=[
        ("", "Two clients ask whether you use non-toxic products. They have kids and a cat.", "Двое клиентов спрашивают, используешь ли ты нетоксичные средства. У них дети и кошка."),
    ], choices=[
        C("switch", "Switch to green products and say so.", "Перейти на эко-средства и сообщить об этом.", [cash(-25000), demand(1.12, 4), stat("reputation", 3)],
          ("", "Your flyer now says safe for kids and pets. The phone rings more.", "На листовке теперь написано «безопасно для детей и животных». Звонков больше.")),
        C("same", "Stick with what cleans best.", "Оставить то, что лучше отмывает.", [cash(5000), demand(0.97, 2)],
          ("", "Floors shine. One family quietly cancels.", "Полы блестят. Одна семья тихо отменяет заказ.")),
    ]),
    S("ch2_clean_referral", F, "ch2", W, weight=5, intro=[
        ("", "Half your new clients came from one building on Eastern Parkway. The doorman knows everyone.", "Половина новых клиентов пришла из одного дома на Истерн-Паркуэй. Консьерж знает всех."),
    ], choices=[
        C("bonus", "Offer twenty-five dollars for every referral.", "Платить двадцать пять долларов за каждую рекомендацию.", [lesson("cac_ltv"), demand(1.15, 3), cash(-10000)],
          ("", "Twenty-five dollars for a client who stays a year. Best ad you've ever bought.", "Двадцать пять долларов за клиента, который остаётся на год. Лучшая реклама, что ты покупал.")),
        C("thanks", "Bring the doorman coffee and say thanks.", "Принести консьержу кофе и поблагодарить.", [stat("connections", 4), npc("tony", 2)],
          ("", "He grins. Tony taught him the same trick years ago.", "Он улыбается. Тони научил его тому же приёму много лет назад.")),
    ]),
]
