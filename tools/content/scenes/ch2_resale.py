# Resale store: vintage clothes and sneakers in Brooklyn.
F, W = "ch2_resale", [{"industry": "resale_store"}]

SCENES = [
    S("ch2_resale_jacket", F, "ch2", W, weight=12, intro=[
        ("", "A real 1990s varsity jacket lands on your counter. You paid fifteen dollars for it at an estate sale.", "На прилавок ложится настоящая университетская куртка из девяностых. Ты отдал за неё пятнадцать долларов на распродаже имущества."),
        ("", "Online, the same jacket goes for anywhere between forty and a hundred and twenty.", "В интернете такая же уходит за сумму от сорока до ста двадцати."),
    ], choices=[
        C("research", "Check what actually sold, then price it.", "Посмотреть реальные продажи и назначить цену.", [lesson("pricing"), cash(6500), stat("energy", -2)],
          ("", "Sold listings say eighty. It goes in four days at eighty.", "Проданные лоты говорят: восемьдесят. За восемьдесят она уходит за четыре дня.")),
        C("quick", "Tag it at forty and move it fast.", "Повесить ценник сорок и быстро продать.", [cash(2500), stat("energy", 2)],
          ("", "Gone in an hour. The buyer relists it the same night for a hundred.", "Ушла за час. Покупатель в тот же вечер выставляет её за сотню.")),
    ]),
    S("ch2_resale_fake", F, "ch2", W, weight=6, intro=[
        ("", "A guy offers you ten designer bags for two hundred dollars. The stitching is a little too perfect.", "Парень предлагает десять дизайнерских сумок за двести долларов. Строчка какая-то слишком идеальная."),
        ("", "Sell them as real and you could clear two thousand.", "Если продать их как настоящие, можно заработать две тысячи."),
    ], choices=[
        C("refuse", "Refuse. Your shop sells real things.", "Отказаться. Твой магазин продаёт настоящие вещи.", [stat("honesty", 5), stat("reputation", 3)],
          ("", "He shrugs and tries the next store. Word gets around that you don't play.", "Он пожимает плечами и идёт в следующий магазин. Слух о том, что ты в такое не играешь, расходится.")),
        C("buy", "Buy them and say nothing.", "Купить и промолчать.", [cash(180000), stat("honesty", -10), fact("sold_fakes")],
          ("", "The money is fast. A review calling your store a scam is faster.", "Деньги приходят быстро. Отзыв, где твой магазин называют мошенниками, — ещё быстрее.")),
    ]),
    S("ch2_resale_fakes_review", F, "ch2", W + [{"fact": "sold_fakes"}], weight=20, intro=[
        ("", "A customer posts a video taking the bag apart at the seams. Two hundred thousand views by lunch.", "Покупательница выкладывает видео, где распарывает сумку по швам. К обеду — двести тысяч просмотров."),
    ], choices=[
        C("refund", "Refund everyone and apologize publicly.", "Вернуть всем деньги и публично извиниться.", [cash(-200000), stat("honesty", 6), stat("reputation", -4), demand(0.85, 2)],
          ("", "It hurts. But the comments slowly change from scam to at least they owned it.", "Больно. Но комментарии медленно меняются с «мошенники» на «хотя бы признали».")),
        C("deny", "Delete the comments and wait.", "Удалить комментарии и переждать.", [stat("reputation", -12), demand(0.7, 3)],
          ("", "The internet keeps screenshots. Foot traffic drops for weeks.", "Интернет хранит скриншоты. Посетителей становится меньше на недели.")),
    ]),
    S("ch2_resale_drop", F, "ch2", W, weight=5, intro=[
        ("", "You have a rack of rare sneakers. Jay says a Friday night drop on social media could pack the store.", "У тебя стойка редких кроссовок. Джей говорит, что дроп в пятницу вечером в соцсетях может забить магазин."),
    ], choices=[
        C("drop", "Do the drop with Jay filming.", "Сделать дроп, Джей снимает.", [demand(1.25, 1), npc("best_friend", 6), stat("energy", -6)],
          ("Jay", "Told you. Line around the block. You owe me a slice.", "Говорил же. Очередь за угол. С тебя кусок пиццы.")),
        C("list", "List them online one by one.", "Выставить онлайн по одной.", [cash(30000), stat("energy", 2)],
          ("", "Steady sales, no crowd, no buzz.", "Стабильные продажи, ни толпы, ни шума.")),
    ]),
    S("ch2_resale_consign", F, "ch2", W, weight=5, intro=[
        ("", "A collector wants you to sell her closet on consignment. She keeps sixty percent, you keep forty.", "Коллекционер просит продать её гардероб на комиссии. Ей шестьдесят процентов, тебе сорок."),
        ("", "No money up front, but the racks will be full of her clothes for months.", "Без вложений, но стойки на месяцы будут заняты её вещами."),
    ], choices=[
        C("accept", "Take the consignment deal.", "Взять вещи на комиссию.", [demand(1.1, 3), stat("connections", 4)],
          ("", "Her friends start dropping by. Some of them have closets too.", "К тебе начинают заходить её подруги. У некоторых тоже есть гардеробы.")),
        C("buy", "Offer to buy the best pieces outright.", "Предложить выкупить лучшие вещи сразу.", [cash(-60000), demand(1.15, 3)],
          ("", "You pay more today and keep every dollar of the markup.", "Сегодня платишь больше, зато вся наценка остаётся тебе.")),
    ]),
    S("ch2_resale_shoplifter", F, "ch2", W, weight=4, intro=[
        ("", "A teenager slips a beanie into his hoodie. You see it on the mirror. He sees you seeing it.", "Подросток прячет шапку под толстовку. Ты видишь это в зеркале. Он видит, что ты видишь."),
    ], choices=[
        C("talk", "Talk to him quietly.", "Тихо с ним поговорить.", [stat("reputation", 3), stat("relationships", 2), cash(-1500)],
          ("", "He puts it back. A week later he brings his older sister to shop.", "Он возвращает шапку. Через неделю приводит старшую сестру за покупками.")),
        C("police", "Call the police.", "Вызвать полицию.", [stat("energy", -4), stat("reputation", -2)],
          ("", "You lose the afternoon to a report about a twelve-dollar beanie.", "Ты теряешь полдня на протокол из-за шапки за двенадцать долларов.")),
    ]),
]
