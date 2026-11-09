# Box truck logistics: deliveries for warehouses and shops.
F, W = "ch2_logistics", [{"industry": "logistics_box_truck"}]

SCENES = [
    S("ch2_box_net60", F, "ch2", W, weight=12, intro=[
        ("", "A furniture warehouse offers steady routes, five days a week. Their terms are net 60.", "Мебельный склад предлагает постоянные маршруты пять дней в неделю. Условия — оплата через 60 дней."),
        ("", "Fuel, tolls and your driver's pay are due every week.", "Топливо, платные дороги и зарплата водителя — каждую неделю."),
    ], choices=[
        C("negotiate", "Ask for net 30 and a deposit.", "Попросить 30 дней и предоплату.", [lesson("cash_gap_b2b"), demand(1.15, 3), stat("energy", -2)],
          ("", "They agree to net 30 if you start Monday. You start Monday.", "Они согласны на 30 дней, если начнёшь в понедельник. Ты начинаешь в понедельник.")),
        C("accept", "Accept net 60 as it is.", "Согласиться на 60 дней как есть.", [demand(1.3, 3), cash(-60000)],
          ("", "The work pours in. The money arrives two months later.", "Работа хлынула. Деньги придут через два месяца.")),
    ]),
    S("ch2_box_fuel", F, "ch2", W, weight=6, intro=[
        ("", "Diesel jumps forty cents a gallon overnight. Your contracts don't say who pays for that.", "Дизель за ночь дорожает на сорок центов за галлон. В договорах не сказано, кто за это платит."),
    ], choices=[
        C("surcharge", "Add a fuel surcharge to new invoices.", "Добавить топливную надбавку в новые счета.", [lesson("pricing"), cash(30000), stat("reputation", -1)],
          ("", "Most clients accept it. The industry does it, and now so do you.", "Большинство клиентов соглашаются. Так делает вся отрасль, теперь и ты.")),
        C("eat", "Absorb it and hope prices fall.", "Взять на себя и надеяться, что цены упадут.", [cash(-50000)],
          ("", "They don't, this month.", "В этом месяце — не упали.")),
    ]),
    S("ch2_box_overweight", F, "ch2", W, weight=5, intro=[
        ("", "A client asks you to take one more pallet. It would put the truck over its weight limit.", "Клиент просит взять ещё один паллет. С ним грузовик превысит допустимый вес."),
    ], choices=[
        C("second", "Make a second trip.", "Сделать второй рейс.", [stat("energy", -6), cash(-8000), stat("reputation", 3)],
          ("", "An extra hour on the BQE. Everything arrives in one piece.", "Лишний час на скоростной. Всё доезжает целым.")),
        C("overload", "Load it anyway.", "Всё равно загрузить.", [cash(-90000), stat("honesty", -3)],
          ("", "A weigh station on the way. The ticket wipes out a week of profit.", "По дороге весовой пункт. Штраф съедает недельную прибыль.")),
    ]),
    S("ch2_box_driver_ot", F, "ch2", W, weight=6, intro=[
        ("", "Your driver logged fifty-two hours this week. Overtime starts after forty.", "Твой водитель отработал на этой неделе пятьдесят два часа. Сверхурочные начинаются после сорока."),
    ], choices=[
        C("pay", "Pay the overtime and plan a second driver.", "Оплатить сверхурочные и планировать второго водителя.", [lesson("payroll"), cash(-30000), stat("reputation", 3)],
          ("", "Time and a half adds up fast. A second driver starts to look cheaper.", "Полуторная оплата быстро набегает. Второй водитель начинает выглядеть дешевле.")),
        C("comp", "Offer a day off instead of pay.", "Предложить отгул вместо денег.", [stat("honesty", -4), demand(0.92, 1)],
          ("", "He takes the day and checks the labor rules that night.", "Он берёт отгул, а вечером читает трудовые правила.")),
    ]),
    S("ch2_box_rush", F, "ch2", W, weight=4, intro=[
        ("", "A bakery's supplier van broke down. They need flour across Brooklyn by five a.m. and will pay triple.", "У поставщика пекарни сломался фургон. Им нужна мука через весь Бруклин к пяти утра, платят втрое."),
    ], choices=[
        C("go", "Take the night run.", "Взять ночной рейс.", [cash(45000), stat("energy", -8), stat("connections", 3)],
          ("", "Flour on time, croissants on time. The owner saves your number.", "Мука вовремя, круассаны вовремя. Хозяин сохраняет твой номер.")),
        C("sleep", "Say no and sleep.", "Отказаться и поспать.", [stat("energy", 6)],
          ("", "You sleep through it. Somebody else gets a new client.", "Ты всё проспал. Новый клиент достался кому-то другому.")),
    ]),
    S("ch2_box_insurance", F, "ch2", W, weight=4, intro=[
        ("", "Your commercial auto insurance renewal arrives. Twenty percent higher than last year.", "Пришло продление коммерческой автостраховки. На двадцать процентов дороже, чем в прошлом году."),
    ], choices=[
        C("shop", "Get three quotes before renewing.", "Собрать три предложения перед продлением.", [cash(25000), stat("energy", -3)],
          ("", "A broker finds the same coverage for less. An afternoon well spent.", "Брокер находит то же покрытие дешевле. Удачно потраченный день.")),
        C("renew", "Renew and move on.", "Продлить и забыть.", [cash(-20000), stat("energy", 2)],
          ("", "Quick and expensive.", "Быстро и дорого.")),
    ]),
]
