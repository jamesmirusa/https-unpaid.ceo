# Chapter 2 in Brooklyn: new people (banker, supplier, journalist, co-founder) and the rival's price war.
F = "ch2_brooklyn"
CH2 = {"chapter": 2}

SCENES = [
    S("ch2_banker_intro", F, "ch2", [CH2, {"notFact": "met_banker"}], arc="banker", weight=8, intro=[
        ("", "An owl in a tweed vest knocks on your door with a business card. Community bank, Atlantic Avenue.", "В дверь стучит сова в твидовом жилете с визиткой. Общественный банк, Атлантик-авеню."),
        ("Ms. Lee", "I'm Ms. Lee. I lend to small businesses in this neighborhood. I like to meet owners before they need me.", "Я мисс Ли. Я кредитую малый бизнес в этом районе. Люблю знакомиться с владельцами до того, как я им понадоблюсь."),
    ], choices=[
        C("meet", "Invite her in and show her your numbers.", "Пригласить её и показать свои цифры.", [npc("banker_lee", 10), fact("met_banker"), lesson("loans")],
          ("Ms. Lee", "Clean books. Rare. Call me before you buy your next unit, not after.", "Чистые книги. Редкость. Звоните до покупки следующей точки, а не после.")),
        C("later", "Take the card and say you'll call.", "Взять визитку и пообещать позвонить.", [fact("met_banker"), stat("connections", 1)],
          ("Ms. Lee", "Everyone says that. The smart ones do.", "Все так говорят. Умные — звонят.")),
    ]),
    S("ch2_rosa_intro", F, "ch2", [CH2, {"notFact": "met_rosa"}], arc="supplier", weight=7, intro=[
        ("", "A hen in steel-toe boots drops a box of samples on your counter and doesn't wait to be asked.", "Курица в ботинках со стальными носами ставит на прилавок коробку образцов и не ждёт приглашения."),
        ("Rosa", "Rosa. Supplies, Red Hook. Whatever you sell, somebody is overcharging you for the stuff it's made of.", "Роза. Поставки, Ред-Хук. Что бы ты ни продавал, кто-то переплачивает тебе за то, из чего это сделано."),
    ], choices=[
        C("listen", "Ask what she can do on price.", "Спросить, что она может сделать по цене.", [npc("supplier_rosa", 8), fact("met_rosa"), stat("connections", 3)],
          ("Rosa", "Pay on time and I'll beat your current supplier. Pay late once and I won't.", "Плати вовремя — и я перебью твоего поставщика. Опоздаешь хоть раз — нет.")),
        C("busy", "Say you're happy with your supplier.", "Сказать, что доволен своим поставщиком.", [fact("met_rosa"), stat("energy", 2)],
          ("Rosa", "Nobody's happy with their supplier. Keep the samples.", "Никто не доволен своим поставщиком. Образцы оставь себе.")),
    ]),
    S("ch2_mia_intro", F, "ch2", [CH2, {"notFact": "met_mia"}, {"sinceFact": "chapter2_start", "min": 2}], arc="journalist", weight=6, intro=[
        ("", "A fox with a voice recorder stops you outside. She writes for a Brooklyn neighborhood news site.", "Лиса с диктофоном останавливает тебя на улице. Она пишет для новостного сайта района в Бруклине."),
        ("Mia", "Mia. I'm doing a piece on new businesses on this street. Got ten minutes and the truth?", "Миа. Делаю материал о новых бизнесах на этой улице. Есть десять минут и правда?"),
    ], choices=[
        C("honest", "Tell her the real story, losses included.", "Рассказать как есть, вместе с убытками.", [npc("journalist_mia", 10), fact("met_mia"), stat("reputation", 4), demand(1.08, 2)],
          ("Mia", "Honest founders make the best stories. Readers can smell a brochure.", "Честные основатели — лучшие истории. Читатели чуют рекламный буклет.")),
        C("decline", "Politely decline. Too early for press.", "Вежливо отказаться. Рано для прессы.", [fact("met_mia"), stat("energy", 2)],
          ("Mia", "Fair. I'll check back when you're bigger.", "Справедливо. Загляну, когда станешь больше.")),
    ]),
    S("ch2_sam_intro", F, "ch2", [CH2, {"notFact": "met_sam"}, {"sinceFact": "chapter2_start", "min": 4}], arc="cofounder", weight=6, intro=[
        ("", "Sam, a beaver you studied with, shows up with two coffees and a spreadsheet.", "Сэм, бобр, с которым ты учился, приходит с двумя кофе и таблицей."),
        ("Sam", "I've watched you do everything alone. I'm good at operations. What if we built this together?", "Я смотрю, как ты всё делаешь один. Я силён в операционке. Что если строить это вместе?"),
    ], choices=[
        C("talk", "Hear Sam out over the spreadsheet.", "Выслушать Сэма и разобрать таблицу.", [npc("cofounder_sam", 10), fact("met_sam"), stat("connections", 3)],
          ("Sam", "No rush. Think about what you'd want in writing.", "Без спешки. Подумай, что ты хочешь закрепить на бумаге.")),
        C("solo", "Say you want to stay solo.", "Сказать, что хочешь оставаться один.", [fact("met_sam"), npc("cofounder_sam", -5), stat("energy", 2)],
          ("Sam", "Understood. Offer stands if you change your mind.", "Понял. Предложение в силе, если передумаешь.")),
    ]),
    S("ch2_block_party", F, "ch2", [CH2], weight=4, intro=[
        ("", "Your block is throwing a summer party. The organizers ask local businesses to chip in three hundred dollars.", "Твой квартал устраивает летний праздник. Организаторы просят местный бизнес скинуться по триста долларов."),
    ], choices=[
        C("sponsor", "Sponsor it and set up a table.", "Стать спонсором и поставить столик.", [cash(-30000), stat("reputation", 5), stat("relationships", 2), demand(1.1, 1)],
          ("", "Kids in face paint, neighbors with your flyers. Good afternoon.", "Дети в аквагриме, соседи с твоими листовками. Хороший день.")),
        C("skip", "Skip it and work.", "Пропустить и работать.", [stat("energy", -2), cash(10000)],
          ("", "You hear the music from the shop. Nobody comes in.", "Музыку слышно из магазина. Никто не заходит.")),
    ]),
    S("ch2_mom_visit", F, "ch2", [CH2], weight=4, intro=[
        ("Mom", "So this is the famous Brooklyn studio. It's small. Is the business doing well, or are you just saying that?", "Значит, это та самая студия в Бруклине. Маленькая. Дела правда идут хорошо, или ты просто так говоришь?"),
    ], choices=[
        C("dinner", "Close early and take her to dinner.", "Закрыться пораньше и сводить её поужинать.", [cash(-8000), stat("relationships", 8), stat("energy", 3)],
          ("Mom", "You look tired, but proud. That's how your grandfather looked.", "Выглядишь уставшим, но гордым. Так выглядел твой дедушка.")),
        C("work", "Show her the numbers, then get back to work.", "Показать ей цифры и вернуться к работе.", [cash(15000), stat("relationships", -5)],
          ("Mom", "Numbers are nice. Call me sometime without them.", "Цифры — это хорошо. Позвони как-нибудь и без них.")),
    ]),
    S("boss_fatcat_price_war", F, "ch2", [CH2, {"fact": "chapter2_done"}], arc="rival", weight=60, intro=[
        ("", "A banner goes up across the street. Same business as yours, twenty percent cheaper. Grand opening.", "Через дорогу вешают баннер. Такой же бизнес, как у тебя, на двадцать процентов дешевле. Торжественное открытие."),
        ("Mr. Whiskerton", "Remember me? Your old landlord. I liked your idea so much I bought one too. Let's see who lasts.", "Помнишь меня? Твой бывший арендодатель. Мне так понравилась твоя идея, что я завёл такую же. Посмотрим, кто дольше протянет."),
        ("Unpaid CEO", "He can lose money longer than you can. Don't fight him where he's strongest.", "Он может терять деньги дольше тебя. Не дерись с ним там, где он сильнее."),
    ], choices=[
        C("ignore", "Ignore him and change nothing.", "Не обращать внимания и ничего не менять.", [demand(0.8, 3), fact("fatcat_price_war")],
          ("", "For three months his banner is louder than your quality.", "Три месяца его баннер громче твоего качества.")),
        C("match", "Match his prices.", "Сравняться с ним по ценам.", [cash(-150000), demand(0.95, 2), fact("fatcat_price_war"), npc("fat_cat", -5)],
          ("", "Customers stay. Your margin goes to war instead of you.", "Клиенты остаются. Вместо тебя воюет твоя маржа.")),
        C("different", "Compete on service his chain can't copy.", "Соревноваться сервисом, который его сеть не скопирует.", [cash(-80000), stat("reputation", 5), demand(0.9, 2), fact("fatcat_price_war")],
          ("Unpaid CEO", "Price is a race to the bottom. Loyalty is a race he doesn't know how to run.", "Цена — это гонка на дно. Лояльность — гонка, в которой он не умеет бегать.")),
    ]),
]
