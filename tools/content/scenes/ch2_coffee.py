# Coffee cart by a Brooklyn subway exit.
F, W = "ch2_coffee", [{"industry": "coffee_cart"}]

SCENES = [
    S("ch2_coffee_math", F, "ch2", W, weight=12, intro=[
        ("Unpaid CEO", "Before you pour a single latte, give me one cup. Not to drink. To count.", "Прежде чем наливать латте, дай мне одну чашку. Не пить. Считать."),
        ("Unpaid CEO", "Beans, milk, lid, sleeve, the cup itself. What does one cup really cost you?", "Зёрна, молоко, крышка, манжета, сам стакан. Сколько на самом деле стоит тебе одна чашка?"),
    ], choices=[
        C("count", "Count every cent with him.", "Посчитать с ним каждый цент.", [lesson("unit_economics"), stat("energy", -3)],
          ("Unpaid CEO", "A dollar twenty. Now you know what four dollars is really worth.", "Доллар двадцать. Теперь ты знаешь, чего на самом деле стоят четыре доллара.")),
        C("skip", "Just sell more, the math will follow.", "Просто продавать больше, цифры подтянутся.", [stat("energy", 3), npc("mentor", -3)],
          ("Unpaid CEO", "Volume hides mistakes. For a while.", "Объём прячет ошибки. На какое-то время.")),
    ]),
    S("ch2_coffee_inspector", F, "ch2", W, weight=6, intro=[
        ("", "A city inspector in a reflective vest circles your cart twice and taps the expired sticker on the window.", "Городской инспектор в светоотражающем жилете дважды обходит тележку и стучит по просроченной наклейке на окне."),
        ("", "Your mobile vending permit renewal is three weeks late.", "Продление разрешения на уличную торговлю опоздало на три недели."),
    ], choices=[
        C("pay", "Pay the late fee and renew today.", "Заплатить штраф и продлить сегодня.", [cash(-30000), stat("reputation", 3), stat("honesty", 2)],
          ("", "The inspector nods. A clean sticker goes on the glass.", "Инспектор кивает. На стекло ложится новая наклейка.")),
        C("argue", "Argue that the office never sent a reminder.", "Спорить, что из офиса не прислали напоминание.", [stat("reputation", -4), demand(0.9, 1)],
          ("", "You win the argument and lose two days of sales while the cart sits closed.", "Спор ты выигрываешь, а два дня продаж теряешь, пока тележка стоит закрытой.")),
    ]),
    S("ch2_coffee_oatmilk", F, "ch2", W, weight=6, intro=[
        ("", "Every third customer asks the same thing. Do you have oat milk?", "Каждый третий покупатель спрашивает одно и то же: а овсяное молоко есть?"),
        ("", "A carton costs you about forty cents per drink. The café across the street charges seventy-five cents extra.", "Пачка обходится примерно в сорок центов на напиток. Кафе через дорогу берёт за него семьдесят пять центов сверху."),
    ], choices=[
        C("charge", "Add oat milk for seventy-five cents extra.", "Добавить овсяное за семьдесят пять центов сверху.", [lesson("pricing"), demand(1.1, 3), cash(-8000)],
          ("", "The line gets longer. So does the list of things you have to keep in stock.", "Очередь становится длиннее. Как и список того, что нужно держать в запасе.")),
        C("free", "Offer it free to win the regulars.", "Давать бесплатно, чтобы завоевать постоянных.", [demand(1.15, 3), cash(-20000), stat("reputation", 2)],
          ("", "People love it. Your margin feels it every single cup.", "Людям нравится. Твоя маржа чувствует это с каждой чашкой.")),
        C("simple", "Keep the menu simple.", "Оставить меню простым.", [stat("energy", 4), demand(0.97, 2)],
          ("", "Fewer cartons, fewer decisions, a few customers walking past.", "Меньше пачек, меньше решений и несколько покупателей, проходящих мимо.")),
    ]),
    S("ch2_coffee_loyalty", F, "ch2", W, weight=5, intro=[
        ("", "A nurse who buys two coffees every morning holds up a punch card from a chain store.", "Медсестра, которая каждое утро берёт два кофе, показывает карточку лояльности сетевой кофейни."),
        ("", "Ten stamps and the next one is free. You don't do that, she says. Should you?", "Десять отметок — и следующий бесплатно. «У вас такого нет», — говорит она. А стоит завести?"),
    ], choices=[
        C("card", "Print punch cards. Tenth coffee free.", "Напечатать карточки. Десятый кофе бесплатно.", [lesson("cac_ltv"), demand(1.1, 4), cash(-6000)],
          ("", "Free coffee costs a dollar twenty. A regular who stays a year is worth far more.", "Бесплатный кофе стоит доллар двадцать. Постоянный гость, который остаётся на год, стоит куда больше.")),
        C("no", "No discounts. Quality speaks for itself.", "Никаких скидок. Качество говорит само за себя.", [stat("reputation", -1), demand(0.97, 2)],
          ("", "She still buys. Her coworkers try the chain.", "Она всё равно покупает. Её коллеги пробуют сеть.")),
    ]),
    S("ch2_coffee_rosa_beans", F, "ch2", W + [{"fact": "met_rosa"}], arc="supplier", weight=6, intro=[
        ("Rosa", "I roast in Red Hook. Better beans than your bulk bags, honestly. Twenty percent more per pound.", "Я обжариваю в Ред-Хуке. Зёрна лучше твоих оптовых мешков, честно. На двадцать процентов дороже за фунт."),
        ("Rosa", "Your customers will taste it. Your accountant will notice it.", "Покупатели почувствуют вкус. Твой бухгалтер почувствует цену."),
    ], choices=[
        C("switch", "Switch to Rosa's beans.", "Перейти на зёрна Розы.", [cash(-40000), npc("supplier_rosa", 10), stat("reputation", 5), demand(1.05, 3)],
          ("Rosa", "Good choice. I deliver Tuesdays. Don't make me wait at the curb.", "Хороший выбор. Привожу по вторникам. Не заставляй ждать у бордюра.")),
        C("stay", "Stay with the cheap bags for now.", "Пока остаться на дешёвых мешках.", [npc("supplier_rosa", -3), cash(5000)],
          ("Rosa", "Your call. My number doesn't change.", "Как скажешь. Мой номер не меняется.")),
    ]),
    S("ch2_coffee_rain", F, "ch2", W, weight=4, once=False, cooldown=8, intro=[
        ("", "A week of cold rain. The subway stairs are empty, and so is your tip jar.", "Неделя холодного дождя. На лестнице метро пусто, и в банке для чаевых тоже."),
    ], choices=[
        C("move", "Move the cart under the train overpass.", "Перекатить тележку под эстакаду.", [stat("energy", -6), demand(1.1, 1)],
          ("", "Wet commuters find you. Your shoes never dry.", "Промокшие пассажиры находят тебя. Твои ботинки так и не высыхают.")),
        C("rest", "Close early and rest.", "Закрыться пораньше и отдохнуть.", [stat("energy", 8), demand(0.85, 1)],
          ("", "You sleep ten hours. The cart sleeps too.", "Ты спишь десять часов. Тележка тоже.")),
    ]),
]
