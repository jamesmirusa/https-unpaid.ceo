# Food truck: halal-style plates and tacos around Brooklyn.
F, W = "ch2_foodtruck", [{"industry": "food_truck"}]

SCENES = [
    S("ch2_truck_spot", F, "ch2", W, weight=8, intro=[
        ("", "You pull into the spot outside the hospital at six in the morning. Another truck is already there.", "В шесть утра ты подъезжаешь к месту у больницы. Там уже стоит другой фургон."),
        ("", "Its owner leans out the window. I've parked here for nine years, friend.", "Хозяин высовывается из окна: «Я паркуюсь здесь девять лет, друг»."),
    ], choices=[
        C("share", "Offer to split the days of the week.", "Предложить делить дни недели.", [stat("connections", 4), stat("reputation", 2), demand(0.95, 2)],
          ("", "Mondays to Wednesdays are yours. He even tells you which nurses tip.", "Понедельник, вторник и среда — твои. Он даже рассказывает, какие медсёстры оставляют чаевые.")),
        C("fight", "Park there anyway, every day.", "Всё равно парковаться там каждый день.", [stat("reputation", -5), demand(1.05, 2), stat("energy", -5)],
          ("", "You get the spot. You also get a parking ticket every week from someone who knows the rules better.", "Место твоё. А ещё каждую неделю штраф от того, кто знает правила лучше.")),
        C("move", "Find a new corner by the community college.", "Найти новый угол у колледжа.", [demand(1.0, 1), stat("energy", -2), stat("connections", 2)],
          ("", "Students are broke but loyal. You add a five-dollar plate.", "У студентов нет денег, но они верные. Ты добавляешь тарелку за пять долларов.")),
    ]),
    S("ch2_truck_festival", F, "ch2", W, weight=6, intro=[
        ("", "A weekend street festival in Prospect Park offers you a vendor slot. The fee is eight hundred dollars, up front.", "Уличный фестиваль на выходных в Проспект-парке предлагает место. Взнос — восемьсот долларов вперёд."),
        ("", "Last year's vendors say a good truck clears three thousand. A rainy one clears nothing.", "Прошлогодние участники говорят: хороший фургон зарабатывает три тысячи. В дождь — ничего."),
    ], choices=[
        C("go", "Pay the fee and prep double.", "Заплатить взнос и готовить вдвое больше.", [cash(160000), stat("energy", -10), stat("reputation", 3)],
          ("", "Sunshine. You sell out by four and still have a line.", "Солнце. К четырём всё распродано, а очередь всё стоит.")),
        C("skip", "Skip it. Too much risk for one weekend.", "Пропустить. Слишком рискованно ради одних выходных.", [stat("energy", 5), stat("relationships", 3)],
          ("", "You spend Saturday with family. Sunday you hear it was a record crowd.", "Субботу ты проводишь с семьёй. В воскресенье узнаёшь, что там был рекорд посетителей.")),
    ]),
    S("ch2_truck_fdny", F, "ch2", W, weight=6, intro=[
        ("", "An FDNY inspector checks the propane tanks on your truck. One valve is past its test date.", "Инспектор пожарной службы проверяет баллоны с пропаном. У одного вентиля истёк срок проверки."),
    ], choices=[
        C("fix", "Close for a day and replace it.", "Закрыться на день и заменить.", [cash(-45000), stat("reputation", 3), demand(0.9, 1)],
          ("", "New valve, new certificate, one lost day. You sleep better.", "Новый вентиль, новый сертификат, один потерянный день. Спишь спокойнее.")),
        C("tape", "Keep working and fix it next month.", "Работать дальше, починить в следующем месяце.", [stat("honesty", -4), cash(-120000), stat("reputation", -6)],
          ("", "He comes back. The fine costs more than the valve ever would.", "Он возвращается. Штраф стоит гораздо больше вентиля.")),
    ]),
    S("ch2_truck_price", F, "ch2", W, weight=8, intro=[
        ("", "Chicken prices jumped fifteen percent. Your ten-dollar plate now leaves you two dollars less.", "Курица подорожала на пятнадцать процентов. Тарелка за десять долларов теперь приносит на два доллара меньше."),
        ("Unpaid CEO", "Eat the cost, cut the portion, or raise the price. Each one teaches you something.", "Проглотить расходы, урезать порцию или поднять цену. Каждый вариант чему-то учит."),
    ], choices=[
        C("raise", "Raise the plate to eleven dollars.", "Поднять тарелку до одиннадцати долларов.", [lesson("pricing"), demand(0.95, 2), cash(25000)],
          ("", "A few regulars grumble. Almost all of them still order.", "Несколько постоянных ворчат. Почти все всё равно заказывают.")),
        C("portion", "Quietly make the portion smaller.", "Тихо уменьшить порцию.", [stat("honesty", -3), stat("reputation", -4), cash(20000)],
          ("", "Nobody says anything. They just come a little less often.", "Никто ничего не говорит. Просто приходят чуть реже.")),
        C("absorb", "Keep the price and absorb it.", "Оставить цену и взять расходы на себя.", [cash(-30000), stat("reputation", 2)],
          ("", "Customers stay happy. Your margin gets thinner.", "Клиенты довольны. Маржа становится тоньше.")),
    ]),
    S("ch2_truck_generator", F, "ch2", W, weight=4, intro=[
        ("", "The generator coughs, sputters and dies during the lunch rush. Twenty people stare at you.", "Генератор кашляет, чихает и глохнет в обеденный пик. На тебя смотрят двадцать человек."),
    ], choices=[
        C("cash_sale", "Sell cold drinks and chips at a discount.", "Продавать холодные напитки и чипсы со скидкой.", [stat("reputation", 3), cash(-5000)],
          ("", "People leave with something and a story. You call a mechanic.", "Люди уходят хоть с чем-то и с историей. Ты звонишь механику.")),
        C("new", "Buy a new generator today.", "Купить новый генератор сегодня.", [cash(-110000), stat("energy", 3)],
          ("", "Expensive, but it purrs. No more praying at noon.", "Дорого, но он мурлычет. Больше никаких молитв в полдень.")),
    ]),
    S("ch2_truck_viral", F, "ch2", W, weight=4, intro=[
        ("", "A food blogger with a million followers posts your lamb plate. Your phone will not stop buzzing.", "Фудблогер с миллионом подписчиков выкладывает твою тарелку с бараниной. Телефон не умолкает."),
    ], choices=[
        C("ride", "Extend hours and ride the wave.", "Продлить часы работы и поймать волну.", [demand(1.3, 2), stat("energy", -12)],
          ("", "Lines for two weeks. You learn exactly how much one person can grill.", "Очереди две недели. Ты точно узнаёшь, сколько может нажарить один человек.")),
        C("steady", "Keep normal hours and keep quality.", "Работать как обычно и держать качество.", [demand(1.15, 3), stat("reputation", 4)],
          ("", "Fewer plates than you could sell, but every one is right.", "Тарелок меньше, чем могло бы уйти, зато каждая — как надо.")),
    ]),
]
